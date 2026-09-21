using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DialuxToRevit.Core.Dxf
{
    /// <summary>
    /// Minimal reader for ASCII DXF. Only what a DIALux luminaire export needs:
    /// header variables and the entities of a named section.
    /// </summary>
    public sealed class DxfDocument
    {
        private readonly List<DxfCodePair> _pairs;
        private readonly Dictionary<string, string> _header;

        private DxfDocument(List<DxfCodePair> pairs, Dictionary<string, string> header)
        {
            _pairs = pairs;
            _header = header;
        }

        /// <summary>Header variables such as $INSUNITS, keyed including the '$'.</summary>
        public IReadOnlyDictionary<string, string> Header { get { return _header; } }

        public static DxfDocument Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("DXF file not found.", path);
            }

            List<DxfCodePair> pairs = ReadPairs(path);
            return new DxfDocument(pairs, ReadHeader(pairs));
        }

        /// <summary>
        /// ASCII DXF is strictly alternating: a group code line, then its value
        /// line. Reading it any other way risks drifting out of step on values
        /// that happen to look like codes.
        /// </summary>
        private static List<DxfCodePair> ReadPairs(string path)
        {
            List<DxfCodePair> pairs = new List<DxfCodePair>();

            // DIALux declares $DWGCODEPAGE ANSI_1252. Group codes, coordinates
            // and DIALux's own layer names are all ASCII, so they read back
            // exactly; a non-ASCII character in a manufacturer name is the one
            // thing that may not round-trip until a CP1252 decoder is wired in.
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
            {
                int lineNumber = 0;
                while (true)
                {
                    string codeLine = reader.ReadLine();
                    if (codeLine == null)
                    {
                        break;
                    }

                    lineNumber++;
                    string trimmed = codeLine.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    int code;
                    if (!int.TryParse(trimmed, out code))
                    {
                        throw new InvalidDataException(
                            "Malformed DXF at line " + lineNumber +
                            ": expected a group code but found '" + trimmed + "'. " +
                            "Binary DXF is not supported; export as ASCII DXF.");
                    }

                    string valueLine = reader.ReadLine();
                    if (valueLine == null)
                    {
                        break;
                    }

                    lineNumber++;
                    pairs.Add(new DxfCodePair(code, valueLine.Trim()));
                }
            }

            return pairs;
        }

        private static Dictionary<string, string> ReadHeader(List<DxfCodePair> pairs)
        {
            Dictionary<string, string> header =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            bool inside = false;
            for (int i = 0; i < pairs.Count; i++)
            {
                DxfCodePair pair = pairs[i];

                if (pair.Code == 2 && i > 0 && pairs[i - 1].Code == 0 && pairs[i - 1].Value == "SECTION")
                {
                    inside = string.Equals(pair.Value, "HEADER", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (pair.Code == 0 && pair.Value == "ENDSEC")
                {
                    if (inside)
                    {
                        break;
                    }

                    continue;
                }

                // A header variable is a code 9 name followed by its value pair.
                if (inside && pair.Code == 9 && i + 1 < pairs.Count && !header.ContainsKey(pair.Value))
                {
                    header[pair.Value] = pairs[i + 1].Value;
                }
            }

            return header;
        }

        /// <summary>Entities of a section, in file order.</summary>
        public List<DxfEntity> GetEntities(string sectionName)
        {
            List<DxfEntity> entities = new List<DxfEntity>();

            bool inside = false;
            string currentType = null;
            List<DxfCodePair> current = null;

            for (int i = 0; i < _pairs.Count; i++)
            {
                DxfCodePair pair = _pairs[i];

                if (pair.Code == 2 && i > 0 && _pairs[i - 1].Code == 0 && _pairs[i - 1].Value == "SECTION")
                {
                    inside = string.Equals(pair.Value, sectionName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (pair.Code == 0 && pair.Value == "ENDSEC")
                {
                    if (inside && current != null)
                    {
                        entities.Add(new DxfEntity(currentType, current));
                        current = null;
                    }

                    inside = false;
                    continue;
                }

                if (!inside)
                {
                    continue;
                }

                if (pair.Code == 0)
                {
                    if (current != null)
                    {
                        entities.Add(new DxfEntity(currentType, current));
                    }

                    currentType = pair.Value;
                    current = new List<DxfCodePair>();
                    current.Add(pair);
                }
                else if (current != null)
                {
                    current.Add(pair);
                }
            }

            if (current != null)
            {
                entities.Add(new DxfEntity(currentType, current));
            }

            return entities;
        }
    }
}
