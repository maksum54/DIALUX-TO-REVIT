using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using DialuxToRevit.Revit.Geometry;
using DialuxToRevit.Revit.Storage;

namespace DialuxToRevit.Revit.Diff
{
    /// <summary>A luminaire already in the model that this add-in placed.</summary>
    public sealed class ExistingLuminaire
    {
        public ExistingLuminaire(FamilyInstance instance, StampData stamp)
        {
            Instance = instance;
            Stamp = stamp;

            if (instance.Location is LocationPoint location)
            {
                Point = location.Point;
                RotationRadians = location.Rotation;
            }
        }

        public FamilyInstance Instance { get; }

        public StampData Stamp { get; }

        public ElementId Id => Instance.Id;

        public XYZ Point { get; }

        public double RotationRadians { get; }

        public string Key => Stamp.Key;

        public string BlockId => Stamp.BlockId;

        /// <summary>
        /// Why this element should not be changed without the user knowing.
        /// Null when it is safe to delete or move.
        /// </summary>
        public string Caution { get; set; }
    }

    /// <summary>
    /// Finds the luminaires a previous import of the same export left behind.
    ///
    /// Matching is on file name rather than full path, so moving the export to a
    /// different folder between revisions does not orphan everything that was
    /// placed from it.
    /// </summary>
    public static class ExistingLuminaireIndex
    {
        public static List<ExistingLuminaire> Collect(Document document, string sourceFile)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string wanted = SafeFileName(sourceFile);
            List<ExistingLuminaire> found = new List<ExistingLuminaire>();

            IEnumerable<FamilyInstance> candidates = new FilteredElementCollector(document)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_LightingFixtures)
                .Cast<FamilyInstance>();

            foreach (FamilyInstance instance in candidates)
            {
                StampData stamp = DialuxStamp.Read(instance);
                if (stamp == null || string.IsNullOrEmpty(stamp.Key))
                {
                    continue;
                }

                if (!string.Equals(SafeFileName(stamp.SourceFile), wanted,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ExistingLuminaire existing = new ExistingLuminaire(instance, stamp);
                existing.Caution = DescribeCaution(instance);
                found.Add(existing);
            }

            return found;
        }

        /// <summary>
        /// What would be lost by deleting this element.
        ///
        /// A circuited fixture is the one that matters: Revit deletes it without
        /// warning, the panel schedule quietly changes, and nobody notices until
        /// the drawing is issued.
        /// </summary>
        private static string DescribeCaution(FamilyInstance instance)
        {
            List<string> reasons = new List<string>();

            try
            {
                MEPModel mep = instance.MEPModel;
                if (mep != null)
                {
                    ISet<ElectricalSystem> systems = mep.GetElectricalSystems();
                    if (systems != null && systems.Count > 0)
                    {
                        reasons.Add(string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            "wired into {0}",
                            string.Join(", ", systems.Select(s => s.Name).ToArray())));
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // A family without an electrical connector throws rather than
                // returning an empty set. That simply means nothing to lose.
            }

            if (instance.GroupId != null && instance.GroupId != ElementId.InvalidElementId)
            {
                reasons.Add("inside a model group");
            }

            return reasons.Count == 0
                ? null
                : string.Join("; ", reasons.ToArray());
        }

        private static string SafeFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path);
            }
            catch (ArgumentException)
            {
                return path;
            }
        }

        /// <summary>Distance between a placed element and an exported point, in millimetres.</summary>
        public static double DistanceMillimetres(ExistingLuminaire existing,
            double xMillimetres, double yMillimetres, double zMillimetres, CoordinateTransform transform)
        {
            if (existing?.Point == null)
            {
                return double.MaxValue;
            }

            XYZ target = transform.ToRevit(
                xMillimetres, yMillimetres, LengthUnits.MillimetresToFeet(zMillimetres));

            // Compared in plan only. Elevation comes from the level and offset
            // the user chose, not from the export, so a change of level would
            // otherwise read as every luminaire having moved.
            double dx = existing.Point.X - target.X;
            double dy = existing.Point.Y - target.Y;

            return LengthUnits.FeetToMillimetres(Math.Sqrt((dx * dx) + (dy * dy)));
        }
    }
}
