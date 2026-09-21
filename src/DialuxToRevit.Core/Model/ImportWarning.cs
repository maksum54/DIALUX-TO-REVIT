namespace DialuxToRevit.Core.Model
{
    /// <summary>How much attention a warning deserves.</summary>
    public enum WarningSeverity
    {
        /// <summary>Worth knowing; import can proceed as-is.</summary>
        Info,

        /// <summary>The user should look before placing anything.</summary>
        Warning,

        /// <summary>The read is inconsistent; placing would put wrong data in the model.</summary>
        Error
    }

    /// <summary>Something noteworthy found while reading the export.</summary>
    public sealed class ImportWarning
    {
        public ImportWarning(WarningSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public WarningSeverity Severity { get; private set; }

        /// <summary>Short stable identifier, for filtering and for tests.</summary>
        public string Code { get; private set; }

        public string Message { get; private set; }

        public override string ToString()
        {
            return Severity.ToString().ToUpperInvariant() + " [" + Code + "] " + Message;
        }
    }
}
