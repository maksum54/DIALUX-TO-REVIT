using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Revit.Diff
{
    /// <summary>What happens to one luminaire on a re-import.</summary>
    public enum DiffAction
    {
        /// <summary>Present in both, in the same place. Left alone.</summary>
        Unchanged,

        /// <summary>In the export but not the model. Placed.</summary>
        Add,

        /// <summary>In the model but no longer in the export. Deleted.</summary>
        Delete,

        /// <summary>Same product, shifted. Moved rather than replaced.</summary>
        Move,

        /// <summary>Same place, different product. Type swapped in place.</summary>
        Retype
    }

    /// <summary>One line of the diff.</summary>
    public sealed class DiffEntry
    {
        public DiffAction Action { get; set; }

        /// <summary>The luminaire from the export, for everything but Delete.</summary>
        public LuminaireInstance Incoming { get; set; }

        /// <summary>The element already in the model, for everything but Add.</summary>
        public ElementId ExistingId { get; set; }

        /// <summary>The group the incoming luminaire belongs to, for the mapping.</summary>
        public PlacementGroup Group { get; set; }

        /// <summary>How far a Move travels, in millimetres.</summary>
        public double DistanceMillimetres { get; set; }

        /// <summary>Set when deleting or changing this element would lose work.</summary>
        public string Caution { get; set; }

        public bool HasCaution => !string.IsNullOrEmpty(Caution);

        public string Describe()
        {
            string where = Incoming != null
                ? string.Format(CultureInfo.CurrentCulture,
                    "({0:F0}, {1:F0}, {2:F0})", Incoming.X, Incoming.Y, Incoming.Z)
                : "id " + ExistingId;

            return Action == DiffAction.Move
                ? string.Format(CultureInfo.CurrentCulture,
                    "{0} {1} by {2:F0} mm", Action, where, DistanceMillimetres)
                : string.Format(CultureInfo.CurrentCulture, "{0} {1}", Action, where);
        }
    }

    /// <summary>Thresholds for matching an export against what is already placed.</summary>
    public sealed class DiffOptions
    {
        /// <summary>
        /// How far a luminaire may have shifted and still count as the same one.
        ///
        /// Beyond this it is treated as a delete plus an add, because a fixture
        /// that moved across the room is more likely a different fixture than
        /// the same one relocated.
        /// </summary>
        public double MoveThresholdMillimetres { get; set; } = 2000.0;

        /// <summary>
        /// How close two luminaires must be to count as occupying the same spot
        /// when deciding whether a product was swapped in place.
        /// </summary>
        public double SamePositionToleranceMillimetres { get; set; } = 50.0;

        /// <summary>Delete everything from this export and place it again.</summary>
        public bool ReplaceAll { get; set; }

        /// <summary>
        /// Whether to delete luminaires that are wired into a circuit. Off by
        /// default: Revit removes a circuited fixture without complaint, and the
        /// damage to a panel schedule is silent.
        /// </summary>
        public bool DeleteCircuited { get; set; }
    }

    /// <summary>The whole diff, ready to be shown before anything is changed.</summary>
    public sealed class ImportDiff
    {
        public ImportDiff()
        {
            Entries = new List<DiffEntry>();
            SkippedGroups = new List<string>();
        }

        public List<DiffEntry> Entries { get; }

        /// <summary>Groups with no family chosen, so nothing was compared for them.</summary>
        public List<string> SkippedGroups { get; }

        public string SourceFile { get; set; }

        public int Count(DiffAction action) => Entries.Count(e => e.Action == action);

        public int AddCount => Count(DiffAction.Add);

        public int DeleteCount => Count(DiffAction.Delete);

        public int MoveCount => Count(DiffAction.Move);

        public int RetypeCount => Count(DiffAction.Retype);

        public int UnchangedCount => Count(DiffAction.Unchanged);

        /// <summary>Entries that would lose a circuit, a group or a tag.</summary>
        public IEnumerable<DiffEntry> Cautions => Entries.Where(e => e.HasCaution);

        public bool HasChanges => AddCount + DeleteCount + MoveCount + RetypeCount > 0;

        /// <summary>True when the model holds nothing from this export yet.</summary>
        public bool IsFirstImport =>
            DeleteCount == 0 && MoveCount == 0 && RetypeCount == 0 && UnchangedCount == 0;

        public string Summarise()
        {
            List<string> lines = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, "+ {0} to place", AddCount),
                string.Format(CultureInfo.CurrentCulture, "- {0} to delete", DeleteCount),
                string.Format(CultureInfo.CurrentCulture, "~ {0} to move", MoveCount),
                string.Format(CultureInfo.CurrentCulture, "* {0} to change type", RetypeCount),
                string.Format(CultureInfo.CurrentCulture, "= {0} unchanged", UnchangedCount)
            };

            int cautions = Cautions.Count();
            if (cautions > 0)
            {
                lines.Add(string.Empty);
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture, "{0} need attention before they change.", cautions));
            }

            if (SkippedGroups.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} group(s) have no family chosen and were left out entirely.",
                    SkippedGroups.Count));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
