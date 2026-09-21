using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using DialuxToRevit.Addin.Persistence;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Families;
using DialuxToRevit.Revit.Placement;

namespace DialuxToRevit.Addin.ViewModels
{
    /// <summary>Backs the mapping dialog.</summary>
    public sealed class MappingViewModel : INotifyPropertyChanged
    {
        private readonly FamilyCatalog _catalog;
        private string _statusMessage;

        public MappingViewModel(DialuxImportResult import, LuminairePlacer placer)
        {
            Import = import ?? throw new ArgumentNullException(nameof(import));
            _catalog = placer.Catalog;

            Rows = new ObservableCollection<MappingRowViewModel>(
                import.Groups.Select(group =>
                    new MappingRowViewModel(group, placer.Levels, placer.Catalog.Entries)));

            foreach (MappingRowViewModel row in Rows)
            {
                row.SuggestFamily(_catalog);
                row.PropertyChanged += (_, _) => RaiseCounts();
            }

            Warnings = new ObservableCollection<ImportWarning>(import.Warnings);

            LoadPresetCommand = new RelayCommand(LoadPreset);
            SavePresetCommand = new RelayCommand(SavePreset, () => Rows.Any(r => r.IsMapped));

            _statusMessage = _catalog.Entries.Count == 0
                ? "No lighting fixture families are loaded in this project. Load one, then reopen this dialog."
                : BuildSummary();
        }

        public DialuxImportResult Import { get; }

        public ObservableCollection<MappingRowViewModel> Rows { get; }

        public ObservableCollection<ImportWarning> Warnings { get; }

        public ICommand LoadPresetCommand { get; }

        public ICommand SavePresetCommand { get; }

        public string SourceSummary => string.Format(
            CultureInfo.CurrentCulture,
            "{0} - {1} INSERTs, {2} luminaires, {3} group(s)",
            Path.GetFileName(Import.SourceFile ?? string.Empty),
            Import.InsertCount,
            Import.FixtureCount,
            Import.Groups.Count);

        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Errors block placement: they mean the export was misread, and placing
        /// on a misread would put wrong luminaire counts into the model.
        /// </summary>
        public bool HasErrors => Import.HasErrors;

        public int MappedCount => Rows.Count(row => row.IsMapped);

        public int MappedFixtureCount => Rows.Where(row => row.IsMapped).Sum(row => row.Quantity);

        public bool CanPlace => !HasErrors && MappedCount > 0;

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                Raise();
            }
        }

        private string BuildSummary()
        {
            if (HasErrors)
            {
                return "The export could not be read consistently. Fix it in DIALux and re-export; nothing will be placed.";
            }

            if (MappedCount == 0)
            {
                return "Choose a family type for each row you want placed.";
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} of {1} group(s) mapped - {2} luminaires will be placed.",
                MappedCount, Rows.Count, MappedFixtureCount);
        }

        private void RaiseCounts()
        {
            Raise(nameof(MappedCount));
            Raise(nameof(MappedFixtureCount));
            Raise(nameof(CanPlace));
            StatusMessage = BuildSummary();
        }

        public Dictionary<string, FamilyMapping> BuildMappings()
        {
            Dictionary<string, FamilyMapping> mappings =
                new Dictionary<string, FamilyMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (MappingRowViewModel row in Rows.Where(r => r.IsMapped))
            {
                FamilyMapping mapping = row.ToMapping();
                mappings[mapping.Key] = mapping;
            }

            return mappings;
        }

        private void LoadPreset()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Load mapping preset",
                Filter = "Mapping preset (*.json)|*.json",
                InitialDirectory = MappingPresetStore.DefaultDirectory
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Dictionary<string, FamilyMapping> preset = MappingPresetStore.Load(dialog.FileName);
            if (preset.Count == 0)
            {
                StatusMessage = "That preset held no usable mappings.";
                return;
            }

            int applied = 0;
            foreach (MappingRowViewModel row in Rows)
            {
                string key = FamilyMapping.MakeKey(row.Group.ProductBlock, row.Group.ZMillimetres);
                if (preset.TryGetValue(key, out FamilyMapping mapping))
                {
                    row.Apply(mapping);
                    applied++;
                }
            }

            RaiseCounts();
            StatusMessage = applied == Rows.Count
                ? string.Format(CultureInfo.CurrentCulture, "Preset applied to all {0} row(s).", applied)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "Preset applied to {0} of {1} row(s); the rest are not in it.",
                    applied, Rows.Count);
        }

        private void SavePreset()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save mapping preset",
                Filter = "Mapping preset (*.json)|*.json",
                FileName = "dialux-mapping.json",
                InitialDirectory = MappingPresetStore.DefaultDirectory
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                MappingPresetStore.Save(dialog.FileName, BuildMappings().Values);
                StatusMessage = "Preset saved to " + dialog.FileName;
            }
            catch (IOException exception)
            {
                StatusMessage = "Could not save the preset: " + exception.Message;
            }
            catch (UnauthorizedAccessException exception)
            {
                StatusMessage = "Could not save the preset: " + exception.Message;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
