using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>What one import run did, for the summary and the audit log.</summary>
    public sealed class PlacementResult
    {
        public PlacementResult()
        {
            PlacedIds = new List<ElementId>();
            Skipped = new List<string>();
            Failures = new List<string>();
            PerGroup = new Dictionary<string, int>();
        }

        public string BatchId { get; set; }

        public string SourceFile { get; set; }

        public DateTime StartedAt { get; set; }

        public List<ElementId> PlacedIds { get; }

        /// <summary>Groups deliberately not placed, with the reason.</summary>
        public List<string> Skipped { get; }

        /// <summary>Luminaires that could not be placed, with the reason.</summary>
        public List<string> Failures { get; }

        public Dictionary<string, int> PerGroup { get; }

        public int PlacedCount => PlacedIds.Count;

        public bool Succeeded => Failures.Count == 0;

        public string Summarise()
        {
            List<string> lines = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, "Placed {0} luminaires.", PlacedCount)
            };

            foreach (KeyValuePair<string, int> group in PerGroup.OrderBy(p => p.Key))
            {
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture, "  {0}: {1}", group.Key, group.Value));
            }

            if (Skipped.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture, "Skipped {0} group(s):", Skipped.Count));
                lines.AddRange(Skipped.Select(s => "  " + s));
            }

            if (Failures.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture, "{0} failure(s):", Failures.Count));

                // Long failure lists help nobody in a dialog; the log has them all.
                lines.AddRange(Failures.Take(10).Select(f => "  " + f));
                if (Failures.Count > 10)
                {
                    lines.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "  ... and {0} more, see the import log.", Failures.Count - 10));
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
