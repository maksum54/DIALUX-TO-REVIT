using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Families;
using DialuxToRevit.Revit.Geometry;
using DialuxToRevit.Revit.Storage;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>Creates the Revit family instances for an import.</summary>
    public sealed class LuminairePlacer
    {
        private readonly Document _document;
        private readonly FamilyCatalog _catalog;
        private readonly LevelResolver _levels;

        public LuminairePlacer(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _catalog = FamilyCatalog.Load(document);
            _levels = LevelResolver.Load(document);
        }

        /// <summary>
        /// Places every mapped group in one transaction.
        ///
        /// A luminaire Revit refuses is recorded as a failure and the rest still
        /// go in, because one awkward point should not cost the other sixty. An
        /// unexpected error is different: it means the run is no longer
        /// trustworthy, so the whole transaction is rolled back rather than
        /// leaving a layout nobody can tell apart from a complete one.
        /// </summary>
        public PlacementResult Place(DialuxImportResult import, PlacementOptions options)
        {
            if (import == null)
            {
                throw new ArgumentNullException(nameof(import));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            PlacementResult result = new PlacementResult
            {
                BatchId = options.BatchId,
                SourceFile = options.SourceFile,
                StartedAt = DateTime.Now
            };

            using (Transaction transaction = new Transaction(_document, "Import DIALux luminaires"))
            {
                transaction.Start();

                try
                {
                    foreach (PlacementGroup group in import.Groups)
                    {
                        PlaceGroup(group, options, result);
                    }
                }
                catch (Exception)
                {
                    transaction.RollBack();
                    throw;
                }

                transaction.Commit();
            }

            return result;
        }

        private void PlaceGroup(PlacementGroup group, PlacementOptions options, PlacementResult result)
        {
            string label = DescribeGroup(group);

            string key = FamilyMapping.MakeKey(group.ProductBlock, group.ZMillimetres);
            if (!options.Mappings.TryGetValue(key, out FamilyMapping mapping) || !mapping.IsMapped)
            {
                result.Skipped.Add(label + ": no family chosen.");
                return;
            }

            FamilyTypeEntry entry = _catalog.Find(mapping.FamilyName, mapping.TypeName);
            if (entry == null)
            {
                result.Skipped.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}: family type '{1} : {2}' is not loaded in this project.",
                    label, mapping.FamilyName, mapping.TypeName));
                return;
            }

            Level level = _levels.FindByName(mapping.LevelName);
            if (level == null)
            {
                result.Skipped.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    "{0}: level '{1}' does not exist in this project.", label, mapping.LevelName));
                return;
            }

            // Activating a symbol is a model change, so it belongs inside the
            // transaction and must happen before the first instance of it.
            if (!entry.Symbol.IsActive)
            {
                entry.Symbol.Activate();
                _document.Regenerate();
            }

            double elevationFeet = level.Elevation + Units.MillimetresToFeet(mapping.OffsetMillimetres);
            int placed = 0;

            foreach (LuminaireInstance instance in group.Instances)
            {
                try
                {
                    XYZ point = options.Transform.ToRevit(instance.X, instance.Y, elevationFeet);

                    FamilyInstance created = _document.Create.NewFamilyInstance(
                        point, entry.Symbol, level, StructuralType.NonStructural);

                    if (mapping.ApplyRotation)
                    {
                        ApplyRotation(created, point, instance.RotationDegrees, options.Transform);
                    }

                    DialuxStamp.Write(created, instance, options.SourceFile, options.BatchId);

                    result.PlacedIds.Add(created.Id);
                    placed++;
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException exception)
                {
                    result.Failures.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} at ({1:F0}, {2:F0}): {3}",
                        label, instance.X, instance.Y, exception.Message));
                }
            }

            if (placed > 0)
            {
                result.PerGroup[label] = placed;
            }
        }

        /// <summary>
        /// Turns the instance about the vertical axis through its own location.
        ///
        /// The plan rotation from the alignment is added to the fixture's own,
        /// so a rotated alignment keeps luminaires pointing the way they do in
        /// DIALux rather than all facing the model's north.
        /// </summary>
        private void ApplyRotation(FamilyInstance instance, XYZ point,
            double rotationDegrees, CoordinateTransform transform)
        {
            double radians = (rotationDegrees * Math.PI / 180.0) + transform.RotationRadians;

            // Revit rejects a rotation of zero, and a full turn is a no-op.
            double normalised = radians % (2.0 * Math.PI);
            if (Math.Abs(normalised) < 1e-9)
            {
                return;
            }

            using (Line axis = Line.CreateBound(point, point + XYZ.BasisZ))
            {
                ElementTransformUtils.RotateElement(_document, instance.Id, axis, normalised);
            }
        }

        private static string DescribeGroup(PlacementGroup group)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0} type {1} @ {2:F0} mm",
                group.Storey, group.TypeIndex, group.ZMillimetres);
        }

        /// <summary>Levels in the project, for the mapping dialog.</summary>
        public LevelResolver Levels => _levels;

        /// <summary>Lighting fixture types in the project, for the mapping dialog.</summary>
        public FamilyCatalog Catalog => _catalog;
    }
}
