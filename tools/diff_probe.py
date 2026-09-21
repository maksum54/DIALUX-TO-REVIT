#!/usr/bin/env python3
"""
Reference implementation of the re-import matching rules.

Mirrors DialuxToRevit.Revit.Diff.DiffEngine pass for pass, so the matching can
be exercised without Revit. The passes run in a fixed order and the order is the
whole point: the aim is to keep the Revit ElementId wherever the same physical
luminaire still exists, because deleting and recreating one loses the circuit,
tags and schedule rows attached to it.

A luminaire is a dict: {"id": any, "block": str, "x": mm, "y": mm, "z": mm}.
"""

import math

# Beyond this a shifted luminaire is treated as a delete plus an add: something
# that moved across the room is more likely a different fixture than the same
# one relocated.
MOVE_THRESHOLD_MM = 2000.0

# How close two luminaires must be to count as occupying the same spot when
# deciding whether the product was swapped in place.
SAME_POSITION_TOLERANCE_MM = 50.0

# Position rounding inside the stamp key.
KEY_TOLERANCE_MM = 1.0


def make_key(block, x, y, z):
    """Identity of a luminaire, stable across re-imports while it has not moved."""
    def r(v):
        return int(math.floor(v / KEY_TOLERANCE_MM + 0.5)) if v >= 0 else -int(
            math.floor(-v / KEY_TOLERANCE_MM + 0.5))
    return "{0}|{1}|{2}|{3}".format(block or "", r(x), r(y), r(z))


def plan_distance(a, b):
    """Compared in plan only: elevation comes from the level the user chose,
    not from the export, so a level change must not read as everything moving."""
    return math.hypot(a["x"] - b["x"], a["y"] - b["y"])


def nearest(pool, item, radius, same_product):
    best, best_d = None, float("inf")
    for candidate in pool:
        if same_product and candidate["block"] != item["block"]:
            continue
        d = plan_distance(candidate, item)
        if d < best_d:
            best, best_d = candidate, d
    if best is None or best_d > radius:
        return None, 0.0
    return best, best_d


def compute(existing, incoming,
            move_threshold=MOVE_THRESHOLD_MM,
            same_position=SAME_POSITION_TOLERANCE_MM,
            replace_all=False):
    """Returns a list of {"action", "existing", "incoming", "distance"}."""
    entries = []

    if replace_all:
        entries += [{"action": "Delete", "existing": e, "incoming": None,
                     "distance": 0.0} for e in existing]
        entries += [{"action": "Add", "existing": None, "incoming": i,
                     "distance": 0.0} for i in incoming]
        return entries

    unclaimed = list(existing)
    remaining = list(incoming)

    # Pass 1: an identical key is the same luminaire, untouched.
    by_key = {}
    for element in unclaimed:
        by_key.setdefault(make_key(element["block"], element["x"], element["y"],
                                   element["z"]), element)

    matched = []
    for item in remaining:
        key = make_key(item["block"], item["x"], item["y"], item["z"])
        element = by_key.get(key)
        if element is not None and element in unclaimed:
            unclaimed.remove(element)
            matched.append(item)
            entries.append({"action": "Unchanged", "existing": element,
                            "incoming": item, "distance": 0.0})
    remaining = [i for i in remaining if i not in matched]

    # Pass 2: same spot, different product -- swap the type, keep the element.
    matched = []
    for item in remaining:
        element, distance = nearest(unclaimed, item, same_position, same_product=False)
        if element is None or element["block"] == item["block"]:
            continue
        unclaimed.remove(element)
        matched.append(item)
        entries.append({"action": "Retype", "existing": element,
                        "incoming": item, "distance": distance})
    remaining = [i for i in remaining if i not in matched]

    # Pass 3: same product nearby -- it shifted, so move it.
    matched = []
    for item in remaining:
        element, distance = nearest(unclaimed, item, move_threshold, same_product=True)
        if element is None:
            continue
        unclaimed.remove(element)
        matched.append(item)
        entries.append({"action": "Move", "existing": element,
                        "incoming": item, "distance": distance})
    remaining = [i for i in remaining if i not in matched]

    # Whatever is left is genuinely new or genuinely gone.
    entries += [{"action": "Add", "existing": None, "incoming": i,
                 "distance": 0.0} for i in remaining]
    entries += [{"action": "Delete", "existing": e, "incoming": None,
                 "distance": 0.0} for e in unclaimed]
    return entries


def counts(entries):
    result = {"Unchanged": 0, "Add": 0, "Delete": 0, "Move": 0, "Retype": 0}
    for entry in entries:
        result[entry["action"]] += 1
    return result
