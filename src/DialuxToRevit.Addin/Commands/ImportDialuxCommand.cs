using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using DialuxToRevit.Addin.ViewModels;
using DialuxToRevit.Addin.Views;
using DialuxToRevit.Core.Model;
using DialuxToRevit.Core.Parsing;
using DialuxToRevit.Revit.Geometry;
using DialuxToRevit.Revit.Placement;

namespace DialuxToRevit.Addin.Commands
{
    /// <summary>Reads a DIALux DXF export and places its luminaires.</summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ImportDialuxCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message,
            ElementSet elements)
        {
            UIDocument uiDocument = commandData?.Application?.ActiveUIDocument;
            if (uiDocument == null)
            {
                message = "Open a project first.";
                return Result.Failed;
            }

            Document document = uiDocument.Document;
            if (document.IsFamilyDocument)
            {
                message = "Luminaires can only be placed into a project, not a family.";
                return Result.Failed;
            }

            string path = AskForExport();
            if (path == null)
            {
                return Result.Cancelled;
            }

            DialuxImportResult import;
            try
            {
                import = DialuxDxfReader.Read(path);
            }
            catch (InvalidDataException exception)
            {
                message = "That file could not be read as a DXF: " + exception.Message;
                return Result.Failed;
            }
            catch (IOException exception)
            {
                message = "That file could not be opened: " + exception.Message;
                return Result.Failed;
            }

            if (import.Groups.Count == 0)
            {
                TaskDialog.Show(
                    "Import DIALux luminaires",
                    "No luminaires were found.\n\n" +
                    "The export needs the Luminaires layer enabled with one layer per " +
                    "product type, and it must be a 3D export -- a 2D plan writes the " +
                    "symbols as loose lines instead of blocks.");
                return Result.Cancelled;
            }

            LuminairePlacer placer = new LuminairePlacer(document);
            MappingViewModel viewModel = new MappingViewModel(import, placer);

            MappingWindow window = new MappingWindow(viewModel);
            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            if (window.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            PlacementOptions options = new PlacementOptions
            {
                SourceFile = path,
                BatchId = Guid.NewGuid().ToString("N").Substring(0, 12),

                // Origin to origin for now. Two-point alignment is the next
                // piece of work; until then a model whose origin differs from
                // the export's needs the export re-based in DIALux.
                Transform = CoordinateTransform.OriginToOrigin()
            };

            foreach (KeyValuePair<string, FamilyMapping> pair in viewModel.BuildMappings())
            {
                options.Mappings[pair.Key] = pair.Value;
            }

            PlacementResult result;
            try
            {
                result = placer.Place(import, options);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException exception)
            {
                message = "Nothing was placed: " + exception.Message;
                return Result.Failed;
            }

            ImportLog.Append(document.PathName, result);
            ShowSummary(uiDocument, result);

            return result.Succeeded ? Result.Succeeded : Result.Failed;
        }

        private static string AskForExport()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Choose a DIALux DXF export",
                Filter = "DIALux export (*.dxf)|*.dxf"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static void ShowSummary(UIDocument uiDocument, PlacementResult result)
        {
            TaskDialog dialog = new TaskDialog("Import DIALux luminaires")
            {
                MainInstruction = string.Format(
                    CultureInfo.CurrentCulture, "Placed {0} luminaires.", result.PlacedCount),
                MainContent = result.Summarise()
            };

            if (result.PlacedCount > 0)
            {
                dialog.AddCommandLink(
                    TaskDialogCommandLinkId.CommandLink1, "Select what was placed");
            }

            if (dialog.Show() == TaskDialogResult.CommandLink1 && result.PlacedCount > 0)
            {
                uiDocument.Selection.SetElementIds(result.PlacedIds.ToList());
            }
        }
    }
}
