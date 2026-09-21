using System;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Families;
using DialuxToRevit.Revit.Geometry;
using DialuxToRevit.Revit.Storage;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>Where a group's luminaires should go, once the mapping is resolved.</summary>
    public sealed class ResolvedTarget
    {
        public FamilyMapping Mapping { get; set; }

        public FamilyTypeEntry Entry { get; set; }

        public Level Level { get; set; }

        /// <summary>Absolute height of the fixtures, in Revit internal units.</summary>
        public double ElevationFeet { get; set; }

        /// <summary>Why the group cannot be placed, or null when it can.</summary>
        public string Problem { get; set; }

        public bool IsUsable => Problem == null;
    }

    /// <summary>
    /// Creates and updates the Revit family instances.
    ///
    /// Only ever one luminaire at a time; the transaction and the ordering
    /// belong to the caller, so that a first import and a re-import share one
    /// code path and cannot drift apart.
    /// </summary>
    public sealed class LuminairePlacer
    {
        private readonly Document _document;

        public LuminairePlacer(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Catalog = FamilyCatalog.Load(document);
            Levels = LevelResolver.Load(document);
        }

        /// <summary>Lighting fixture types in the project, for the mapping dialog.</summary>
        public FamilyCatalog Catalog { get; }

        /// <summary>Levels in the project, for the mapping dialog.</summary>
        public LevelResolver Levels { get; }

        /// <summary>Resolves a group's mapping against what the project actually has.</summary>
        public ResolvedTarget Resolve(PlacementGroup group, PlacementOptions options)
        {
            ResolvedTarget target = new ResolvedTarget();

            string key = FamilyMapping.MakeKey(group.ProductBlock, group.ZMillimetres);
            if (!options.Mappings.TryGetValue(key, out FamilyMapping mapping) || !mapping.IsMapped)
            {
                target.Problem = "no family chosen";
                return target;
            }

            target.Mapping = mapping;
            target.Entry = Catalog.Find(mapping.FamilyName, mapping.TypeName);
            if (target.Entry == null)
            {
                target.Problem = string.Format(
                    CultureInfo.CurrentCulture,
                    "family type '{0} : {1}' is not loaded in this project",
                    mapping.FamilyName, mapping.TypeName);
                return target;
            }

            target.Level = Levels.FindByName(mapping.LevelName);
            if (target.Level == null)
            {
                target.Problem = string.Format(
                    CultureInfo.CurrentCulture,
                    "level '{0}' does not exist in this project", mapping.LevelName);
                return target;
            }

            target.ElevationFeet =
                target.Level.Elevation + LengthUnits.MillimetresToFeet(mapping.OffsetMillimetres);

            return target;
        }

        /// <summary>
        /// Activating a symbol is a model change, so it has to happen inside the
        /// transaction and before the first instance of it is created.
        /// </summary>
        public void EnsureActive(FamilySymbol symbol)
        {
            if (symbol != null && !symbol.IsActive)
            {
                symbol.Activate();
                _document.Regenerate();
            }
        }

        /// <summary>Creates one luminaire. Must be called inside an open transaction.</summary>
        public FamilyInstance Create(LuminaireInstance instance, ResolvedTarget target,
            PlacementOptions options)
        {
            XYZ point = options.Transform.ToRevit(instance.X, instance.Y, target.ElevationFeet);

            FamilyInstance created = _document.Create.NewFamilyInstance(
                point, target.Entry.Symbol, target.Level, StructuralType.NonStructural);

            if (target.Mapping.ApplyRotation)
            {
                SetRotation(created, point, DesiredRotation(instance, options));
            }

            DialuxStamp.Write(created, instance, options.SourceFile, options.BatchId);
            return created;
        }

        /// <summary>
        /// Moves an existing luminaire onto its new position, keeping the
        /// element and everything attached to it.
        /// </summary>
        public void MoveTo(FamilyInstance existing, LuminaireInstance instance,
            ResolvedTarget target, PlacementOptions options)
        {
            if (!(existing.Location is LocationPoint location))
            {
                return;
            }

            XYZ destination = options.Transform.ToRevit(instance.X, instance.Y, target.ElevationFeet);
            XYZ translation = destination - location.Point;

            if (!translation.IsZeroLength())
            {
                ElementTransformUtils.MoveElement(_document, existing.Id, translation);
            }

            if (target.Mapping.ApplyRotation)
            {
                // Rotate by the difference, since the element already carries
                // whatever rotation the previous import gave it.
                double current = (existing.Location as LocationPoint)?.Rotation ?? 0.0;
                SetRotation(existing, destination, DesiredRotation(instance, options) - current);
            }

            DialuxStamp.Write(existing, instance, options.SourceFile, options.BatchId);
        }

        /// <summary>Swaps the family type of an existing luminaire, in place.</summary>
        public void Retype(FamilyInstance existing, LuminaireInstance instance,
            ResolvedTarget target, PlacementOptions options)
        {
            EnsureActive(target.Entry.Symbol);

            if (existing.Symbol == null || existing.Symbol.Id != target.Entry.Symbol.Id)
            {
                existing.Symbol = target.Entry.Symbol;
            }

            MoveTo(existing, instance, target, options);
        }

        private static double DesiredRotation(LuminaireInstance instance, PlacementOptions options)
        {
            // The alignment's own rotation is added, so a rotated alignment keeps
            // luminaires pointing the way they do in DIALux rather than all
            // facing the model's north.
            return (instance.RotationDegrees * Math.PI / 180.0) + options.Transform.RotationRadians;
        }

        private void SetRotation(FamilyInstance instance, XYZ point, double radians)
        {
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
    }
}
