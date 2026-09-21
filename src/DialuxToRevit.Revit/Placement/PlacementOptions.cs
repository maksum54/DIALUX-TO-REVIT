using System.Collections.Generic;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Revit.Geometry;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>Everything the placer needs beyond the export itself.</summary>
    public sealed class PlacementOptions
    {
        public PlacementOptions()
        {
            Mappings = new Dictionary<string, FamilyMapping>();
            Transform = CoordinateTransform.OriginToOrigin();
        }

        /// <summary>Mappings by <see cref="FamilyMapping.Key"/>.</summary>
        public Dictionary<string, FamilyMapping> Mappings { get; }

        public CoordinateTransform Transform { get; set; }

        /// <summary>The export path, recorded on every placed element.</summary>
        public string SourceFile { get; set; }

        /// <summary>Identifies this import run, for the log and for later diffs.</summary>
        public string BatchId { get; set; }
    }
}
