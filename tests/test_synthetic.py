#!/usr/bin/env python3
"""
Self-contained tests for the DXF reading rules.

These build small DXF files in a temp directory, so they run on a fresh clone
with no sample export present. They cover the rules that a naive reader gets
wrong:

  * several INSERTs at one point are one luminaire, not several
  * a layer holding two mounting heights stays split
  * rotations are never snapped to 90 degrees
  * building and storey come from each layer, so multi-storey exports work
"""

import os
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "tools"))

from dxf_probe import probe  # noqa: E402


def write_dxf(path, inserts, insunits="4"):
    """Emit the smallest DXF the probe accepts: a header and some INSERTs."""
    out = ["0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", insunits,
           "0", "ENDSEC", "0", "SECTION", "2", "ENTITIES"]
    for layer, block, x, y, z, rotation in inserts:
        out += ["0", "INSERT", "8", layer, "2", block,
                "10", repr(x), "20", repr(y), "30", repr(z), "50", repr(rotation)]
    out += ["0", "ENDSEC", "0", "EOF"]
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")
    return path


class Failures(object):
    def __init__(self):
        self.items = []

    def check(self, label, actual, expected):
        if actual != expected:
            self.items.append("{0}: expected {1!r}, got {2!r}".format(label, expected, actual))

    def close(self, label, actual, expected, tolerance):
        if abs(actual - expected) > tolerance:
            self.items.append("{0}: expected {1}, got {2}".format(label, expected, actual))


def test_deduplicates_colocated_blocks(tmp, f):
    """A luminaire split into housing and optic blocks is still one luminaire."""
    path = write_dxf(os.path.join(tmp, "dedup.dxf"), [
        ("DLX_BLD1_FL0_LUM 2", "39794_2_0", 1000.0, 2000.0, 3000.0, 0.0),
        ("DLX_BLD1_FL0_LUM 2", "39794_2_1", 1000.0, 2000.0, 3000.0, 0.0),
        ("DLX_BLD1_FL0_LUM 2", "39794_2_0", 4000.0, 2000.0, 3000.0, 0.0),
        ("DLX_BLD1_FL0_LUM 2", "39794_2_1", 4000.0, 2000.0, 3000.0, 0.0),
    ])
    r = probe(path)
    f.check("dedup insert_count", r["insert_count"], 4)
    f.check("dedup fixture_count", r["fixture_count"], 2)
    f.check("dedup product block", r["fixtures"][0]["block_base"], "39794_2")
    f.check("dedup parts merged", sorted(r["fixtures"][0]["part_blocks"]),
            ["39794_2_0", "39794_2_1"])


def test_near_but_distinct_points_stay_separate(tmp, f):
    """Deduplication must not swallow two genuinely adjacent fixtures."""
    path = write_dxf(os.path.join(tmp, "near.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1001.0, 2000.0, 2400.0, 0.0),
    ])
    r = probe(path)
    f.check("adjacent fixtures kept", r["fixture_count"], 2)


def test_mounting_heights_stay_split(tmp, f):
    """One layer, two heights: two groups and a warning, never an average."""
    path = write_dxf(os.path.join(tmp, "heights.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 4000.0, 2000.0, 2400.0, 0.0),
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 7000.0, 2000.0, 3000.0, 0.0),
    ])
    r = probe(path)
    f.check("height groups", len(r["groups"]), 2)
    f.check("height group sizes", [g["count"] for g in r["groups"]], [2, 1])
    f.check("height group Z", [g["z_mm"] for g in r["groups"]], [2400.0, 3000.0])
    codes = {w["code"] for w in r["warnings"]}
    f.check("height warning raised", "MULTIPLE_MOUNTING_HEIGHTS" in codes, True)


def test_rotation_is_not_snapped(tmp, f):
    """Real exports contain rotations like 269.73; they must survive intact."""
    path = write_dxf(os.path.join(tmp, "rotation.dxf"), [
        ("DLX_BLD1_FL0_LUM 2", "39794_2_0", 1000.0, 2000.0, 3000.0, 269.73),
        ("DLX_BLD1_FL0_LUM 2", "39794_2_0", 4000.0, 2000.0, 3000.0, 0.64),
    ])
    r = probe(path)
    rotations = sorted(x["rotation"] for x in r["fixtures"])
    f.close("small rotation preserved", rotations[0], 0.64, 0.0001)
    f.close("large rotation preserved", rotations[1], 269.73, 0.0001)


def test_multi_storey_is_separated(tmp, f):
    """Building and storey are read per layer, so several storeys can coexist."""
    path = write_dxf(os.path.join(tmp, "storeys.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
        ("DLX_BLD1_FL1_LUM 1", "41725_2_0", 1000.0, 2000.0, 6400.0, 0.0),
        ("DLX_BLD2_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
    ])
    r = probe(path)
    f.check("storey groups", len(r["groups"]), 3)
    f.check("storey keys",
            [(g["building"], g["floor"]) for g in r["groups"]],
            [(1, 0), (1, 1), (2, 0)])


def test_non_luminaire_layers_ignored(tmp, f):
    """Anything outside the DLX luminaire layers is not a fixture."""
    path = write_dxf(os.path.join(tmp, "other.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
        ("0", "SomeTitleBlock", 1000.0, 2000.0, 0.0, 0.0),
        ("DLX_BLD1_FL0_LUMKEY", "TableThing", 5000.0, 2000.0, 0.0, 0.0),
    ])
    r = probe(path)
    f.check("only luminaires counted", r["fixture_count"], 1)
    f.check("only luminaire INSERTs counted", r["insert_count"], 1)


def test_insunits_warning(tmp, f):
    """DIALux declares inches while writing millimetres; that must be flagged."""
    path = write_dxf(os.path.join(tmp, "units.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
    ], insunits="1")
    r = probe(path)
    codes = {w["code"] for w in r["warnings"]}
    f.check("insunits warning raised", "INSUNITS_NOT_MM" in codes, True)

    clean = write_dxf(os.path.join(tmp, "units_mm.dxf"), [
        ("DLX_BLD1_FL0_LUM 1", "41725_2_0", 1000.0, 2000.0, 2400.0, 0.0),
    ], insunits="4")
    codes = {w["code"] for w in probe(clean)["warnings"]}
    f.check("no insunits warning when mm", "INSUNITS_NOT_MM" in codes, False)


def main():
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    failed = 0

    with tempfile.TemporaryDirectory() as tmp:
        for test in tests:
            f = Failures()
            try:
                test(tmp, f)
            except Exception as exc:  # noqa: BLE001 - surface the error as a failure
                f.items.append("raised {0}: {1}".format(type(exc).__name__, exc))

            if f.items:
                failed += 1
                print("FAIL {0}".format(test.__name__))
                for item in f.items:
                    print("  - {0}".format(item))
            else:
                print("PASS {0}".format(test.__name__))

    print("\n{0} passed, {1} failed".format(len(tests) - failed, failed))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
