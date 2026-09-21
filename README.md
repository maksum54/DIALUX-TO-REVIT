# DIALux to Revit

A Revit 2025 add-in that reads a DIALux Evo DXF export and places the luminaires
as Revit lighting fixture families, with the family type chosen by the user per
product type, and with re-imports diffed against what is already in the model.

## Status

| Phase | Scope | State |
|---|---|---|
| 1 | Read the DXF, deduplicate, parse the luminaire list, validate | done |
| 2 | Mapping UI and placement into Revit | done |
| 3 | Levels and offsets from Z, family suggestions from block size | done |
| 4 | The re-import diff | stamp written, diff not started |
| 5 | Two-point alignment | transform built, not yet wired to the UI |

Placement currently aligns origin to origin. The two-point and manual
transforms are written but nothing calls them yet -- the dialog does not ask for
reference points -- so a model whose origin differs from the export's needs the
export re-based in DIALux for now.

## Layout

```
src/DialuxToRevit.Core/   DXF reading rules. No Revit API reference, so it can
                          be tested without Revit and used from the add-in.
src/DialuxToRevit.Revit/  Placement, levels, family catalog, the storage stamp.
src/DialuxToRevit.Addin/  Ribbon, the import command, the WPF mapping dialog.
tools/dxf_probe.py        Reference implementation of the same rules, runnable
                          from the command line. Doubles as the oracle the C#
                          port is checked against.
tests/                    Self-contained tests, plus a pinned regression case.
docs/DXF-FINDINGS.md      What a real DIALux export actually contains.
docs/DESIGN.md            The design, including the phases not yet built.
```

## Building and installing

Needs Revit 2025 installed for the API assemblies, and the .NET 8 SDK.

```
dotnet build DialuxToRevit.sln -c Release
```

If Revit is not on the default path, point the build at it:

```
dotnet build DialuxToRevit.sln -c Release -p:RevitApiDir="D:\Revit 2025\"
```

Then edit `src/DialuxToRevit.Addin/DialuxToRevit.addin` so `<Assembly>` is the
absolute path to the built `DialuxToRevit.Addin.dll`, copy that manifest to
`%APPDATA%\Autodesk\Revit\Addins\2025\`, and restart Revit -- a manifest
change needs a full restart, not just closing the document.

`DialuxToRevit.Core` has no Revit reference, so it builds and its rules can be
exercised on a machine without Revit.

## Using it

The ribbon adds a **DIALux** tab with an **Import DXF** button. It asks for the
export, reads it, and shows a row per product type per mounting height with the
description from the luminaire list, the size measured from the block, and a
dropdown of the lighting fixture types loaded in the project. Family names come
from Revit; the add-in never invents one.

Choices can be saved as a preset and reloaded on the next revision. Presets are
keyed by DIALux block id and mounting height rather than by layer name, so they
survive a layer being renamed or the LUM numbering changing between revisions.

Every run appends a line to `DialuxToRevit-import-log.txt` beside the model,
recording the timestamp, batch, source file and counts.

## Reading an export

```bash
python3 tools/dxf_probe.py path/to/export.dxf
python3 tools/dxf_probe.py path/to/export.dxf --json out.json
```

Output for the reference export:

```
INSERTs   : 99  ->  61 luminaires after deduplication
Labels    : 61 matched, 0 mismatched

Placement groups  (building, floor, type, Z)
  BLD  FL TYPE   Z (mm)  QTY  BLOCK        ROTATIONS
    1   0    1   2400.0   21  41725_2      0, 270
    1   0    1   3000.0    1  41725_2      0
    1   0    2   3000.0   30  39794_2      0, 180, 269.73, 270
    1   0    3   3000.0    8  38388_2      0
    1   0    4   5000.0    1  39969_2      0.64
```

## Tests

```bash
python3 tests/test_synthetic.py    # no sample file needed
python3 tests/test_dxf_probe.py    # needs a sample export
```

The regression case needs a DIALux export that this repository does not ship,
because sample exports are project data and this repository is public. Supply
one as `tests/samples/FG_WAREHOUSE11222.dxf`, or point `DIALUX_SAMPLE_DXF` at
it. Without it the case reports SKIP instead of failing.

## Exporting from DIALux

In DIALux Evo, Export > DWG/DXF:

- Layer: enable **Luminaires** with **One layer per product type**, and enable
  **Luminaire list**
- Format: DXF
- Unit of measure: **Millimetre**

The export must be 3D. A 2D plan export writes the luminaire symbols as loose
lines instead of block references, which loses both the mounting height and the
one-entity-per-luminaire structure everything here relies on.
