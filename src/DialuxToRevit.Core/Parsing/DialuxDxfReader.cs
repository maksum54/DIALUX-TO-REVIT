using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DialuxToRevit.Core.Dxf;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Core.Validation;

namespace DialuxToRevit.Core.Parsing
{
    /// <summary>
    /// Reads a DIALux DXF export into the placement groups the add-in maps onto
    /// Revit family types.
    /// </summary>
    public static class DialuxDxfReader
    {
        /// <summary>
        /// Insertion points closer than this are the same luminaire. DIALux
        /// repeats coordinates of co-located blocks bit for bit, so this only
        /// has to absorb formatting noise -- it is deliberately far too small to
        /// merge two genuinely adjacent fixtures.
        /// </summary>
        public const double PositionQuantumMillimetres = 0.1;

        /// <summary>How far an index label may sit from the luminaire it annotates.</summary>
        public const double LabelSearchRadiusMillimetres = 500.0;

        /// <summary>Block names look like "39794_2_0": product, variant, part.</summary>
        private static readonly Regex BlockPartSuffix = new Regex(
            @"^(?<base>.*)_(?<part>\d+)$", RegexOptions.Compiled);

        public static DialuxImportResult Read(string path)
        {
            DxfDocument document = DxfDocument.Load(path);
            DialuxImportResult result = new DialuxImportResult();
            result.SourceFile = path;

            List<DxfEntity> entities = document.GetEntities("ENTITIES");
            Dictionary<string, double[]> blockExtents = BlockGeometryReader.ReadExtents(document);

            ImportValidator.CheckHeaderUnits(document, result.Warnings);
            ReadLuminaireLists(entities, result);
            ReadInstances(entities, blockExtents, result);
            BuildGroups(result);
            CrossCheckIndexLabels(entities, result);
            ImportValidator.CheckQuantities(result);

            return result;
        }

        private static void ReadLuminaireLists(List<DxfEntity> entities, DialuxImportResult result)
        {
            foreach (DxfEntity entity in entities)
            {
                if (!string.Equals(entity.Type, "ACAD_TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int building, floor;
                if (!DialuxLayerName.TryParseList(entity.Layer, out building, out floor))
                {
                    continue;
                }

                StoreyKey storey = new StoreyKey(building, floor);
                TableGrid grid = AcadTableReader.TryReadGrid(entity);
                if (grid == null)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "TABLE_UNREADABLE",
                        storey + ": the luminaire list table could not be read; " +
                        "types will have no description."));
                    continue;
                }

                string buildingName, storeyName;
                List<LuminaireType> types =
                    LuminaireListReader.Read(grid, storey, out buildingName, out storeyName);

                result.BuildingNames[storey] = buildingName;
                result.StoreyNames[storey] = storeyName;
                result.Types.AddRange(types);

                foreach (LuminaireType type in types)
                {
                    if (string.IsNullOrEmpty(type.Product))
                    {
                        result.Warnings.Add(new ImportWarning(
                            WarningSeverity.Warning,
                            "TYPE_UNNAMED",
                            storey + " type " + type.Index +
                            ": the luminaire list carries no product name."));
                    }
                }
            }
        }

        /// <summary>
        /// Folds INSERTs into physical luminaires.
        ///
        /// One luminaire is frequently exported as several INSERTs sharing an
        /// exact insertion point, because DIALux splits housing and optic into
        /// separate blocks. Counting INSERTs directly overstates the fixture
        /// count badly -- in the reference export, 99 INSERTs are 61 luminaires.
        /// </summary>
        private static void ReadInstances(List<DxfEntity> entities,
            Dictionary<string, double[]> blockExtents, DialuxImportResult result)
        {
            Dictionary<Tuple<string, long, long, long>, List<DxfEntity>> buckets =
                new Dictionary<Tuple<string, long, long, long>, List<DxfEntity>>();
            List<Tuple<string, long, long, long>> order =
                new List<Tuple<string, long, long, long>>();

            foreach (DxfEntity entity in entities)
            {
                if (!string.Equals(entity.Type, "INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int building, floor, typeIndex;
                if (!DialuxLayerName.TryParseLuminaire(entity.Layer, out building, out floor, out typeIndex))
                {
                    continue;
                }

                result.InsertCount++;

                Tuple<string, long, long, long> key = Tuple.Create(
                    entity.Layer,
                    Quantize(entity.GetDouble(10, 0.0)),
                    Quantize(entity.GetDouble(20, 0.0)),
                    Quantize(entity.GetDouble(30, 0.0)));

                List<DxfEntity> bucket;
                if (!buckets.TryGetValue(key, out bucket))
                {
                    bucket = new List<DxfEntity>();
                    buckets[key] = bucket;
                    order.Add(key);
                }

                bucket.Add(entity);
            }

            foreach (Tuple<string, long, long, long> key in order)
            {
                List<DxfEntity> parts = buckets[key];
                parts.Sort((a, b) => string.CompareOrdinal(a.GetString(2, string.Empty), b.GetString(2, string.Empty)));

                DxfEntity head = parts[0];
                int building, floor, typeIndex;
                DialuxLayerName.TryParseLuminaire(head.Layer, out building, out floor, out typeIndex);

                LuminaireInstance instance = new LuminaireInstance();
                instance.Layer = head.Layer;
                instance.Storey = new StoreyKey(building, floor);
                instance.TypeIndex = typeIndex;
                instance.X = head.GetDouble(10, 0.0);
                instance.Y = head.GetDouble(20, 0.0);
                instance.Z = head.GetDouble(30, 0.0);
                instance.RotationDegrees = head.GetDouble(50, 0.0);
                instance.ProductBlock = StripPartSuffix(head.GetString(2, string.Empty));
                instance.Size = MeasureFixture(parts, blockExtents);

                List<double> rotations = new List<double>();
                List<string> bases = new List<string>();

                foreach (DxfEntity part in parts)
                {
                    string block = part.GetString(2, string.Empty);
                    instance.PartBlocks.Add(block);

                    double rotation = Math.Round(part.GetDouble(50, 0.0), 3);
                    if (!rotations.Contains(rotation))
                    {
                        rotations.Add(rotation);
                    }

                    string baseName = StripPartSuffix(block);
                    if (!bases.Contains(baseName))
                    {
                        bases.Add(baseName);
                    }
                }

                if (rotations.Count > 1)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "PART_ROTATION_MISMATCH",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}: blocks at ({1:F1}, {2:F1}, {3:F1}) disagree on rotation ({4}); using {5:F2}.",
                            instance.Layer, instance.X, instance.Y, instance.Z,
                            string.Join(", ", rotations.Select(r => r.ToString("0.##", CultureInfo.InvariantCulture)).ToArray()),
                            instance.RotationDegrees)));
                }

                if (bases.Count > 1)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "PART_PRODUCT_MISMATCH",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}: blocks at ({1:F1}, {2:F1}, {3:F1}) mix products ({4}).",
                            instance.Layer, instance.X, instance.Y, instance.Z,
                            string.Join(", ", bases.ToArray()))));
                }

                result.Instances.Add(instance);
            }
        }

        /// <summary>
        /// Groups by storey, type and mounting height. Height is never merged --
        /// see <see cref="PlacementGroup"/>.
        /// </summary>
        private static void BuildGroups(DialuxImportResult result)
        {
            Dictionary<Tuple<int, int, int, long>, PlacementGroup> groups =
                new Dictionary<Tuple<int, int, int, long>, PlacementGroup>();

            foreach (LuminaireInstance instance in result.Instances)
            {
                Tuple<int, int, int, long> key = Tuple.Create(
                    instance.Storey.Building,
                    instance.Storey.Floor,
                    instance.TypeIndex,
                    Quantize(instance.Z));

                PlacementGroup group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new PlacementGroup();
                    group.Storey = instance.Storey;
                    group.TypeIndex = instance.TypeIndex;
                    group.ZMillimetres = instance.Z;
                    group.Layer = instance.Layer;
                    group.ProductBlock = instance.ProductBlock;
                    group.Size = instance.Size;
                    group.Type = result.FindType(instance.Storey, instance.TypeIndex);
                    groups[key] = group;
                }

                group.Instances.Add(instance);
            }

            result.Groups.AddRange(groups.Values
                .OrderBy(g => g.Storey.Building)
                .ThenBy(g => g.Storey.Floor)
                .ThenBy(g => g.TypeIndex)
                .ThenBy(g => g.ZMillimetres));

            ImportValidator.CheckMountingHeights(result);
        }

        /// <summary>
        /// The LUMKEY_IDX texts carry the type number beside each luminaire.
        /// They are an independent witness to both the count and the type
        /// assignment, so a disagreement means the read is wrong somewhere.
        /// </summary>
        private static void CrossCheckIndexLabels(List<DxfEntity> entities, DialuxImportResult result)
        {
            foreach (DxfEntity entity in entities)
            {
                if (!string.Equals(entity.Type, "TEXT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int building, floor;
                if (!DialuxLayerName.TryParseIndexLabel(entity.Layer, out building, out floor))
                {
                    continue;
                }

                StoreyKey storey = new StoreyKey(building, floor);
                string text = (entity.GetString(1, string.Empty) ?? string.Empty).Trim();
                double lx = entity.GetDouble(10, 0.0);
                double ly = entity.GetDouble(20, 0.0);

                LuminaireInstance nearest = null;
                double nearestDistance = double.MaxValue;

                foreach (LuminaireInstance instance in result.Instances)
                {
                    if (!instance.Storey.Equals(storey))
                    {
                        continue;
                    }

                    double dx = instance.X - lx;
                    double dy = instance.Y - ly;
                    double distance = Math.Sqrt((dx * dx) + (dy * dy));
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = instance;
                    }
                }

                if (nearest == null || nearestDistance > LabelSearchRadiusMillimetres)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "LABEL_ORPHAN",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Index label '{0}' at ({1:F1}, {2:F1}) has no luminaire within {3:F0} mm.",
                            text, lx, ly, LabelSearchRadiusMillimetres)));
                    continue;
                }

                int labelValue;
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out labelValue)
                    && labelValue == nearest.TypeIndex)
                {
                    result.LabelsMatched++;
                }
                else
                {
                    result.LabelsMismatched++;
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "LABEL_MISMATCH",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Index label '{0}' sits beside a luminaire on {1} (expected {2}).",
                            text, nearest.Layer, nearest.TypeIndex)));
                }
            }
        }

        /// <summary>
        /// Overall size of one luminaire in millimetres.
        ///
        /// A luminaire split across several blocks is measured by the largest of
        /// them on each axis, since the housing is what defines the footprint.
        /// Block geometry is in metres and the INSERT scale brings it to
        /// millimetres.
        /// </summary>
        private static BlockSize MeasureFixture(List<DxfEntity> parts,
            Dictionary<string, double[]> blockExtents)
        {
            if (blockExtents == null || blockExtents.Count == 0)
            {
                return new BlockSize();
            }

            double[] largest = new double[3];
            bool measured = false;

            foreach (DxfEntity part in parts)
            {
                double[] extent;
                if (!blockExtents.TryGetValue(part.GetString(2, string.Empty), out extent))
                {
                    continue;
                }

                measured = true;
                double[] scale =
                {
                    part.GetDouble(41, 1.0),
                    part.GetDouble(42, 1.0),
                    part.GetDouble(43, 1.0)
                };

                for (int axis = 0; axis < 3; axis++)
                {
                    double size = Math.Abs(extent[axis] * scale[axis]);
                    if (size > largest[axis])
                    {
                        largest[axis] = size;
                    }
                }
            }

            return measured
                ? new BlockSize(largest[0], largest[1], largest[2])
                : new BlockSize();
        }

        /// <summary>"39794_2_0" -> "39794_2". Names without a part suffix pass through.</summary>
        public static string StripPartSuffix(string blockName)
        {
            if (string.IsNullOrEmpty(blockName))
            {
                return string.Empty;
            }

            Match match = BlockPartSuffix.Match(blockName);
            return match.Success ? match.Groups["base"].Value : blockName;
        }

        private static long Quantize(double millimetres)
        {
            return (long)Math.Round(millimetres / PositionQuantumMillimetres, MidpointRounding.AwayFromZero);
        }
    }
}
