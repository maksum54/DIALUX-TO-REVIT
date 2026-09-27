using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
                    "the luminaires using the family types you choose per product type.",
                LargeImage = LoadIcon("Import32.png"),
                Image = LoadIcon("Import16.png")
            };

            panel.AddItem(importButton);
            return Result.Succeeded;
        }

        /// <summary>Reads a PNG embedded under Resources; null leaves the button text-only.</summary>
        private static ImageSource LoadIcon(string fileName)
        {
            Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("DialuxToRevit.Addin.Resources." + fileName);
            if (stream == null)
            {
                return null;
            }

            using (stream)
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
