using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DialuxToRevit.Core.Dxf;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Core.Parsing
{
    /// <summary>Storey and type number assigned to one luminaire layer.</summary>
    public sealed class LuminaireLayer
    {
        public int Building { get; set; }
        public int Floor { get; set; }
        public int TypeIndex { get; set; }
    }

    /// <summary>
    /// Decides which layers carry luminaires.
    ///
    /// DIALux-named layers ("DLX_BLD1_FL0_LUM 2", "DLX_LUM 2") are read as
    /// usual. When a file has none -- the layers were renamed in CAD, or the
    /// export used a custom scheme -- every layer holding DIALux luminaire
    /// blocks becomes one type, numbered in layer-name order, so any layer name
    /// imports. The layer name then stands in for the product description.
    /// </summary>
    public static class LuminaireLayers
    {
        /// <summary>DIALux block names look like "39794_2_0".</summary>
        private static readonly Regex DialuxBlock = new Regex(
            @"^\d+_\d+_\d+$", RegexOptions.Compiled);

        public static Dictionary<string, LuminaireLayer> Classify(
            List<DxfEntity> entities, DialuxImportResult result)
        {
            Dictionary<string, LuminaireLayer> layers =
                new Dictionary<string, LuminaireLayer>(StringComparer.OrdinalIgnoreCase);

            List<DxfEntity> inserts = entities
                .Where(e => string.Equals(e.Type, "INSERT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (DxfEntity insert in inserts)
            {
                int building, floor, typeIndex;
                string layer = insert.Layer ?? string.Empty;
                if (!layers.ContainsKey(layer)
                    && DialuxLayerName.TryParseLuminaire(layer, out building, out floor, out typeIndex))
                {
                    layers[layer] = new LuminaireLayer
                    {
                        Building = building,
                        Floor = floor,
                        TypeIndex = typeIndex
                    };
                }
            }

            if (layers.Count > 0)
            {
                return layers;
            }

            // No DIALux layer names: fall back to the blocks. Prefer INSERTs of
            // DIALux luminaire blocks so title blocks and furniture stay out;
            // if even those were renamed, take every INSERT.
            List<DxfEntity> candidates = inserts
                .Where(e => DialuxBlock.IsMatch(e.GetString(2, string.Empty) ?? string.Empty))
                .ToList();
            if (candidates.Count == 0)
            {
                candidates = inserts;
            }

            List<string> names = candidates
                .Select(e => e.Layer ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < names.Count; i++)
            {
                layers[names[i]] = new LuminaireLayer { TypeIndex = i + 1 };
            }

            if (names.Count > 0)
            {
                result.Warnings.Add(new ImportWarning(
                    WarningSeverity.Info,
                    "LAYERS_NOT_DIALUX",
                    "No DIALux-named luminaire layers; each of the " + names.Count +
                    " layer(s) holding luminaire blocks is read as one type (" +
                    string.Join(", ", names.ToArray()) + ")."));
            }

            return layers;
        }
    }
}
