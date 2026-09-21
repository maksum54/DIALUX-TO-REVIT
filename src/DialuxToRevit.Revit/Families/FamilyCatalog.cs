using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace DialuxToRevit.Revit.Families
{
    /// <summary>One lighting fixture type available in the project.</summary>
    public sealed class FamilyTypeEntry
    {
        public FamilyTypeEntry(FamilySymbol symbol)
        {
            Symbol = symbol;
            FamilyName = symbol.FamilyName;
            TypeName = symbol.Name;
        }

        public FamilySymbol Symbol { get; }

        public string FamilyName { get; }

        public string TypeName { get; }

        public string DisplayName => FamilyName + " : " + TypeName;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// The lighting fixture family types loaded in the project.
    ///
    /// Names come from Revit and are never invented: the user picks from what
    /// the project actually has, and a mapping that names a missing family is
    /// reported rather than substituted.
    /// </summary>
    public sealed class FamilyCatalog
    {
        private readonly List<FamilyTypeEntry> _entries;

        private FamilyCatalog(List<FamilyTypeEntry> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<FamilyTypeEntry> Entries => _entries;

        public static FamilyCatalog Load(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<FamilyTypeEntry> entries = new FilteredElementCollector(document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .Cast<FamilySymbol>()
                .Select(symbol => new FamilyTypeEntry(symbol))
                .OrderBy(entry => entry.FamilyName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.TypeName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new FamilyCatalog(entries);
        }

        public FamilyTypeEntry Find(string familyName, string typeName)
        {
            if (string.IsNullOrEmpty(familyName) || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            return _entries.FirstOrDefault(entry =>
                string.Equals(entry.FamilyName, familyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.TypeName, typeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
