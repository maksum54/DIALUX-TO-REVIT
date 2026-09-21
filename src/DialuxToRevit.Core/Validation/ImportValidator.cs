using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DialuxToRevit.Core.Dxf;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Core.Validation
{
    /// <summary>
    /// Cross-checks that make a bad read visible before anything is placed.
    ///
    /// A DIALux export states the fixture count three times over -- the
    /// geometry, the luminaire list quantity, and the index labels. Any
    /// disagreement between them means the file was misread, and it is far
    /// cheaper to surface that here than to find it in a panel schedule later.
    /// </summary>
    public static class ImportValidator
    {
        /// <summary>$INSUNITS code 4 is millimetres.</summary>
        private const string MillimetreUnitCode = "4";

        /// <summary>
        /// DIALux writes millimetre coordinates but declares $INSUNITS=1
        /// (inches) in the sample export. Importing such a file with
        /// auto-detected units scales the whole model by 25.4, so the unit is
        /// always forced rather than trusted.
        /// </summary>
        public static void CheckHeaderUnits(DxfDocument document, List<ImportWarning> warnings)
        {
            string units;
            if (!document.Header.TryGetValue("$INSUNITS", out units))
            {
                return;
            }

            if (!string.Equals(units, MillimetreUnitCode, StringComparison.Ordinal))
            {
                warnings.Add(new ImportWarning(
                    WarningSeverity.Info,
                    "INSUNITS_NOT_MM",
                    "$INSUNITS=" + units + " but DIALux writes millimetres. " +
                    "Coordinates are read as millimetres; never let a CAD import auto-detect units for this file."));
            }
        }

        /// <summary>
        /// One layer may hold more than one mounting height. That is legitimate
        /// -- the same product hung at two heights -- but it must never be
        /// averaged away, so each height stays its own group and the user is
        /// told to give it its own level and offset.
        /// </summary>
        public static void CheckMountingHeights(DialuxImportResult result)
        {
            var byType = result.Groups
                .GroupBy(g => new { g.Storey, g.TypeIndex })
                .Where(g => g.Count() > 1);

            foreach (var group in byType)
            {
                string heights = string.Join(", ", group
                    .OrderBy(g => g.ZMillimetres)
                    .Select(g => g.ZMillimetres.ToString("F0", CultureInfo.InvariantCulture) + " mm")
                    .ToArray());

                result.Warnings.Add(new ImportWarning(
                    WarningSeverity.Warning,
                    "MULTIPLE_MOUNTING_HEIGHTS",
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} type {1}: {2} mounting heights on one layer ({3}). " +
                        "Kept separate -- each needs its own level and offset.",
                        group.Key.Storey, group.Key.TypeIndex, group.Count(), heights)));
            }
        }

        /// <summary>
        /// The luminaire list quantity against the deduplicated geometry count.
        /// This is the check that catches a broken deduplication rule, so a
        /// mismatch is an error rather than a warning.
        /// </summary>
        public static void CheckQuantities(DialuxImportResult result)
        {
            foreach (LuminaireType type in result.Types)
            {
                if (!type.Quantity.HasValue)
                {
                    continue;
                }

                int actual = result.Instances.Count(
                    i => i.Storey.Equals(type.Storey) && i.TypeIndex == type.Index);

                if (actual != type.Quantity.Value)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Error,
                        "QUANTITY_MISMATCH",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} type {1}: the luminaire list declares {2} but the geometry yields {3}.",
                            type.Storey, type.Index, type.Quantity.Value, actual)));
                }
            }

            // Types drawn in the geometry but absent from the list, and the
            // reverse. Either way the user would be mapping something blind.
            var geometryTypes = result.Instances
                .Select(i => new { i.Storey, i.TypeIndex })
                .Distinct();

            foreach (var key in geometryTypes)
            {
                if (result.FindType(key.Storey, key.TypeIndex) == null)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "TYPE_NOT_IN_LIST",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} type {1} is drawn but has no luminaire list row; it will show no description.",
                            key.Storey, key.TypeIndex)));
                }
            }

            foreach (LuminaireType type in result.Types)
            {
                bool drawn = result.Instances.Any(
                    i => i.Storey.Equals(type.Storey) && i.TypeIndex == type.Index);

                if (!drawn)
                {
                    result.Warnings.Add(new ImportWarning(
                        WarningSeverity.Warning,
                        "TYPE_NOT_DRAWN",
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} type {1} is listed but no luminaire of it was found in the geometry.",
                            type.Storey, type.Index)));
                }
            }
        }
    }
}
