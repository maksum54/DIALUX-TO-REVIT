#!/usr/bin/env python3
"""
Reference implementation / oracle for the DIALux DXF reader.

This mirrors, step for step, what DialuxToRevit.Core does in C#. It exists so the
parsing rules can be verified against real DIALux exports without opening Revit,
and so the C# port has a known-good set of numbers to be checked against.

Usage:
    python3 tools/dxf_probe.py <file.dxf> [--json out.json]
"""

import argparse
import json
import math
import re
import sys
from collections import Counter, defaultdict

# DIALux layer naming. Multi-building / multi-storey aware.
RE_LUM = re.compile(r"^DLX_(?:BLD(\d+)_FL(\d+)_|(TERR)_)?LUM\s*(\d+)$", re.IGNORECASE)
RE_KEY = re.compile(r"^DLX_(?:BLD(\d+)_FL(\d+)_|(TERR)_)?LUMKEY$", re.IGNORECASE)
RE_IDX = re.compile(r"^DLX_(?:BLD(\d+)_FL(\d+)_|(TERR)_)?LUMKEY_IDX$", re.IGNORECASE)
RE_TITLE = re.compile(r"^Luminaire list\s*\((.+)\)\s*$", re.IGNORECASE)
# Block names look like "39794_2_0": <productId>_<variant>_<part>. The trailing
# part index is what splits one physical luminaire across several INSERTs.
RE_BLOCK_PART = re.compile(r"^(.*)_(\d+)$")

# Two INSERTs are the same luminaire when their insertion points agree to
# within this distance. DIALux repeats identical doubles bit-for-bit, so this
# only needs to absorb formatting noise, not real tolerance.
POSITION_QUANTUM_MM = 0.1
# How far an index label may sit from the luminaire it annotates.
LABEL_SEARCH_RADIUS_MM = 1000.0


def warn(warnings, severity, code, message):
    """Warning codes and severities mirror DialuxToRevit.Core exactly, so the
    C# port can be checked against this implementation case for case."""
    warnings.append({"severity": severity, "code": code, "message": message})


# --------------------------------------------------------------------------
# Low level DXF
# --------------------------------------------------------------------------

def read_pairs(path):
    """ASCII DXF is strictly alternating code/value lines. Read it that way."""
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        lines = fh.read().split("\n")
    lines = [ln.rstrip("\r") for ln in lines]
    pairs = []
    i = 0
    while i < len(lines) - 1:
        code = lines[i].strip()
        if not code:
            i += 1
            continue
        try:
            pairs.append((int(code), lines[i + 1].strip()))
        except ValueError:
            raise ValueError(
                f"malformed DXF at line {i + 1}: expected a group code, got {code!r}"
            )
        i += 2
    return pairs


def section(pairs, name):
    out, inside = [], False
    for i, (code, value) in enumerate(pairs):
        if code == 2 and i > 0 and pairs[i - 1] == (0, "SECTION"):
            inside = value == name
            continue
        if code == 0 and value == "ENDSEC":
            inside = False
            continue
        if inside:
            out.append((code, value))
    return out


def split_entities(pairs):
    """Group a flat pair stream into entities; code 0 opens each one."""
    entities, current = [], None
    for code, value in pairs:
        if code == 0:
            if current:
                entities.append(current)
            current = [(code, value)]
        elif current is not None:
            current.append((code, value))
    if current:
        entities.append(current)
    return entities


def read_block_sizes(pairs):
    """
    Bounding box per block definition, in block units.

    Block geometry is polyface mesh vertices; DIALux stores it in metres and
    scales it by 1000 at insertion, so the size in millimetres is the box here
    multiplied by the INSERT scale. Useful for suggesting a Revit family and for
    warning when the chosen one is a very different size.
    """
    body = section(pairs, "BLOCKS")
    sizes, name, verts, vx = {}, None, [], {}
    collecting = False

    def close(current, points):
        if current and points:
            sizes[current] = tuple(
                max(pt[i] for pt in points) - min(pt[i] for pt in points)
                for i in range(3)
            )

    i = 0
    while i < len(body):
        code, value = body[i]
        if code == 0 and value == "BLOCK":
            close(name, verts)
            name, verts, collecting = "?", [], False
        elif code == 2 and name == "?":
            name = value
        elif code == 0 and value == "VERTEX":
            collecting, vx = True, {}
        elif code == 0:
            collecting = False
        elif collecting and code in (10, 20, 30):
            vx[code] = float(value)
            if len(vx) == 3:
                verts.append((vx[10], vx[20], vx[30]))
                vx = {}
        i += 1
    close(name, verts)
    return sizes


def first(entity, code, default=None):
    for c, v in entity:
        if c == code:
            return v
    return default


# --------------------------------------------------------------------------
# Luminaire list (ACAD_TABLE)
# --------------------------------------------------------------------------

def parse_table(entity):
    """
    Read an AcDbTable into a rows x cols grid of strings.

    Cell count is authoritative: code 90 states it, and every cell emits a
    `301 CELL_VALUE` record even when it holds no text. Relying on the text
    values alone would silently shift columns whenever DIALux leaves one blank
    (it leaves 'Item number' blank in the sample export).
    """
    body = [(c, v) for c, v in entity if c != 310]
    try:
        start = next(i for i, (c, v) in enumerate(body) if c == 100 and v == "AcDbTable")
    except StopIteration:
        return None

    rows = cols = None
    for c, v in body[start:]:
        if c == 91 and rows is None:
            rows = int(v)
        elif c == 92 and cols is None:
            cols = int(v)
        if rows is not None and cols is not None:
            break
    if not rows or not cols:
        return None

    # Walk cells in row-major order. A cell runs from its CELL_VALUE marker to
    # the next one; its text is code 302, with code 1 as fallback.
    marks = [i for i, (c, v) in enumerate(body) if c == 301 and v == "CELL_VALUE"]
    grid = [["" for _ in range(cols)] for _ in range(rows)]
    for n, mark in enumerate(marks):
        if n >= rows * cols:
            break
        end = marks[n + 1] if n + 1 < len(marks) else len(body)
        text = ""
        for c, v in body[mark:end]:
            if c == 302 and v:
                text = v
                break
        if not text:
            for c, v in body[mark:end]:
                if c == 1 and v:
                    text = v
                    break
        grid[n // cols][n % cols] = text
    return grid


def parse_luminaire_list(grid):
    """Turn the raw grid into {index: {column: value}} plus the title."""
    if not grid:
        return "", {}
    title = grid[0][0].strip()
    headers = [h.strip() for h in grid[1]]
    try:
        idx_col = headers.index("Index")
    except ValueError:
        idx_col = 0
    types = {}
    for row in grid[2:]:
        key = row[idx_col].strip()
        if not key.isdigit():
            continue
        record = {
            headers[i] if i < len(headers) and headers[i] else f"col{i}": row[i].strip()
            for i in range(len(row))
        }
        record["Product"] = product_name(record)
        record["Description"] = describe(record)
        types[int(key)] = record
    return title, types


def product_name(record):
    """
    DIALux is not consistent about which column carries the product name: in the
    sample export type 2 leaves 'Article name' blank and puts PLPA40L-E/65 under
    'Item number', while the other three types do the opposite. Read both.
    """
    for column in ("Article name", "Item number"):
        value = record.get(column, "").strip()
        if value:
            return value
    return record.get("Fitting", "").strip()


def describe(record):
    """One line for the mapping grid, so the user can tell what the type is."""
    parts = [record.get("Manufacturer", "").strip(), product_name(record)]
    fitting = record.get("Fitting", "").strip()
    if fitting and fitting not in parts:
        parts.append(fitting)
    detail = " / ".join(p for p in parts if p)
    extras = [record.get(c, "").strip() for c in ("Luminous flux", "Connected load")]
    extras = [e for e in extras if e]
    return f"{detail} - {' - '.join(extras)}" if extras else detail


def split_title(title):
    """'Luminaire list (Building 2, OFFICE GF)' -> ('Building 2', 'OFFICE GF')."""
    m = RE_TITLE.match(title or "")
    if not m:
        return "", ""
    inner = m.group(1)
    # Storey names are the more likely to contain a comma, so split on the
    # first separator and keep the remainder whole.
    if ", " in inner:
        building, storey = inner.split(", ", 1)
        return building.strip(), storey.strip()
    return inner.strip(), ""


# --------------------------------------------------------------------------
# Fixtures
# --------------------------------------------------------------------------

def block_base(name):
    m = RE_BLOCK_PART.match(name or "")
    return m.group(1) if m else (name or "")


def quantize(value):
    return int(round(float(value) / POSITION_QUANTUM_MM))


RE_DLX_BLOCK = re.compile(r"^\d+_\d+_\d+$")


def classify_layers(entities, warnings):
    """
    Map each luminaire layer to (building, floor, type_index).

    DIALux-named layers are parsed. When a file has none (renamed in CAD or a
    custom scheme), every layer holding DIALux luminaire blocks -- or, failing
    that, any INSERT -- becomes one type, numbered in layer-name order.
    """
    inserts = [e for e in entities if e[0][1] == "INSERT"]
    layers = {}
    for ent in inserts:
        layer = first(ent, 8, "")
        m = RE_LUM.match(layer)
        if m and layer not in layers:
            layers[layer] = (
                int(m.group(1) or 0),
                int(m.group(2)) if m.group(2) else (-1 if m.group(3) else 0),
                int(m.group(4)),
            )
    if layers:
        return layers

    candidates = [e for e in inserts if RE_DLX_BLOCK.match(first(e, 2, "") or "")]
    if not candidates:
        candidates = inserts
    names = sorted({first(e, 8, "") for e in candidates}, key=str.lower)
    for i, name in enumerate(names):
        layers[name] = (0, 0, i + 1)
    if names:
        warn(warnings, "Info", "LAYERS_NOT_DIALUX",
             f"No DIALux-named luminaire layers; each of the {len(names)} "
             f"layer(s) holding luminaire blocks is read as one type "
             f"({', '.join(names)}).")
    return layers


def collect_fixtures(entities, warnings, block_sizes=None, layers=None):
    """
    Fold INSERTs down to physical luminaires.

    One luminaire is frequently exported as several INSERTs sharing an exact
    insertion point (housing and optic as separate blocks). Counting INSERTs
    would overstate the fixture count -- in the sample, 99 INSERTs are 61
    luminaires.
    """
    if layers is None:
        layers = classify_layers(entities, warnings)
    buckets = defaultdict(list)
    for ent in entities:
        if ent[0][1] != "INSERT":
            continue
        layer = first(ent, 8, "")
        if layer not in layers:
            continue
        building, floor, type_index = layers[layer]
        # Block geometry is in metres, so the INSERT scale gives the drawing
        # unit: 1000 for the usual millimetre export, 1 (or absent) for metres.
        scale = tuple(float(first(ent, c, "1") or 1) for c in (41, 42, 43))
        to_mm = 1000.0 / abs(scale[0]) if abs(scale[0]) > 1e-9 else 1.0
        x, y, z = (float(first(ent, c, "0")) * to_mm for c in (10, 20, 30))
        rot = float(first(ent, 50, "0") or 0)
        block = first(ent, 2, "")
        scale = tuple(v * to_mm for v in scale)
        key = (layer, quantize(x), quantize(y), quantize(z))
        buckets[key].append(
            {
                "layer": layer,
                "building": building,
                "floor": floor,
                "type_index": type_index,
                "block": block,
                "block_base": block_base(block),
                "x": x, "y": y, "z": z, "rotation": rot, "scale": scale,
                "to_mm": to_mm,
            }
        )

    fixtures = []
    for key, parts in sorted(buckets.items()):
        parts.sort(key=lambda p: p["block"])
        head = dict(parts[0])
        head["part_blocks"] = [p["block"] for p in parts]
        head["size_mm"] = fixture_size(parts, block_sizes)

        rotations = {round(p["rotation"], 3) for p in parts}
        if len(rotations) > 1:
            warn(warnings, "Warning", "PART_ROTATION_MISMATCH",
                 f"{head['layer']}: blocks at ({head['x']:.1f}, {head['y']:.1f}, "
                 f"{head['z']:.1f}) disagree on rotation "
                 f"({', '.join(f'{r:g}' for r in sorted(rotations))}); "
                 f"using {head['rotation']:.2f}.")
        bases = {p["block_base"] for p in parts}
        if len(bases) > 1:
            warn(warnings, "Warning", "PART_PRODUCT_MISMATCH",
                 f"{head['layer']}: blocks at ({head['x']:.1f}, {head['y']:.1f}, "
                 f"{head['z']:.1f}) mix products ({', '.join(sorted(bases))}).")
        fixtures.append(head)
    return fixtures


def fixture_size(parts, block_sizes):
    """
    Overall size of a luminaire in millimetres.

    A luminaire split across several blocks is measured by the largest of them
    in each axis, since the housing is what defines the fixture footprint.
    """
    if not block_sizes:
        return None
    dims = [0.0, 0.0, 0.0]
    found = False
    for part in parts:
        box = block_sizes.get(part["block"])
        if not box:
            continue
        found = True
        for i in range(3):
            dims[i] = max(dims[i], box[i] * part["scale"][i])
    return tuple(round(d, 1) for d in dims) if found else None


def cross_check_labels(entities, fixtures, warnings):
    """
    The LUMKEY_IDX texts carry the type number next to each luminaire. They are
    an independent witness to the fixture count, so disagreement means the read
    is wrong somewhere.
    """
    to_mm = fixtures[0]["to_mm"] if fixtures else 1.0
    labels = []
    for ent in entities:
        if ent[0][1] != "TEXT":
            continue
        if not RE_IDX.match(first(ent, 8, "")):
            continue
        labels.append(
            {
                "value": (first(ent, 1, "") or "").strip(),
                "x": float(first(ent, 10, "0")) * to_mm,
                "y": float(first(ent, 20, "0")) * to_mm,
            }
        )
    if not labels:
        return 0, 0

    matched = mismatched = 0
    for label in labels:
        best, best_d = None, float("inf")
        for f in fixtures:
            d = math.hypot(f["x"] - label["x"], f["y"] - label["y"])
            if d < best_d:
                best, best_d = f, d
        if best is None or best_d > LABEL_SEARCH_RADIUS_MM:
            warn(warnings, "Warning", "LABEL_ORPHAN",
                 f"Index label '{label['value']}' at ({label['x']:.1f}, "
                 f"{label['y']:.1f}) has no luminaire within "
                 f"{LABEL_SEARCH_RADIUS_MM:.0f} mm.")
            continue
        if label["value"].isdigit() and int(label["value"]) == best["type_index"]:
            matched += 1
        else:
            mismatched += 1
            warn(warnings, "Warning", "LABEL_MISMATCH",
                 f"Index label '{label['value']}' sits beside a luminaire on "
                 f"{best['layer']} (expected {best['type_index']}).")
    return matched, mismatched


def build_groups(fixtures, warnings):
    """
    Placement groups are keyed by (building, floor, type, Z).

    Z is never merged. A single product type may legitimately hang at two
    heights, and collapsing that would place luminaires at a height nobody
    asked for -- so each distinct Z becomes its own row and raises a warning.
    """
    groups = defaultdict(list)
    for f in fixtures:
        groups[(f["building"], f["floor"], f["type_index"], quantize(f["z"]))].append(f)

    by_type = defaultdict(set)
    for (bld, flr, idx, qz) in groups:
        by_type[(bld, flr, idx)].add(qz)
    for (bld, flr, idx), zs in sorted(by_type.items()):
        if len(zs) > 1:
            heights = ", ".join(f"{z * POSITION_QUANTUM_MM:.0f}" for z in sorted(zs))
            warn(warnings, "Warning", "MULTIPLE_MOUNTING_HEIGHTS",
                 f"BLD{bld}_FL{flr} type {idx}: {len(zs)} mounting heights on "
                 f"one layer ({heights} mm). Kept separate -- each needs its "
                 f"own level and offset.")

    out = []
    for key in sorted(groups):
        bld, flr, idx, qz = key
        items = groups[key]
        out.append(
            {
                "building": bld, "floor": flr, "type_index": idx,
                "z_mm": qz * POSITION_QUANTUM_MM,
                "count": len(items),
                "layer": items[0]["layer"],
                "block_base": items[0]["block_base"],
                "rotations": sorted({round(i["rotation"], 2) for i in items}),
                "size_mm": items[0].get("size_mm"),
            }
        )
    return out


# --------------------------------------------------------------------------

def probe(path):
    pairs = read_pairs(path)
    entities = split_entities(section(pairs, "ENTITIES"))
    warnings = []

    head = section(pairs, "HEADER")
    header = {}
    for i, (c, v) in enumerate(head):
        if c == 9 and i + 1 < len(head) and v not in header:
            header[v] = head[i + 1][1]
    insunits = header.get("$INSUNITS")
    if insunits is not None and insunits != "4":
        warn(warnings, "Info", "INSUNITS_NOT_MM",
             f"$INSUNITS={insunits} but DIALux writes millimetres. Coordinates "
             f"are read as millimetres; never let a CAD import auto-detect "
             f"units for this file.")

    tables = [e for e in entities if e[0][1] == "ACAD_TABLE"]
    title, types = "", {}
    for t in tables:
        grid = parse_table(t)
        if grid:
            title, types = parse_luminaire_list(grid)
            break
        warn(warnings, "Warning", "TABLE_UNREADABLE",
             "the luminaire list table could not be read; types will have no "
             "description.")
    building_name, storey_name = split_title(title)

    block_sizes = read_block_sizes(pairs)
    layers = classify_layers(entities, warnings)
    fixtures = collect_fixtures(entities, warnings, block_sizes, layers)
    groups = build_groups(fixtures, warnings)
    matched, mismatched = cross_check_labels(entities, fixtures, warnings)

    # Quantity in the luminaire list must equal the deduplicated fixture count.
    per_type = Counter(f["type_index"] for f in fixtures)

    # A type drawn but not listed, or listed but not drawn, means the user would
    # be mapping something they cannot see the description of -- or mapping a
    # row that places nothing.
    for idx in sorted(per_type):
        if idx not in types:
            warn(warnings, "Warning", "TYPE_NOT_IN_LIST",
                 f"type {idx} is drawn but has no luminaire list row; it will "
                 f"show no description.")
    for idx in sorted(types):
        if idx not in per_type:
            warn(warnings, "Warning", "TYPE_NOT_DRAWN",
                 f"type {idx} is listed but no luminaire of it was found in the "
                 f"geometry.")

    for idx, row in sorted(types.items()):
        if not row.get("Product"):
            warn(warnings, "Warning", "TYPE_UNNAMED",
                 f"type {idx}: the luminaire list carries no product name.")
        declared = row.get("Quantity", "").strip()
        if declared.isdigit() and int(declared) != per_type.get(idx, 0):
            warn(warnings, "Error", "QUANTITY_MISMATCH",
                 f"type {idx}: the luminaire list declares {declared} but the "
                 f"geometry yields {per_type.get(idx, 0)}.")

    inserts = sum(1 for e in entities if e[0][1] == "INSERT"
                  and first(e, 8, "") in layers)
    return {
        "source": path,
        "building": building_name,
        "storey": storey_name,
        "insert_count": inserts,
        "fixture_count": len(fixtures),
        "label_matched": matched,
        "label_mismatched": mismatched,
        "types": types,
        "groups": groups,
        "fixtures": fixtures,
        "warnings": warnings,
    }


def report(r):
    print(f"File      : {r['source']}")
    print(f"Building  : {r['building'] or '(unknown)'}")
    print(f"Storey    : {r['storey'] or '(unknown)'}")
    print(f"INSERTs   : {r['insert_count']}  ->  {r['fixture_count']} luminaires "
          f"after deduplication")
    print(f"Labels    : {r['label_matched']} matched, {r['label_mismatched']} mismatched")

    print("\nLuminaire list")
    for idx, row in sorted(r["types"].items()):
        print(f"  [{idx}] qty {row.get('Quantity',''):>3}  {row.get('Description','')}")

    print("\nPlacement groups  (building, floor, type, Z)")
    print(f"  {'BLD':>3} {'FL':>3} {'TYPE':>4} {'Z (mm)':>8} {'QTY':>4}  "
          f"{'BLOCK':<12} {'SIZE (mm)':<18} ROTATIONS")
    for g in r["groups"]:
        rots = ", ".join(f"{x:g}" for x in g["rotations"])
        size = g.get("size_mm")
        size_text = f"{size[0]:.0f} x {size[1]:.0f} x {size[2]:.0f}" if size else "-"
        print(f"  {g['building']:>3} {g['floor']:>3} {g['type_index']:>4} "
              f"{g['z_mm']:>8.1f} {g['count']:>4}  {g['block_base']:<12} "
              f"{size_text:<18} {rots}")

    if r["warnings"]:
        print(f"\nWarnings ({len(r['warnings'])})")
        for w in r["warnings"]:
            print(f"  {w['severity'].upper():<7} [{w['code']}] {w['message']}")
    else:
        print("\nNo warnings.")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("dxf")
    ap.add_argument("--json", help="also write the full result as JSON")
    args = ap.parse_args()
    r = probe(args.dxf)
    report(r)
    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(r, fh, indent=2)
        print(f"\nJSON written to {args.json}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
