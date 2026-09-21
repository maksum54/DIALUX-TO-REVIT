using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>
    /// Overall size of a luminaire in millimetres, measured from the block
    /// geometry. Advisory only: some DIALux products ship a placeholder box
    /// rather than real dimensions, so this may suggest a family or warn about
    /// a mismatch, but must never choose one.
    /// </summary>
    public struct BlockSize
    {
        public readonly double Width;
        public readonly double Depth;
        public readonly double Height;

        public BlockSize(double width, double depth, double height)
        {
            Width = width;
            Depth = depth;
            Height = height;
        }

        public bool IsEmpty
        {
            get { return Width <= 0.0 && Depth <= 0.0 && Height <= 0.0; }
        }

        /// <summary>The larger of the two plan dimensions.</summary>
        public double LongestPlanSide
        {
            get { return Width > Depth ? Width : Depth; }
        }

        public override string ToString()
        {
            if (IsEmpty)
            {
                return "-";
            }

            return string.Format(
                CultureInfo.InvariantCulture, "{0:F0} x {1:F0} x {2:F0}", Width, Depth, Height);
        }
    }
}
