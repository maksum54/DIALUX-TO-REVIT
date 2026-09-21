using System.Collections.Generic;
using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>
    /// One physical luminaire, after the INSERTs that make it up have been
    /// folded together. Coordinates are millimetres, exactly as DIALux writes
    /// them; conversion to Revit's internal feet happens at placement time.
    /// </summary>
    public sealed class LuminaireInstance
    {
        public LuminaireInstance()
        {
            PartBlocks = new List<string>();
        }

        public string Layer { get; set; }

        public StoreyKey Storey { get; set; }

        /// <summary>Type number from the layer, matching the luminaire list Index.</summary>
        public int TypeIndex { get; set; }

        /// <summary>Block name without the trailing part number, e.g. "39794_2".</summary>
        public string ProductBlock { get; set; }

        /// <summary>
        /// Every block drawn at this point. A luminaire is regularly exported as
        /// more than one INSERT -- housing and optic as separate blocks sharing
        /// an insertion point -- so this records what was merged.
        /// </summary>
        public List<string> PartBlocks { get; private set; }

        public double X { get; set; }

        public double Y { get; set; }

        /// <summary>Mounting height in millimetres, straight from group code 30.</summary>
        public double Z { get; set; }

        /// <summary>
        /// Rotation about Z in degrees (group code 50). Real exports contain
        /// values such as 269.73 and 0.64, so this is never snapped to 90.
        /// </summary>
        public double RotationDegrees { get; set; }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} type {1} at ({2:F1}, {3:F1}, {4:F1}) rot {5:F2}",
                Layer, TypeIndex, X, Y, Z, RotationDegrees);
        }
    }
}
