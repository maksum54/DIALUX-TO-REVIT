using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Families;
using DialuxToRevit.Revit.Placement;

namespace DialuxToRevit.Addin.ViewModels
{
    /// <summary>One row of the mapping grid: one product type at one height.</summary>
    public sealed class MappingRowViewModel : INotifyPropertyChanged
    {
        private readonly LevelResolver _levels;
        private FamilyTypeEntry _familyType;
        private Level _level;
        private double _offsetMillimetres;
        private bool _applyRotation = true;

        public MappingRowViewModel(PlacementGroup group, LevelResolver levels,
            IReadOnlyList<FamilyTypeEntry> familyTypes)
        {
            Group = group;
            _levels = levels;
            FamilyTypes = familyTypes;

            _level = levels.SuggestForHeight(group.ZMillimetres);
            _offsetMillimetres = LevelResolver.OffsetMillimetres(_level, group.ZMillimetres);
        }

        public PlacementGroup Group { get; }

        /// <summary>Family types loaded in the project. Names come from Revit only.</summary>
        public IReadOnlyList<FamilyTypeEntry> FamilyTypes { get; }

        public IReadOnlyList<Level> Levels => _levels.Levels;

        public string Storey => Group.Storey.ToString();

        public int TypeIndex => Group.TypeIndex;

        public int Quantity => Group.Count;

        public string MountingHeight =>
            Group.ZMillimetres.ToString("F0", CultureInfo.CurrentCulture);

        public string Description => Group.Description;

        public string Size => Group.Size.ToString();

        /// <summary>Distinct rotations in this group, so odd angles are visible up front.</summary>
        public string Rotations =>
            string.Join(", ", Group.DistinctRotations.Select(
                r => r.ToString("0.##", CultureInfo.CurrentCulture)));

        public FamilyTypeEntry FamilyType
        {
            get => _familyType;
            set
            {
                if (_familyType == value)
                {
                    return;
                }

                _familyType = value;
                Raise();
                Raise(nameof(IsMapped));
                Raise(nameof(SizeWarning));
                Raise(nameof(HasSizeWarning));
            }
        }

        public Level Level
        {
            get => _level;
            set
            {
                if (_level == value)
                {
                    return;
                }

                _level = value;

                // The offset follows the level so the fixture stays at the
                // height DIALux gave it; the user sees the consequence of the
                // level they picked rather than having to work it out.
                OffsetMillimetres = LevelResolver.OffsetMillimetres(_level, Group.ZMillimetres);
                Raise();
            }
        }

        public double OffsetMillimetres
        {
            get => _offsetMillimetres;
            set
            {
                if (Math.Abs(_offsetMillimetres - value) < 1e-6)
                {
                    return;
                }

                _offsetMillimetres = value;
                Raise();
            }
        }

        public bool ApplyRotation
        {
            get => _applyRotation;
            set
            {
                if (_applyRotation == value)
                {
                    return;
                }

                _applyRotation = value;
                Raise();
            }
        }

        public bool IsMapped => _familyType != null;

        /// <summary>
        /// Set when the chosen family is a very different size from what DIALux
        /// reports. Advisory: placeholder-sized products never trigger it.
        /// </summary>
        public string SizeWarning =>
            _familyType == null
                ? null
                : FamilySuggester.DescribeMismatch(_familyType.Symbol, Group.Size);

        public bool HasSizeWarning => !string.IsNullOrEmpty(SizeWarning);

        public FamilyMapping ToMapping()
        {
            return new FamilyMapping
            {
                BlockId = Group.ProductBlock,
                ZMillimetres = Group.ZMillimetres,
                FamilyName = _familyType?.FamilyName,
                TypeName = _familyType?.TypeName,
                LevelName = _level?.Name,
                OffsetMillimetres = _offsetMillimetres,
                ApplyRotation = _applyRotation
            };
        }

        /// <summary>Applies a saved mapping, ignoring anything this project no longer has.</summary>
        public void Apply(FamilyMapping mapping)
        {
            if (mapping == null)
            {
                return;
            }

            FamilyTypeEntry match = FamilyTypes.FirstOrDefault(entry =>
                string.Equals(entry.FamilyName, mapping.FamilyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.TypeName, mapping.TypeName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                FamilyType = match;
            }

            Level level = _levels.FindByName(mapping.LevelName);
            if (level != null)
            {
                Level = level;
                OffsetMillimetres = mapping.OffsetMillimetres;
            }

            ApplyRotation = mapping.ApplyRotation;
        }

        /// <summary>Offers a family whose footprint matches the exported size.</summary>
        public void SuggestFamily(FamilyCatalog catalog)
        {
            if (_familyType != null)
            {
                return;
            }

            FamilyTypeEntry suggestion = FamilySuggester.Suggest(catalog, Group.Size);
            if (suggestion != null)
            {
                FamilyType = suggestion;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
