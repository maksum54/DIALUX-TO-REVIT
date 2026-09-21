using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DialuxToRevit.Revit.Geometry;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>Finds project levels and works out offsets against them.</summary>
    public sealed class LevelResolver
    {
        private readonly List<Level> _levels;

        private LevelResolver(List<Level> levels)
        {
            _levels = levels;
        }

        public IReadOnlyList<Level> Levels => _levels;

        public static LevelResolver Load(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            List<Level> levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ToList();

            return new LevelResolver(levels);
        }

        public Level FindByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return _levels.FirstOrDefault(level =>
                string.Equals(level.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The level a luminaire at this height most likely belongs to: the
        /// highest one at or below it, falling back to the lowest level when the
        /// fixture sits beneath them all.
        ///
        /// A starting point for the user to correct, not a decision -- the
        /// offset column shows the consequence so a wrong guess is visible.
        /// </summary>
        public Level SuggestForHeight(double zMillimetres)
        {
            if (_levels.Count == 0)
            {
                return null;
            }

            double zFeet = LengthUnits.MillimetresToFeet(zMillimetres);
            Level below = _levels.LastOrDefault(level => level.Elevation <= zFeet + 1e-6);
            return below ?? _levels[0];
        }

        /// <summary>Height of a point above a level, in millimetres.</summary>
        public static double OffsetMillimetres(Level level, double zMillimetres)
        {
            if (level == null)
            {
                return zMillimetres;
            }

            return zMillimetres - LengthUnits.FeetToMillimetres(level.Elevation);
        }
    }
}
