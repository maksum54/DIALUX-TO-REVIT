using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>
    /// What one placement group becomes in Revit: which family type, on which
    /// level, at what offset.
    ///
    /// Mappings are keyed by <see cref="BlockId"/> and mounting height rather
    /// than by layer name. The block id is DIALux's own product identity and
    /// survives a layer being renamed or the LUM numbering changing between
    /// revisions; a layer name does not.
    /// </summary>
    public sealed class FamilyMapping
    {
        /// <summary>DIALux product block, e.g. "39794_2".</summary>
        public string BlockId { get; set; }

        /// <summary>Mounting height in millimetres, part of the key.</summary>
        public double ZMillimetres { get; set; }

        /// <summary>Revit family name, exactly as the project has it.</summary>
        public string FamilyName { get; set; }

        /// <summary>Revit family type name.</summary>
        public string TypeName { get; set; }

        /// <summary>Revit level name to place on.</summary>
        public string LevelName { get; set; }

        /// <summary>Height above the level, in millimetres.</summary>
        public double OffsetMillimetres { get; set; }

        /// <summary>Whether to apply the rotation from the export.</summary>
        public bool ApplyRotation { get; set; }

        /// <summary>Rows left unmapped are skipped rather than guessed at.</summary>
        public bool IsMapped
        {
            get { return !string.IsNullOrEmpty(FamilyName) && !string.IsNullOrEmpty(TypeName); }
        }

        public FamilyMapping()
        {
            ApplyRotation = true;
        }

        /// <summary>Key for matching a saved mapping back to a group.</summary>
        public static string MakeKey(string blockId, double zMillimetres)
        {
            return string.Format(
                CultureInfo.InvariantCulture, "{0}@{1:F1}", blockId ?? string.Empty, zMillimetres);
        }

        public string Key
        {
            get { return MakeKey(BlockId, ZMillimetres); }
        }

        public override string ToString()
        {
            return Key + " -> " + FamilyName + " : " + TypeName;
        }
    }
}
