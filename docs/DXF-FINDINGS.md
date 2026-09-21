# What a DIALux export actually contains

Everything below was measured from a real export: DIALux Evo 5.13.0.9626,
AutoCAD 2013 (AC1027) ASCII DXF, one storey of a warehouse project, 162
entities. The numbers are pinned as a regression case in
`tests/expected/FG_WAREHOUSE11222.json`.

## Entities that matter

| Entity | Count | Layer | Carries |
|---|---|---|---|
| `INSERT` | 99 | `DLX_BLD1_FL0_LUM 1..4` | Position, height, rotation, product |
| `TEXT` | 61 | `DLX_BLD1_FL0_LUMKEY_IDX` | The type number drawn beside each luminaire |
| `ACAD_TABLE` | 1 | `DLX_BLD1_FL0_LUMKEY` | The luminaire list, nine columns |

A single `INSERT`:

```
  8  DLX_BLD1_FL0_LUM 1     layer, encoding building / storey / type
  2  41725_2_0              block name: product, variant, part
 10  15890.3362751007       X in millimetres
 20  -5079.414129257202     Y in millimetres
 30  2400.0                 Z in millimetres -- the mounting height
 41  1000.0                 scale (block geometry is in metres)
 50  270.0000000000007      rotation about Z, in degrees
```

Nothing has to be reconstructed. Position, height, rotation and product identity
are all stated directly.

## 99 INSERTs are 61 luminaires

| Layer | INSERTs | Distinct points | Blocks | List quantity |
|---|---|---|---|---|
| LUM 1 | 22 | 22 | `41725_2_0` | 22 |
| LUM 2 | 60 | 30 | `39794_2_0`, `39794_2_1` | 30 |
| LUM 3 | 16 | 8 | `38388_2_0`, `38388_2_1` | 8 |
| LUM 4 | 1 | 1 | `39969_2_0` | 1 |
| | **99** | **61** | | **61** |

DIALux splits some luminaires into more than one block -- housing and optic --
drawn at an identical insertion point. Counting `INSERT` entities therefore
overstates the fixture count by 38 in this file alone.

The rule: group INSERTs by layer and insertion point, quantised to 0.1 mm. One
group is one luminaire. The product is the block name minus its trailing part
number, so `39794_2_0` and `39794_2_1` are both product `39794_2`.

Three independent statements of the count agree at 61 -- distinct positions,
luminaire list quantities, and index labels. All three are asserted on every
read; disagreement means the file was misread and is reported as an error
before anything is placed.

## Traps

**`$INSUNITS` is 1 (inches) while the coordinates are millimetres.** A CAD
import set to auto-detect units will scale the model by 25.4. Units are forced,
never detected.

**One layer can hold more than one mounting height.** `LUM 1` has 21 luminaires
at 2400 mm and one at 3000 mm. Mounting height is part of the grouping key, and
a layer carrying several heights raises `MULTIPLE_MOUNTING_HEIGHTS` rather than
being averaged or collapsed.

**Rotations are not multiples of 90.** The file contains 269.73 and 0.64
degrees. These are real installation angles, not floating point noise, and are
never snapped.

**The luminaire list table sits far from the geometry.** `$EXTMAX` X is 180226 mm
while the furthest luminaire is at 32393 mm, because the table is placed off to
the side. Any bounding box used for alignment must come from the luminaire
INSERTs alone, or it will be out by 148 metres.

**Empty table cells still occupy a slot.** The table declares 6 rows and 9
columns (codes 91 and 92) and emits exactly 54 `301 CELL_VALUE` records, but
only 42 of them hold text. Reading the text values in sequence shifts every
later column left and mislabels the whole table, so cells are walked by their
markers in row-major order instead.

**The product name is not always in the same column.** Three of the four types
put it under `Article name`; type 2 leaves that blank and puts `PLPA40L-E/65`
under `Item number`. Both columns are read, in that order.

| Type | Article name | Item number |
|---|---|---|
| 1 | `RRDA170L12_65K` | *(empty)* |
| 2 | *(empty)* | `PLPA40L-E/65` |
| 3 | `DN393B LED22-840 PSD D200 WH WH` | *(empty)* |
| 4 | `PHBSS150L65` | *(empty)* |

## Block geometry gives the real fixture size

Blocks are polyface meshes (`POLYLINE` with flag 70 = 64), stored in metres and
scaled by 1000 at insertion.

| Block | Size | Consistent with |
|---|---|---|
| `39794_2_*` | 602 x 602 x 100 mm | A 600 x 600 recessed panel |
| `38388_2_*` | 177 x 177 x 100 mm | A downlight with a 145 mm cutout |
| `41725_2_0` | 100 x 100 x 100 mm | A generic placeholder |
| `39969_2_0` | 100 x 100 x 100 mm | A generic placeholder |

Useful for suggesting a Revit family and for warning when the chosen family is a
very different size -- but only ever a suggestion, since two of the four types
carry a placeholder rather than real dimensions.

## A note on 2D exports

An earlier 2D plan export of the same project wrote each luminaire as loose
lines, arcs and text rather than a block reference, with no usable Z. Recovering
fixture positions from that needs geometric clustering, which is both slower and
far less reliable than reading an insertion point. The 3D export removes that
problem entirely, and is the only form supported here.
