using System.Collections.Generic;
using System.Globalization;

namespace DialuxToRevit.Core.Dxf
{
    /// <summary>
    /// A single DXF entity: the group codes from its opening code 0 up to the
    /// next one.
    /// </summary>
    public sealed class DxfEntity
    {
        private readonly List<DxfCodePair> _codes;

        public DxfEntity(string type, List<DxfCodePair> codes)
        {
            Type = type;
            _codes = codes ?? new List<DxfCodePair>();
        }

        /// <summary>Entity type name, e.g. INSERT, TEXT, ACAD_TABLE.</summary>
        public string Type { get; private set; }

        public IReadOnlyList<DxfCodePair> Codes { get { return _codes; } }

        /// <summary>Layer name (group code 8).</summary>
        public string Layer { get { return GetString(8, string.Empty); } }

        public string GetString(int code, string fallback)
        {
            for (int i = 0; i < _codes.Count; i++)
            {
                if (_codes[i].Code == code)
                {
                    return _codes[i].Value;
                }
            }

            return fallback;
        }

        /// <summary>
        /// Reads a numeric group code. DXF always writes '.' as the decimal
        /// separator, so this parses invariantly -- letting the current culture
        /// decide would silently mangle coordinates on, for example, an
        /// Indonesian or German Windows install.
        /// </summary>
        public double GetDouble(int code, double fallback)
        {
            string raw = GetString(code, null);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            double parsed;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public int GetInt(int code, int fallback)
        {
            string raw = GetString(code, null);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            int parsed;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
