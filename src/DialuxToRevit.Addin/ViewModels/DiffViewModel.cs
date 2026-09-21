using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using DialuxToRevit.Revit.Diff;

namespace DialuxToRevit.Addin.ViewModels
{
    /// <summary>Backs the confirmation shown before a re-import changes anything.</summary>
    public sealed class DiffViewModel : INotifyPropertyChanged
    {
        private readonly DiffOptions _options;

        public DiffViewModel(ImportDiff diff, DiffOptions options)
        {
            Diff = diff ?? throw new ArgumentNullException(nameof(diff));
            _options = options ?? new DiffOptions();

            Lines = new List<DiffLine>
            {
                new DiffLine("Place", diff.AddCount,
                    "In the export but not yet in the model."),
                new DiffLine("Delete", diff.DeleteCount,
                    "In the model but no longer in the export."),
                new DiffLine("Move", diff.MoveCount,
                    "Same luminaire, shifted. The element is kept, so its circuit and tags survive."),
                new DiffLine("Change type", diff.RetypeCount,
                    "Same position, different product. The type is swapped in place."),
                new DiffLine("Unchanged", diff.UnchangedCount,
                    "Left alone.")
            };

            Cautions = diff.Cautions
                .Select(entry => string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} element {1} - {2}", entry.Action, entry.ExistingId, entry.Caution))
                .ToList();

            SkippedGroups = diff.SkippedGroups;
        }

        public ImportDiff Diff { get; }

        public IReadOnlyList<DiffLine> Lines { get; }

        public IReadOnlyList<string> Cautions { get; }

        public IReadOnlyList<string> SkippedGroups { get; }

        public bool HasCautions => Cautions.Count > 0;

        public bool HasSkipped => SkippedGroups.Count > 0;

        public string CautionHeading => string.Format(
            CultureInfo.CurrentCulture,
            "{0} element(s) would lose a circuit or leave a group.", Cautions.Count);

        /// <summary>
        /// Off by default. Revit deletes a circuited fixture without complaint
        /// and the panel schedule changes silently, so removing one has to be a
        /// deliberate choice rather than a side effect of re-importing.
        /// </summary>
        public bool DeleteCircuited
        {
            get => _options.DeleteCircuited;
            set
            {
                if (_options.DeleteCircuited == value)
                {
                    return;
                }

                _options.DeleteCircuited = value;
                Raise();
                Raise(nameof(CautionAction));
            }
        }

        public string CautionAction => DeleteCircuited
            ? "These will be deleted."
            : "These will be kept and listed in the summary.";

        public string Headline => Diff.IsFirstImport
            ? string.Format(
                CultureInfo.CurrentCulture,
                "{0} luminaires will be placed.", Diff.AddCount)
            : string.Format(
                CultureInfo.CurrentCulture,
                "This model already holds luminaires from this export. {0} change(s) to apply.",
                Diff.AddCount + Diff.DeleteCount + Diff.MoveCount + Diff.RetypeCount);

        public bool HasChanges => Diff.HasChanges;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>One counted row of the diff summary.</summary>
    public sealed class DiffLine
    {
        public DiffLine(string action, int count, string explanation)
        {
            Action = action;
            Count = count;
            Explanation = explanation;
        }

        public string Action { get; }

        public int Count { get; }

        public string Explanation { get; }

        /// <summary>Rows with nothing in them are dimmed rather than hidden.</summary>
        public double Opacity => Count > 0 ? 1.0 : 0.45;
    }
}
