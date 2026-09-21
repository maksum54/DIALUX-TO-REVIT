using System.Collections.Generic;
using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>
    /// The unit the user maps to a Revit family type: one product type at one
    /// mounting height on one storey.
    ///
    /// Height is part of the key on purpose. A single layer can legitimately
    /// contain more than one mounting height (the sample export has 21
    /// luminaires at 2400 mm and one at 3000 mm on the same layer), and merging
    /// them would place fixtures at a height nobody chose. Each height is kept
    /// separate and raises a warning instead.
    /// </summary>
    public sealed class PlacementGroup
    {
        public PlacementGroup()
        {
            Instances = new List<LuminaireInstance>();
        }

        public StoreyKey Storey { get; set; }

        public int TypeIndex { get; set; }

        /// <summary>Mounting height in millimetres.</summary>
        public double ZMillimetres { get; set; }

        public string Layer { get; set; }

        public string ProductBlock { get; set; }

        /// <summary>The luminaire list row for this type; null if the list is missing.</summary>
        public LuminaireType Type { get; set; }

        public List<LuminaireInstance> Instances { get; private set; }

        public int Count { get { return Instances.Count; } }

        /// <summary>Distinct rotations present, rounded to two decimals.</summary>
        public IList<double> DistinctRotations
        {
            get
            {
                List<double> found = new List<double>();
                for (int i = 0; i < Instances.Count; i++)
                {
                    double rounded = System.Math.Round(Instances[i].RotationDegrees, 2);
                    if (!found.Contains(rounded))
                    {
                        found.Add(rounded);
                    }
                }

                found.Sort();
                return found;
            }
        }

        /// <summary>Size of the luminaires in this group, in millimetres.</summary>
        public BlockSize Size { get; set; }

        public string Description
        {
            get { return Type != null ? Type.Description : Layer; }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} type {1} @ {2:F0} mm x{3}",
                Storey, TypeIndex, ZMillimetres, Count);
        }
    }
}
