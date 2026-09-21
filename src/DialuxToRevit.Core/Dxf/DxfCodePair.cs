using System.Globalization;

namespace DialuxToRevit.Core.Dxf
{
    /// <summary>One group code / value line pair from an ASCII DXF file.</summary>
    public struct DxfCodePair
    {
        public readonly int Code;
        public readonly string Value;

        public DxfCodePair(int code, string value)
        {
            Code = code;
            Value = value;
        }

        public override string ToString()
        {
            return Code.ToString(CultureInfo.InvariantCulture) + " " + Value;
        }
    }
}
