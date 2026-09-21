using System.Text.RegularExpressions;

namespace DialuxToRevit.Core.Parsing
{
    /// <summary>
    /// DIALux encodes building and storey in the layer name, e.g.
    /// "DLX_BLD1_FL0_LUM 2". Multi-storey exports reuse the same scheme with a
    /// different FL number, so the building/storey pair is read from every layer
    /// rather than assumed to be constant across the file.
    /// </summary>
    public static class DialuxLayerName
    {
        private static readonly Regex LuminaireLayer = new Regex(
            @"^DLX_BLD(?<bld>\d+)_FL(?<flr>\d+)_LUM\s*(?<idx>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ListLayer = new Regex(
            @"^DLX_BLD(?<bld>\d+)_FL(?<flr>\d+)_LUMKEY$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IndexLayer = new Regex(
            @"^DLX_BLD(?<bld>\d+)_FL(?<flr>\d+)_LUMKEY_IDX$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>A luminaire layer, one per product type per storey.</summary>
        public static bool TryParseLuminaire(string layer, out int building, out int floor, out int typeIndex)
        {
            building = 0;
            floor = 0;
            typeIndex = 0;

            if (string.IsNullOrEmpty(layer))
            {
                return false;
            }

            Match match = LuminaireLayer.Match(layer.Trim());
            if (!match.Success)
            {
                return false;
            }

            building = int.Parse(match.Groups["bld"].Value);
            floor = int.Parse(match.Groups["flr"].Value);
            typeIndex = int.Parse(match.Groups["idx"].Value);
            return true;
        }

        /// <summary>The layer carrying the luminaire list table.</summary>
        public static bool TryParseList(string layer, out int building, out int floor)
        {
            return TryParse(ListLayer, layer, out building, out floor);
        }

        /// <summary>The layer carrying the small type-number labels.</summary>
        public static bool TryParseIndexLabel(string layer, out int building, out int floor)
        {
            return TryParse(IndexLayer, layer, out building, out floor);
        }

        private static bool TryParse(Regex regex, string layer, out int building, out int floor)
        {
            building = 0;
            floor = 0;

            if (string.IsNullOrEmpty(layer))
            {
                return false;
            }

            Match match = regex.Match(layer.Trim());
            if (!match.Success)
            {
                return false;
            }

            building = int.Parse(match.Groups["bld"].Value);
            floor = int.Parse(match.Groups["flr"].Value);
            return true;
        }
    }
}
