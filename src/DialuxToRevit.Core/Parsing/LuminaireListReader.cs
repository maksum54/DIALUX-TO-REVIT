using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DialuxToRevit.Core.Dxf;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Core.Parsing
{
    /// <summary>Turns the ACAD_TABLE grid into luminaire type records.</summary>
    public static class LuminaireListReader
    {
        private static readonly Regex TitlePattern = new Regex(
            @"^Luminaire list\s*\((?<inner>.+)\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Reads the table. Row 0 is the title, row 1 the column headers and the
        /// rest are data rows. Columns are located by header text rather than by
        /// position, so a future DIALux version that adds or reorders a column
        /// does not silently shift the values.
        /// </summary>
        public static List<LuminaireType> Read(TableGrid grid, StoreyKey storey,
            out string buildingName, out string storeyName)
        {
            buildingName = string.Empty;
            storeyName = string.Empty;

            List<LuminaireType> types = new List<LuminaireType>();
            if (grid == null || grid.RowCount < 3)
            {
                return types;
            }

            SplitTitle(grid[0, 0], out buildingName, out storeyName);

            Dictionary<string, int> columns =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < grid.ColumnCount; c++)
            {
                string header = (grid[1, c] ?? string.Empty).Trim();
                if (header.Length > 0 && !columns.ContainsKey(header))
                {
                    columns[header] = c;
                }
            }

            int indexColumn;
            if (!columns.TryGetValue("Index", out indexColumn))
            {
                indexColumn = 0;
            }

            for (int r = 2; r < grid.RowCount; r++)
            {
                string indexText = (grid[r, indexColumn] ?? string.Empty).Trim();
                int index;
                if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                {
                    continue;
                }

                LuminaireType type = new LuminaireType();
                type.Index = index;
                type.Storey = storey;
                type.Manufacturer = Cell(grid, columns, r, "Manufacturer");
                type.ArticleName = Cell(grid, columns, r, "Article name");
                type.ItemNumber = Cell(grid, columns, r, "Item number");
                type.Fitting = Cell(grid, columns, r, "Fitting");
                type.LuminousFlux = Cell(grid, columns, r, "Luminous flux");
                type.MaintenanceFactor = Cell(grid, columns, r, "Maintenance factor");
                type.ConnectedLoad = Cell(grid, columns, r, "Connected load");

                string quantityText = Cell(grid, columns, r, "Quantity");
                int quantity;
                if (int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
                {
                    type.Quantity = quantity;
                }

                for (int c = 0; c < grid.ColumnCount; c++)
                {
                    string header = (grid[1, c] ?? string.Empty).Trim();
                    if (header.Length > 0)
                    {
                        type.Columns[header] = (grid[r, c] ?? string.Empty).Trim();
                    }
                }

                types.Add(type);
            }

            return types;
        }

        private static string Cell(TableGrid grid, Dictionary<string, int> columns, int row, string header)
        {
            int column;
            if (!columns.TryGetValue(header, out column))
            {
                return string.Empty;
            }

            return (grid[row, column] ?? string.Empty).Trim();
        }

        /// <summary>
        /// "Luminaire list (Building 2, OFFICE GF)" -> "Building 2" + "OFFICE GF".
        /// Split on the first separator: a storey name is far more likely to
        /// contain a comma than a building name.
        /// </summary>
        public static void SplitTitle(string title, out string buildingName, out string storeyName)
        {
            buildingName = string.Empty;
            storeyName = string.Empty;

            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            Match match = TitlePattern.Match(title.Trim());
            if (!match.Success)
            {
                return;
            }

            string inner = match.Groups["inner"].Value;
            int separator = inner.IndexOf(", ", StringComparison.Ordinal);
            if (separator < 0)
            {
                buildingName = inner.Trim();
                return;
            }

            buildingName = inner.Substring(0, separator).Trim();
            storeyName = inner.Substring(separator + 2).Trim();
        }
    }
}
