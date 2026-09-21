#!/usr/bin/env python3
"""
Regression test for the DXF reading rules.

Pins the numbers that the parser must keep producing for a known DIALux export:
the 99 INSERTs that are really 61 luminaires, the split of one layer across two
mounting heights, and the non-orthogonal rotations that must not be snapped.

The sample DXF is project data and this repository is public, so the file is not
committed. Supply it with either:

    tests/samples/FG_WAREHOUSE11222.dxf
    DIALUX_SAMPLE_DXF=/path/to/FG_WAREHOUSE11222.dxf python3 tests/test_dxf_probe.py

Without it the test reports SKIP rather than failing, so a fresh clone stays green.
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(ROOT, "tools"))

from dxf_probe import probe  # noqa: E402

ROTATION_TOLERANCE = 0.01
POSITION_TOLERANCE_MM = 0.1


def locate_sample(name):
    override = os.environ.get("DIALUX_SAMPLE_DXF")
    if override and os.path.isfile(override):
        return override
    candidate = os.path.join(HERE, "samples", name + ".dxf")
    return candidate if os.path.isfile(candidate) else None


def check(failures, label, actual, expected):
    if actual != expected:
        failures.append("{0}: expected {1!r}, got {2!r}".format(label, expected, actual))


def check_close(failures, label, actual, expected, tolerance):
    if abs(actual - expected) > tolerance:
        failures.append("{0}: expected {1}, got {2}".format(label, expected, actual))


def run_case(expected_path):
    name = os.path.splitext(os.path.basename(expected_path))[0]
    sample = locate_sample(name)
    if sample is None:
        print("SKIP {0} -- sample DXF not present".format(name))
        return None

    with open(expected_path, "r", encoding="utf-8") as fh:
        expected = json.load(fh)

    result = probe(sample)
    failures = []

    check(failures, "insert_count", result["insert_count"], expected["insert_count"])
    check(failures, "fixture_count", result["fixture_count"], expected["fixture_count"])
    check(failures, "label_matched", result["label_matched"], expected["label_matched"])
    check(failures, "label_mismatched", result["label_mismatched"], expected["label_mismatched"])

    # Deduplication is the rule most likely to regress, and the luminaire list
    # is an independent witness to it, so assert against the list too.
    for index, quantity in sorted(expected["type_quantities"].items()):
        row = result["types"].get(int(index), {})
        declared = row.get("Quantity", "")
        check(failures, "type {0} declared quantity".format(index), declared, str(quantity))
        placed = sum(1 for f in result["fixtures"] if f["type_index"] == int(index))
        check(failures, "type {0} fixtures".format(index), placed, quantity)

    check(failures, "warning codes",
          sorted({w["code"] for w in result["warnings"]}),
          sorted(expected["warning_codes"]))

    check(failures, "group count", len(result["groups"]), len(expected["groups"]))
    for n, (got, want) in enumerate(zip(result["groups"], expected["groups"])):
        prefix = "group {0}".format(n)
        check(failures, prefix + " building", got["building"], want["building"])
        check(failures, prefix + " floor", got["floor"], want["floor"])
        check(failures, prefix + " type_index", got["type_index"], want["type_index"])
        check_close(failures, prefix + " z_mm", got["z_mm"], want["z_mm"], POSITION_TOLERANCE_MM)
        check(failures, prefix + " count", got["count"], want["count"])
        check(failures, prefix + " block_base", got["block_base"], want["block_base"])
        if "size_mm" in want:
            got_size = got.get("size_mm")
            check(failures, prefix + " size present", got_size is not None, True)
            if got_size is not None:
                for axis, (a, b) in enumerate(zip(got_size, want["size_mm"])):
                    check_close(failures, "{0} size axis {1}".format(prefix, axis),
                                a, b, POSITION_TOLERANCE_MM)
        check(failures, prefix + " rotation count",
              len(got["rotations"]), len(want["rotations"]))
        for a, b in zip(got["rotations"], want["rotations"]):
            check_close(failures, prefix + " rotation", a, b, ROTATION_TOLERANCE)

    if failures:
        print("FAIL {0}".format(name))
        for failure in failures:
            print("  - {0}".format(failure))
        return False

    print("PASS {0}  ({1} INSERTs -> {2} luminaires, {3} groups)".format(
        name, result["insert_count"], result["fixture_count"], len(result["groups"])))
    return True


def main():
    expected_dir = os.path.join(HERE, "expected")
    cases = sorted(
        os.path.join(expected_dir, f)
        for f in os.listdir(expected_dir)
        if f.endswith(".json")
    )
    if not cases:
        print("No expectation files found.")
        return 1

    results = [run_case(case) for case in cases]
    if any(r is False for r in results):
        return 1
    if all(r is None for r in results):
        print("\nAll cases skipped: no sample DXF available.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
