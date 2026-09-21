# Design

The add-in reads a DIALux Evo DXF export and places its luminaires into Revit
2025 as lighting fixture family instances, letting the user choose which family
type to use per DIALux product type, and diffing later re-imports against what
is already in the model.

`docs/DXF-FINDINGS.md` records what the export format actually contains and why
the reading rules are what they are. This document covers the shape of the add-in.

## Decisions

| Decision | Choice |
|---|---|
| Input format | DXF only |
| Storeys | Multi-building and multi-storey, read from each layer |
| Several mounting heights on one layer | Kept separate, with a warning; never merged |
| Family selection | Chosen by the user from the families loaded in the project |
| Revit version | 2025 (.NET 8) |

## Projects

```
DialuxToRevit.Core      netstandard2.0. Reading, deduplication, validation.
                        No Revit reference, so it is testable without Revit.
DialuxToRevit.Revit     net8.0-windows. Placement, the storage stamp, the diff.
DialuxToRevit.UI        net8.0-windows. WPF mapping dialog.
DialuxToRevit.Addin     net8.0-windows. Ribbon, IExternalCommand, the manifest.
```

Keeping `Core` free of the Revit API is the point of the split: the rules that
are most likely to be wrong are the parsing rules, and they can be exercised in
a test run that takes a second rather than by restarting Revit.

`tools/dxf_probe.py` implements the same rules and shares the same warning codes
and severities. It is how the numbers were established in the first place, and
it stays in the repository as the oracle the C# is checked against.

## Reading (built)

1. Read the ASCII DXF as strictly alternating code/value pairs.
2. Take every `INSERT` whose layer matches `DLX_BLD<n>_FL<n>_LUM <n>`.
3. Group them by layer and insertion point quantised to 0.1 mm. Each group is
   one luminaire; the product is the block name without its part suffix.
4. Read the `ACAD_TABLE` on the matching `..._LUMKEY` layer by walking its
   cells, giving manufacturer, product, flux, load and quantity per type.
5. Group luminaires by building, storey, type and mounting height. These groups
   are the rows the user maps.
6. Validate, and report anything inconsistent before placement.

Coordinates stay in millimetres throughout `Core`. Conversion to Revit's
internal feet happens once, at placement.

### Validation

A DIALux export states its fixture count three times: the geometry, the
luminaire list quantities, and the index labels drawn beside each luminaire. All
three are checked against each other. It is much cheaper to catch a misread here
than to find it in a panel schedule weeks later.

| Code | Severity | Meaning |
|---|---|---|
| `QUANTITY_MISMATCH` | Error | List quantity disagrees with the geometry |
| `MULTIPLE_MOUNTING_HEIGHTS` | Warning | One layer holds several heights |
| `LABEL_MISMATCH` | Warning | An index label contradicts its layer |
| `LABEL_ORPHAN` | Warning | An index label has no luminaire near it |
| `PART_ROTATION_MISMATCH` | Warning | Co-located blocks disagree on rotation |
| `PART_PRODUCT_MISMATCH` | Warning | Co-located blocks are different products |
| `TYPE_NOT_IN_LIST` | Warning | A type is drawn but not listed |
| `TYPE_NOT_DRAWN` | Warning | A type is listed but not drawn |
| `TYPE_UNNAMED` | Warning | A listed type has no product name |
| `TABLE_UNREADABLE` | Warning | The luminaire list could not be parsed |
| `INSUNITS_NOT_MM` | Info | The header disagrees with the real units |

An error blocks placement. Warnings are shown and can be accepted.

## Mapping UI (phase 2)

One row per placement group, keyed by storey, type and mounting height:

| Bld | Storey | Type | Qty | Z | Description | Size | Family : Type | Level | Offset |
|---|---|---|---|---|---|---|---|---|---|
| 1 | OFFICE GF | 1 | 21 | 2400 | PARAGON / RRDA170L12_65K - 1123 lm - 12.1 W | 100x100 | *dropdown* | GF | 2400 |
| 1 | OFFICE GF | 1 | 1 | 3000 | PARAGON / RRDA170L12_65K - 1123 lm - 12.1 W | 100x100 | *dropdown* | GF | 3000 |
| 1 | OFFICE GF | 2 | 30 | 3000 | PARAGON / PLPA40L-E/65 - 4286 lm - 41.3 W | 602x602 | *dropdown* | GF | 3000 |

- The family dropdown lists the `FamilySymbol`s of category
  `OST_LightingFixtures` already loaded in the project, with a Load Family
  button for when the right one is not there yet. Family names come from Revit;
  the add-in never invents them.
- Description comes from the luminaire list, so the user can see what a type is
  before choosing a family for it.
- Size comes from the block geometry and is advisory only. Two of the four types
  in the reference export carry a placeholder size, so it can suggest and warn
  but must never choose.
- Offset is pre-filled from Z and recomputed as `Z - Elevation(level)` when the
  level changes.
- Two rows for type 1 rather than one, because the export puts it at two
  heights. The warning says why.

Mappings are saved as JSON keyed by **block id** (`39794_2`), not by layer name.
The block id is DIALux's product identity and survives a layer being renamed or
the LUM numbering changing between revisions; the layer name does not.

## Placement (phase 2-3)

Per luminaire: activate the `FamilySymbol`, create a non-structural
`FamilyInstance` at the converted point on the chosen level, then rotate it
about the vertical axis through that point by the DXF rotation.

Alignment between the DIALux origin and the Revit project origin needs all three
of:

1. Origin to origin, for when they already share a system.
2. Two-point alignment: the user picks two reference points in Revit and gives
   the matching DXF coordinates. The most reliable option on real projects, and
   the one to reach for when the export's coordinates look unrelated to the
   model's.
3. Manual dX, dY and rotation.

Families start unhosted. Because Z is read from the export, unhosted placement
is already at the right elevation, so hosting to ceilings is a later refinement
rather than a prerequisite.

## Re-import (phase 4)

Every placed instance is stamped via Extensible Storage, which keeps the
information with the element without adding project parameters:

```
DLX_SourceFile   the export it came from
DLX_Storey       building and storey
DLX_BlockId      product, e.g. 39794_2
DLX_Key          hash(BlockId + X + Y + Z, rounded to 1 mm)
DLX_BatchId      the import that created it
```

On re-import, existing stamped instances are diffed against the new read:

| Case | Action |
|---|---|
| Key in both | Leave alone; update the type if the mapping changed |
| Key only in the export | Place |
| Key only in the model | Delete |
| Same product, moved less than the threshold | Move |
| Same position, different product | Change type in place |

Move and change-in-place matter more than they look. Deleting and recreating a
luminaire that merely shifted loses its `ElementId`, and with it the circuit,
tags and schedule rows attached to it. Revit deletes a circuited fixture without
complaint, and the damage to a panel schedule is silent.

The diff is shown before it is applied, with a count per case and an explicit
warning when something due to be deleted is circuited. A Replace All mode stays
available for when the export has changed wholesale.
