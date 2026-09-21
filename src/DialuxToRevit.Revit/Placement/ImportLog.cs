using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DialuxToRevit.Revit.Placement
{
    /// <summary>
    /// Appends a line per import run so a model's luminaires can be traced back
    /// to the export and the run that created them. Kept next to the Revit
    /// model, because that is where someone auditing it will look.
    /// </summary>
    public static class ImportLog
    {
        public const string FileName = "DialuxToRevit-import-log.txt";

        public static void Append(string modelPath, PlacementResult result)
        {
            if (result == null)
            {
                return;
            }

            string directory = ResolveDirectory(modelPath);
            if (directory == null)
            {
                return;
            }

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss}\tbatch={1}\tsource={2}\tplaced={3}\tskipped={4}\tfailed={5}\tstatus={6}",
                result.StartedAt,
                result.BatchId,
                Path.GetFileName(result.SourceFile ?? string.Empty),
                result.PlacedCount,
                result.Skipped.Count,
                result.Failures.Count,
                result.Succeeded ? "ok" : "errors");

            try
            {
                File.AppendAllText(Path.Combine(directory, FileName), line + Environment.NewLine);
            }
            catch (IOException)
            {
                // A log that cannot be written must not lose the import that was
                // already committed, so this failure is swallowed deliberately.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Recent entries, newest first.</summary>
        public static IEnumerable<string> ReadRecent(string modelPath, int count)
        {
            string directory = ResolveDirectory(modelPath);
            if (directory == null)
            {
                return Enumerable.Empty<string>();
            }

            string path = Path.Combine(directory, FileName);
            if (!File.Exists(path))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                return File.ReadAllLines(path).Reverse().Take(count).ToList();
            }
            catch (IOException)
            {
                return Enumerable.Empty<string>();
            }
        }

        private static string ResolveDirectory(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                // An unsaved model has nowhere of its own to log to.
                return null;
            }

            try
            {
                return Path.GetDirectoryName(modelPath);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
