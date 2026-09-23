using System.Text.RegularExpressions;

namespace DialuxToRevit.Core.Parsing
{
    /// <summary>
    /// DIALux encodes building and storey in the layer name, e.g.
    /// "DLX_BLD1_FL0_LUM 2". Multi-storey exports reuse the same scheme with a
    /// different FL number, so the building/storey pair is read from every layer
    /// rather than assumed to be constant across the file.
    ///
    /// Exports without buildings/storeys drop the BLD/FL part ("DLX_LUM 1"),
    /// and outdoor scenes use "DLX_TERR_LUM 1". Those map to storey 0/0 and to
    /// <see cref="TerrainFloor"/> respectively, so the terrain type numbers --
    /// which restart at 1 -- never collide with the indoor ones.
    /// </summary>
    public static class DialuxLayerName
    {
        /// <summary>Floor number assigned to "DLX_TERR_" (outdoor) layers.</summary>
        public const int TerrainFloor = -1;

        private static readonly Regex LuminaireLayer = new Regex(
            @"^DLX_(?:BLD(?<bld>\d+)_FL(?<flr>\d+)_|(?<terr>TERR)_)?LUM\s*(?<idx>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ListLayer = new Regex(
            @"^DLX_(?:BLD(?<bld>\d+)_FL(?<flr>\d+)_|(?<terr>TERR)_)?LUMKEY$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IndexLayer = new Regex(
            @"^DLX_(?:BLD(?<bld>\d+)_FL(?<flr>\d+)_|(?<terr>TERR)_)?LUMKEY_IDX$",
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

            ReadStorey(match, out building, out floor);
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

            ReadStorey(match, out building, out floor);
            return true;
        }

        private static void ReadStorey(Match match, out int building, out int floor)
        {
            if (match.Groups["bld"].Success)
            {
                building = int.Parse(match.Groups["bld"].Value);
                floor = int.Parse(match.Groups["flr"].Value);
                return;
            }

            building = 0;
            floor = match.Groups["terr"].Success ? TerrainFloor : 0;
        }
    }
}
