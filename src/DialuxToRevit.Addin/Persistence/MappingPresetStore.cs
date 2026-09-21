using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Addin.Persistence
{
    /// <summary>
    /// Saves and reloads the family choices.
    ///
    /// Entries are keyed by DIALux block id and mounting height, never by layer
    /// name, so a preset still applies after a layer is renamed or the LUM
    /// numbering shifts between revisions.
    /// </summary>
    public static class MappingPresetStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string DefaultDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DialuxToRevit");

        public static void Save(string path, IEnumerable<FamilyMapping> mappings)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            MappingPreset preset = new MappingPreset
            {
                SavedAt = DateTime.Now,
                Mappings = new List<FamilyMapping>(mappings)
            };

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(preset, Options));
        }

        /// <summary>
        /// Loads a preset, keyed for lookup. A malformed or unreadable file
        /// yields an empty set rather than throwing: a bad preset should leave
        /// the user filling the grid in by hand, not block the import.
        /// </summary>
        public static Dictionary<string, FamilyMapping> Load(string path)
        {
            Dictionary<string, FamilyMapping> result =
                new Dictionary<string, FamilyMapping>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return result;
            }

            try
            {
                MappingPreset preset =
                    JsonSerializer.Deserialize<MappingPreset>(File.ReadAllText(path), Options);

                if (preset?.Mappings == null)
                {
                    return result;
                }

                foreach (FamilyMapping mapping in preset.Mappings)
                {
                    if (!string.IsNullOrEmpty(mapping?.BlockId))
                    {
                        result[mapping.Key] = mapping;
                    }
                }
            }
            catch (JsonException)
            {
                return result;
            }
            catch (IOException)
            {
                return result;
            }

            return result;
        }
    }

    /// <summary>The file format written by <see cref="MappingPresetStore"/>.</summary>
    public sealed class MappingPreset
    {
        public int Version { get; set; } = 1;

        public DateTime SavedAt { get; set; }

        public List<FamilyMapping> Mappings { get; set; } = new List<FamilyMapping>();
    }
}
