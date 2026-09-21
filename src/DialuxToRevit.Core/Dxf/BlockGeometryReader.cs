using System;
using System.Collections.Generic;

namespace DialuxToRevit.Core.Dxf
{
    /// <summary>
    /// Measures each block definition in the BLOCKS section.
    ///
    /// DIALux draws luminaires as polyface meshes stored in metres and scaled by
    /// 1000 at insertion, so a block's size in millimetres is the box measured
    /// here multiplied by the INSERT scale factors.
    /// </summary>
    public static class BlockGeometryReader
    {
        /// <summary>
        /// Bounding box extents per block name, in block units. Blocks that
        /// carry no vertices are left out, so a caller can tell "no geometry"
        /// apart from "measured as zero".
        /// </summary>
        public static Dictionary<string, double[]> ReadExtents(DxfDocument document)
        {
            Dictionary<string, double[]> extents =
                new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

            if (document == null)
            {
                return extents;
            }

            string current = null;
            double[] min = null;
            double[] max = null;

            foreach (DxfEntity entity in document.GetEntities("BLOCKS"))
            {
                if (string.Equals(entity.Type, "BLOCK", StringComparison.OrdinalIgnoreCase))
                {
                    Commit(extents, current, min, max);
                    current = entity.GetString(2, string.Empty);
                    min = null;
                    max = null;
                }
                else if (string.Equals(entity.Type, "ENDBLK", StringComparison.OrdinalIgnoreCase))
                {
                    Commit(extents, current, min, max);
                    current = null;
                    min = null;
                    max = null;
                }
                else if (current != null
                    && string.Equals(entity.Type, "VERTEX", StringComparison.OrdinalIgnoreCase))
                {
                    double x = entity.GetDouble(10, 0.0);
                    double y = entity.GetDouble(20, 0.0);
                    double z = entity.GetDouble(30, 0.0);

                    if (min == null)
                    {
                        min = new[] { x, y, z };
                        max = new[] { x, y, z };
                    }
                    else
                    {
                        if (x < min[0]) { min[0] = x; }
                        if (y < min[1]) { min[1] = y; }
                        if (z < min[2]) { min[2] = z; }
                        if (x > max[0]) { max[0] = x; }
                        if (y > max[1]) { max[1] = y; }
                        if (z > max[2]) { max[2] = z; }
                    }
                }
            }

            Commit(extents, current, min, max);
            return extents;
        }

        private static void Commit(Dictionary<string, double[]> extents,
            string name, double[] min, double[] max)
        {
            if (string.IsNullOrEmpty(name) || min == null || max == null)
            {
                return;
            }

            extents[name] = new[]
            {
                max[0] - min[0],
                max[1] - min[1],
                max[2] - min[2]
            };
        }
    }
}
