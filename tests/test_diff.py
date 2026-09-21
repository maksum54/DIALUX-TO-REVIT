#!/usr/bin/env python3
"""
Tests for the re-import matching rules.

These lock in the behaviour that protects Revit element identity. The costly
mistake is treating a luminaire that merely shifted as a delete plus an add:
the new element has a new ElementId, so the circuit, tags and schedule rows on
the old one are gone, and Revit reports none of it.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "tools"))

from diff_probe import compute, counts, make_key  # noqa: E402


def lum(block, x, y, z=3000.0, id=None):
    return {"id": id, "block": block, "x": float(x), "y": float(y), "z": float(z)}


class Failures(object):
    def __init__(self):
        self.items = []

    def check(self, label, actual, expected):
        if actual != expected:
            self.items.append("{0}: expected {1!r}, got {2!r}".format(label, expected, actual))


def test_unchanged_when_nothing_moved(f):
    existing = [lum("A", 0, 0, id=1), lum("A", 1000, 0, id=2)]
    incoming = [lum("A", 0, 0), lum("A", 1000, 0)]
    c = counts(compute(existing, incoming))
    f.check("unchanged", c["Unchanged"], 2)
    f.check("no churn", c["Add"] + c["Delete"] + c["Move"] + c["Retype"], 0)


def test_a_shift_is_a_move_not_a_replace(f):
    """The rule that protects circuits and tags."""
    existing = [lum("A", 0, 0, id=1)]
    incoming = [lum("A", 200, 0)]
    entries = compute(existing, incoming)
    c = counts(entries)
    f.check("moved", c["Move"], 1)
    f.check("not deleted", c["Delete"], 0)
    f.check("not added", c["Add"], 0)
    f.check("keeps the element", entries[0]["existing"]["id"], 1)
    f.check("distance", round(entries[0]["distance"]), 200)


def test_a_long_move_becomes_delete_plus_add(f):
    """Past the threshold it is more likely a different fixture."""
    existing = [lum("A", 0, 0, id=1)]
    incoming = [lum("A", 9000, 0)]
    c = counts(compute(existing, incoming))
    f.check("deleted", c["Delete"], 1)
    f.check("added", c["Add"], 1)
    f.check("not moved", c["Move"], 0)


def test_same_spot_different_product_is_a_retype(f):
    existing = [lum("A", 0, 0, id=1)]
    incoming = [lum("B", 0, 0)]
    entries = compute(existing, incoming)
    c = counts(entries)
    f.check("retyped", c["Retype"], 1)
    f.check("not deleted", c["Delete"], 0)
    f.check("keeps the element", entries[0]["existing"]["id"], 1)


def test_retype_is_preferred_over_move(f):
    """
    A swapped product sitting exactly where the old one was must not be matched
    to a like-for-like fixture further away, or one element is needlessly
    destroyed and another needlessly moved.
    """
    existing = [lum("A", 0, 0, id=1), lum("B", 1500, 0, id=2)]
    incoming = [lum("B", 0, 0)]
    entries = compute(existing, incoming)
    retype = [e for e in entries if e["action"] == "Retype"]
    f.check("one retype", len(retype), 1)
    f.check("retyped the one in place", retype[0]["existing"]["id"], 1)
    f.check("the far one is deleted",
            [e["existing"]["id"] for e in entries if e["action"] == "Delete"], [2])


def test_count_increase_keeps_the_originals(f):
    """More luminaires in the revision: the existing ones must not churn."""
    existing = [lum("A", 0, 0, id=1), lum("A", 1000, 0, id=2)]
    incoming = [lum("A", 0, 0), lum("A", 1000, 0), lum("A", 2000, 0)]
    c = counts(compute(existing, incoming))
    f.check("two unchanged", c["Unchanged"], 2)
    f.check("one added", c["Add"], 1)
    f.check("nothing deleted", c["Delete"], 0)
    f.check("nothing moved", c["Move"], 0)


def test_count_decrease_deletes_only_the_surplus(f):
    existing = [lum("A", 0, 0, id=1), lum("A", 1000, 0, id=2), lum("A", 2000, 0, id=3)]
    incoming = [lum("A", 0, 0), lum("A", 1000, 0)]
    entries = compute(existing, incoming)
    c = counts(entries)
    f.check("two unchanged", c["Unchanged"], 2)
    f.check("one deleted", c["Delete"], 1)
    f.check("deleted the surplus",
            [e["existing"]["id"] for e in entries if e["action"] == "Delete"], [3])


def test_first_import_is_all_additions(f):
    """An empty model is just a diff where everything is new."""
    incoming = [lum("A", 0, 0), lum("A", 1000, 0)]
    c = counts(compute([], incoming))
    f.check("all added", c["Add"], 2)
    f.check("nothing else", c["Delete"] + c["Move"] + c["Retype"] + c["Unchanged"], 0)


def test_replace_all_deletes_everything_first(f):
    existing = [lum("A", 0, 0, id=1)]
    incoming = [lum("A", 0, 0)]
    c = counts(compute(existing, incoming, replace_all=True))
    f.check("deleted", c["Delete"], 1)
    f.check("added", c["Add"], 1)
    f.check("nothing preserved", c["Unchanged"], 0)


def test_no_element_is_claimed_twice(f):
    """
    Two incoming luminaires near one existing element: only one may inherit it,
    the other has to be a genuine addition.
    """
    existing = [lum("A", 0, 0, id=1)]
    incoming = [lum("A", 100, 0), lum("A", 200, 0)]
    entries = compute(existing, incoming)
    claimed = [e["existing"]["id"] for e in entries if e["existing"] is not None]
    f.check("element claimed once", claimed, [1])
    c = counts(entries)
    f.check("one move", c["Move"], 1)
    f.check("one add", c["Add"], 1)


def test_key_rounds_to_a_millimetre(f):
    """DXF precision is far finer than a millimetre; that must not look like a move."""
    f.check("sub-millimetre noise ignored",
            make_key("A", 1000.04, 2000.0, 3000.0),
            make_key("A", 1000.0, 2000.0, 3000.0))
    f.check("a real millimetre counts",
            make_key("A", 1000.0, 2000.0, 3000.0) != make_key("A", 1002.0, 2000.0, 3000.0),
            True)


def test_negative_coordinates_round_symmetrically(f):
    """Real exports are all-negative in Y, so rounding must not be lopsided."""
    f.check("negative noise ignored",
            make_key("A", -5079.4, -998.2, 2400.0),
            make_key("A", -5079.4, -998.2, 2400.0))
    f.check("negative shift detected",
            make_key("A", -5079.0, 0, 0) != make_key("A", -5081.0, 0, 0),
            True)


def main():
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    failed = 0
    for test in tests:
        f = Failures()
        try:
            test(f)
        except Exception as exc:  # noqa: BLE001
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
