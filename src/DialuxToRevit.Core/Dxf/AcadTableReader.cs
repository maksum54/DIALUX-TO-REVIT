using System.Collections.Generic;

namespace DialuxToRevit.Core.Dxf
{
    /// <summary>A rows x columns grid of cell strings read from an ACAD_TABLE.</summary>
    public sealed class TableGrid
    {
        private readonly string[,] _cells;

        public TableGrid(int rows, int columns)
        {
            RowCount = rows;
            ColumnCount = columns;
            _cells = new string[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    _cells[r, c] = string.Empty;
                }
            }
        }

        public int RowCount { get; private set; }

        public int ColumnCount { get; private set; }

        public string this[int row, int column]
        {
            get
            {
                if (row < 0 || row >= RowCount || column < 0 || column >= ColumnCount)
                {
                    return string.Empty;
                }

                return _cells[row, column];
            }

            set { _cells[row, column] = value ?? string.Empty; }
        }
    }

    /// <summary>Reads the luminaire list out of an ACAD_TABLE entity.</summary>
    public static class AcadTableReader
    {
        /// <summary>
        /// Rebuilds the cell grid.
        ///
        /// The cell count is taken from the table header (code 91 rows, code 92
        /// columns) and cells are walked in row-major order, one per
        /// "301 CELL_VALUE" marker. That matters because DIALux leaves cells
        /// empty -- an empty cell still emits a CELL_VALUE record but carries no
        /// text, so collecting the text values alone would shift every later
        /// column left by one and mislabel the whole table.
        /// </summary>
        public static TableGrid TryReadGrid(DxfEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            // Code 310 holds a large binary mirror of the table; skipping it
            // keeps the scan below cheap and avoids false code matches.
            List<DxfCodePair> body = new List<DxfCodePair>();
            for (int i = 0; i < entity.Codes.Count; i++)
            {
                if (entity.Codes[i].Code != 310)
                {
                    body.Add(entity.Codes[i]);
                }
            }

            int start = -1;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Code == 100 && body[i].Value == "AcDbTable")
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                return null;
            }

            int rows = -1;
            int columns = -1;
            for (int i = start; i < body.Count; i++)
            {
                if (body[i].Code == 91 && rows < 0)
                {
                    int parsed;
                    if (int.TryParse(body[i].Value, out parsed))
                    {
                        rows = parsed;
                    }
                }
                else if (body[i].Code == 92 && columns < 0)
                {
                    int parsed;
                    if (int.TryParse(body[i].Value, out parsed))
                    {
                        columns = parsed;
                    }
                }

                if (rows > 0 && columns > 0)
                {
                    break;
                }
            }

            if (rows <= 0 || columns <= 0)
            {
                return null;
            }

            List<int> marks = new List<int>();
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Code == 301 && body[i].Value == "CELL_VALUE")
                {
                    marks.Add(i);
                }
            }

            TableGrid grid = new TableGrid(rows, columns);
            int cellLimit = rows * columns;

            for (int n = 0; n < marks.Count && n < cellLimit; n++)
            {
                int from = marks[n];
                int to = (n + 1 < marks.Count) ? marks[n + 1] : body.Count;
                grid[n / columns, n % columns] = ReadCellText(body, from, to);
            }

            return grid;
        }

        /// <summary>
        /// Cell text lives in code 302, with code 1 as a fallback for writers
        /// that only fill the legacy field.
        /// </summary>
        private static string ReadCellText(List<DxfCodePair> body, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (body[i].Code == 302 && !string.IsNullOrEmpty(body[i].Value))
                {
                    return body[i].Value;
                }
            }

            for (int i = from; i < to; i++)
            {
                if (body[i].Code == 1 && !string.IsNullOrEmpty(body[i].Value))
                {
                    return body[i].Value;
                }
            }

            return string.Empty;
        }
    }
}
