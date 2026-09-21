using System;
using System.Reflection;
using Autodesk.Revit.UI;

namespace DialuxToRevit.Addin
{
    /// <summary>Builds the ribbon when Revit starts.</summary>
    public sealed class App : IExternalApplication
    {
        private const string TabName = "DIALux";
        private const string PanelName = "Luminaires";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // The tab already exists, which is the normal case when another
                // add-in of ours has loaded first.
            }

            RibbonPanel panel = application.CreateRibbonPanel(TabName, PanelName);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData importButton = new PushButtonData(
                "DialuxImport",
                "Import" + Environment.NewLine + "DXF",
                assemblyPath,
                typeof(Commands.ImportDialuxCommand).FullName)
            {
                ToolTip = "Read a DIALux DXF export and place its luminaires.",
                LongDescription =
                    "Reads a DIALux Evo DXF export, shows what it contains, and places " +
                    "the luminaires using the family types you choose per product type."
            };

            panel.AddItem(importButton);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
