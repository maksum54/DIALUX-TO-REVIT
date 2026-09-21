using System;
using System.Linq;
using Autodesk.Revit.DB;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Geometry;

namespace DialuxToRevit.Revit.Families
{
    /// <summary>
    /// Suggests a family type by comparing its footprint with the size measured
    /// from the DIALux block.
    ///
    /// Only ever a suggestion. Some DIALux products ship a 100 x 100 placeholder
    /// box instead of real dimensions, and a placeholder must not be allowed to
    /// pick a family, so a size that looks like one suggests nothing at all.
    /// </summary>
    public static class FamilySuggester
    {
        /// <summary>Sizes at or below this in plan are treated as placeholders.</summary>
        public const double PlaceholderPlanSizeMillimetres = 100.0;

        /// <summary>How far a footprint may differ and still be offered.</summary>
        public const double MatchToleranceMillimetres = 50.0;

        /// <summary>Beyond this difference, a chosen family is worth warning about.</summary>
        public const double WarnToleranceMillimetres = 200.0;

        public static FamilyTypeEntry Suggest(FamilyCatalog catalog, BlockSize size)
        {
            if (catalog == null || size.IsEmpty || IsPlaceholder(size))
            {
                return null;
            }

            FamilyTypeEntry best = null;
            double bestDifference = double.MaxValue;

            foreach (FamilyTypeEntry entry in catalog.Entries)
            {
                double? footprint = MeasurePlanSize(entry.Symbol);
                if (!footprint.HasValue)
                {
                    continue;
                }

                double difference = Math.Abs(footprint.Value - size.LongestPlanSide);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    best = entry;
                }
            }

            return bestDifference <= MatchToleranceMillimetres ? best : null;
        }

        public static bool IsPlaceholder(BlockSize size)
        {
            return size.LongestPlanSide <= PlaceholderPlanSizeMillimetres;
        }

        /// <summary>
        /// Longest plan dimension of a family type's bounding box, in
        /// millimetres, or null when the type has no usable geometry.
        /// </summary>
        public static double? MeasurePlanSize(FamilySymbol symbol)
        {
            if (symbol == null)
            {
                return null;
            }

            try
            {
                BoundingBoxXYZ box = symbol.get_BoundingBox(null);
                if (box == null)
                {
                    return null;
                }

                double width = Units.FeetToMillimetres(box.Max.X - box.Min.X);
                double depth = Units.FeetToMillimetres(box.Max.Y - box.Min.Y);
                double longest = Math.Max(width, depth);

                return longest > 0.0 ? longest : (double?)null;
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // A family type that has never been placed may not report a box.
                // That is not an error; it just cannot be suggested from size.
                return null;
            }
        }

        /// <summary>
        /// Describes a size disagreement between the export and the chosen
        /// family, or null when they are close enough to say nothing.
        /// </summary>
        public static string DescribeMismatch(FamilySymbol symbol, BlockSize size)
        {
            if (symbol == null || size.IsEmpty || IsPlaceholder(size))
            {
                return null;
            }

            double? footprint = MeasurePlanSize(symbol);
            if (!footprint.HasValue)
            {
                return null;
            }

            double difference = Math.Abs(footprint.Value - size.LongestPlanSide);
            if (difference <= WarnToleranceMillimetres)
            {
                return null;
            }

            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                "DIALux reports {0:F0} mm across but {1} measures {2:F0} mm.",
                size.LongestPlanSide, symbol.Name, footprint.Value);
        }
    }
}
