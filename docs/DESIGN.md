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
DialuxToRevit.Revit     net8.0-windows. Placement, levels, the family catalog,
                        coordinate transforms, the storage stamp, the log.
DialuxToRevit.Addin     net8.0-windows. Ribbon, IExternalCommand, the manifest,
                        and the WPF mapping dialog.
```

The dialog lives in the add-in rather than an assembly of its own: it is one
window used by one command, so a separate UI assembly would add a load-time
dependency and buy nothing.

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

## Mapping UI (built)

One row per placement group, keyed by storey, type and mounting height. The
reference export produces five rows:

| Storey | Type | Qty | Z | Description | Size | Family : type | Level | Offset | Rotate |
|---|---|---|---|---|---|---|---|---|---|
| BLD1_FL0 | 1 | 21 | 2400 | PARAGON / RRDA170L12_65K - 1123 lm - 12.1 W | 100 x 100 x 100 | *dropdown* | GF | 2400 | yes |
| BLD1_FL0 | 1 | 1 | 3000 | PARAGON / RRDA170L12_65K - 1123 lm - 12.1 W | 100 x 100 x 100 | *dropdown* | GF | 3000 | yes |
| BLD1_FL0 | 2 | 30 | 3000 | PARAGON / PLPA40L-E/65 - 4286 lm - 41.3 W | 602 x 602 x 100 | *dropdown* | GF | 3000 | yes |
| BLD1_FL0 | 3 | 8 | 3000 | Philips / DN393B LED22-840 ... - 2400 lm - 24 W | 177 x 177 x 100 | *dropdown* | GF | 3000 | yes |
| BLD1_FL0 | 4 | 1 | 5000 | PARAGON / PHBSS150L65 - 22500 lm - 146.3 W | 100 x 100 x 100 | *dropdown* | GF | 5000 | yes |

- The family dropdown lists the `FamilySymbol`s of category
  `OST_LightingFixtures` loaded in the project. Names come from Revit; the
  add-in never invents one, and a saved mapping naming a family the project no
  longer has is reported rather than substituted.
- Description comes from the luminaire list, so the user can see what a type is
  before choosing a family for it.
- Size is measured from the block geometry. It drives a suggestion and a
  mismatch warning, but never a choice: two of the four types above carry a
  100 x 100 placeholder rather than real dimensions, and a placeholder must not
  be allowed to pick a family. Sizes at or below 100 mm suggest nothing.
- Level is suggested as the highest one at or below the fixture, and Offset is
  recomputed as `Z - Elevation(level)` whenever the level changes, so a wrong
  guess shows up as an absurd offset rather than hiding.
- Type 1 gets two rows rather than one because the export puts it at two
  heights. The warning list says why.
- Rows left unmapped are skipped, not guessed at. An `Error` warning disables
  placement entirely, because an export that was misread would put wrong
  luminaire counts into the model.

Mappings are saved as JSON keyed by **block id** (`39794_2`), not by layer name.
The block id is DIALux's product identity and survives a layer being renamed or
the LUM numbering changing between revisions; the layer name does not.

## Placement (built)

Per luminaire: activate the `FamilySymbol`, create a non-structural
`FamilyInstance` at the converted point on the chosen level, then rotate it
about the vertical axis through that point by the DXF rotation.

Alignment between the DIALux origin and the Revit project origin needs all three
of:

1. Origin to origin, for when they already share a system. **This is what the
   command currently uses.**
2. Two-point alignment: the user picks two reference points in Revit and gives
   the matching DXF coordinates. The most reliable option on real projects, and
   the one to reach for when the export's coordinates look unrelated to the
   model's. `CoordinateTransform.TwoPoint` is written, along with a scale check
   that catches a mis-picked point, but the dialog does not yet collect the
   points.
3. Manual dX, dY and rotation. Also written, also not yet collected.

Whatever the alignment, its plan rotation is added to each fixture's own, so a
rotated alignment keeps luminaires pointing the way they do in DIALux instead of
all facing the model's north.

Families start unhosted. Because Z is read from the export, unhosted placement
is already at the right elevation, so hosting to ceilings is a later refinement
rather than a prerequisite.

## Re-import (built)

Every placed instance is stamped via Extensible Storage, which keeps the
information with the element without adding project parameters. The stamp is
written from the first release even though the diff comes later: without it,
luminaires placed today could never be matched against a future export and
would have to be deleted and replaced wholesale.

```
DLX_SourceFile   the export it came from
DLX_Storey       building and storey
DLX_BlockId      product, e.g. 39794_2
DLX_Key          hash(BlockId + X + Y + Z, rounded to 1 mm)
DLX_BatchId      the import that created it
```

On re-import, existing stamped instances are diffed against the new read.
Matching runs in four passes, and the order is the whole point:

| Pass | Test | Action |
|---|---|---|
| 1 | Identical stamp key | Unchanged |
| 2 | Same position, different product | Change type in place |
| 3 | Same product within the move threshold | Move |
| 4 | Anything left | Add, or Delete |

Pass 2 runs before pass 3 deliberately. A swapped product sitting exactly where
the old one was must not be matched to a like-for-like fixture further away, or
one element is needlessly destroyed and another needlessly moved.

Move and change-in-place matter more than they look. Deleting and recreating a
luminaire that merely shifted loses its `ElementId`, and with it the circuit,
tags and schedule rows attached to it. Revit deletes a circuited fixture without
complaint, and the damage to a panel schedule is silent.

Positions are compared in plan only. Elevation comes from the level and offset
the user chose, not from the export, so comparing in 3D would make a change of
level read as every luminaire having moved.

Matching within a pass is greedy nearest-first, and an element already claimed
cannot be claimed again. With luminaires on a regular grid a smarter assignment
would cost more than it is worth: a wrong pairing between two identical fixtures
a metre apart produces the same model either way.

Existing luminaires are found by the file *name* of the export rather than its
full path, so moving the DXF to another folder between revisions does not
orphan everything placed from it.

### Before anything changes

The diff is shown first, with a count per case and the luminaires that need
attention listed separately:

```
  12  Place        In the export but not yet in the model.
   3  Delete       In the model but no longer in the export.
   6  Move         Same luminaire, shifted. The element is kept, so its circuit and tags survive.
   1  Change type  Same position, different product. The type is swapped in place.
  45  Unchanged    Left alone.
```

A luminaire that is wired into a circuit, or that sits inside a model group, is
kept by default and reported in the summary. Deleting one takes a deliberate
tick. A Replace All mode stays available for when the export has changed
wholesale, at the cost of every circuit and tag on the old fixtures.

The confirmation is skipped only when the model holds nothing from this export
and nothing needs attention: there is no decision to make, and the mapping
dialog has already said how many will be placed.

A first import and a re-import take the same code path -- the diff of an empty
model is simply every luminaire as an addition -- so the two cannot drift
apart.

### Applying

Everything happens in one transaction, deletions first so that a luminaire being
replaced never briefly coexists with the one taking its place. A luminaire Revit
refuses is recorded as a failure and the rest still go through; an unexpected
error rolls the whole thing back rather than leaving a model nobody can tell
from a finished one.

Every run appends a line to an import log beside the model, with the timestamp,
the batch, the source file and a count per action.

## Verifying without Revit

`Core` has no Revit reference, so the reading rules are exercised directly.
The matching rules cannot be -- they speak in `Document` and `ElementId` -- so
`tools/diff_probe.py` mirrors the four passes and `tests/test_diff.py` pins the
behaviour that protects element identity. It proves the algorithm, not the C#
port of it; the compiler on CI is what checks the port.

## Building

The Revit API comes from NuGet reference packages, pinned to 2025.0.2. Building
against the earliest 2025 release keeps the add-in loadable on every Revit 2025
update, and it means the solution builds on a machine with no Revit installed --
including a CI runner. `-p:UseLocalRevitApi=true` switches to an installed copy.

The Revit assemblies must never reach the output: Revit loads its own, and a
second set beside the add-in causes assembly identity conflicts at run time. CI
fails the build if one appears.
