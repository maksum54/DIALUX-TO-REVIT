using System.Collections.Generic;
using System.Linq;

namespace DialuxToRevit.Core.Model
{
    /// <summary>Everything read from one DIALux DXF export.</summary>
    public sealed class DialuxImportResult
    {
        public DialuxImportResult()
        {
            Types = new List<LuminaireType>();
            Instances = new List<LuminaireInstance>();
            Groups = new List<PlacementGroup>();
            Warnings = new List<ImportWarning>();
            StoreyNames = new Dictionary<StoreyKey, string>();
            BuildingNames = new Dictionary<StoreyKey, string>();
        }

        public string SourceFile { get; set; }

        /// <summary>Building name per storey, from the luminaire list title.</summary>
        public Dictionary<StoreyKey, string> BuildingNames { get; private set; }

        /// <summary>Storey name per storey, from the luminaire list title.</summary>
        public Dictionary<StoreyKey, string> StoreyNames { get; private set; }

        /// <summary>Raw INSERT count on luminaire layers, before deduplication.</summary>
        public int InsertCount { get; set; }

        /// <summary>Index labels that agreed with the layer they sit next to.</summary>
        public int LabelsMatched { get; set; }

        /// <summary>Index labels that disagreed.</summary>
        public int LabelsMismatched { get; set; }

        public List<LuminaireType> Types { get; private set; }

        public List<LuminaireInstance> Instances { get; private set; }

        public List<PlacementGroup> Groups { get; private set; }

        public List<ImportWarning> Warnings { get; private set; }

        /// <summary>Deduplicated luminaire count -- what will be placed.</summary>
        public int FixtureCount { get { return Instances.Count; } }

        /// <summary>True when any warning is severe enough to block placement.</summary>
        public bool HasErrors
        {
            get { return Warnings.Any(w => w.Severity == WarningSeverity.Error); }
        }

        public LuminaireType FindType(StoreyKey storey, int index)
        {
            return Types.FirstOrDefault(t => t.Storey.Equals(storey) && t.Index == index);
        }
    }
}
