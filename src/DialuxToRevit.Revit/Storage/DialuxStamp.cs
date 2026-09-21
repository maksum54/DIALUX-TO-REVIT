using System;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using DialuxToRevit.Core.Model;

namespace DialuxToRevit.Revit.Storage
{
    /// <summary>
    /// Identity the add-in writes onto every luminaire it places.
    ///
    /// Extensible Storage is used rather than project parameters so the data
    /// travels with the element without adding anything to the project's
    /// parameter list. It is written from the first release even though the
    /// re-import diff comes later: without it, fixtures placed today could
    /// never be matched against a future export, and would have to be deleted
    /// and replaced wholesale.
    /// </summary>
    public static class DialuxStamp
    {
        private static readonly Guid SchemaGuid = new Guid("6f2b1c84-3d5a-4f19-9a6e-2c7d41f8b0e3");

        private const string SchemaName = "DialuxToRevit";
        private const string VendorId = "DLXR";

        private const string FieldSourceFile = "SourceFile";
        private const string FieldStorey = "Storey";
        private const string FieldBlockId = "BlockId";
        private const string FieldKey = "Key";
        private const string FieldBatchId = "BatchId";

        /// <summary>Position rounding used by the key, in millimetres.</summary>
        public const double KeyToleranceMillimetres = 1.0;

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
            {
                return existing;
            }

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Vendor);
            builder.SetVendorId(VendorId);

            builder.AddSimpleField(FieldSourceFile, typeof(string));
            builder.AddSimpleField(FieldStorey, typeof(string));
            builder.AddSimpleField(FieldBlockId, typeof(string));
            builder.AddSimpleField(FieldKey, typeof(string));
            builder.AddSimpleField(FieldBatchId, typeof(string));

            return builder.Finish();
        }

        /// <summary>
        /// Identity of one luminaire, stable across re-imports as long as it has
        /// not moved. Position is rounded to a millimetre, which is far coarser
        /// than DIALux's output precision and far finer than any real move.
        /// </summary>
        public static string MakeKey(string blockId, double xMillimetres,
            double yMillimetres, double zMillimetres)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}",
                blockId ?? string.Empty,
                Round(xMillimetres),
                Round(yMillimetres),
                Round(zMillimetres));
        }

        private static long Round(double millimetres)
        {
            return (long)Math.Round(
                millimetres / KeyToleranceMillimetres, MidpointRounding.AwayFromZero);
        }

        /// <summary>Writes the stamp. Must be called inside an open transaction.</summary>
        public static void Write(Element element, LuminaireInstance instance,
            string sourceFile, string batchId)
        {
            if (element == null || instance == null)
            {
                return;
            }

            Entity entity = new Entity(GetOrCreateSchema());
            entity.Set(FieldSourceFile, sourceFile ?? string.Empty);
            entity.Set(FieldStorey, instance.Storey.ToString());
            entity.Set(FieldBlockId, instance.ProductBlock ?? string.Empty);
            entity.Set(FieldKey, MakeKey(instance.ProductBlock, instance.X, instance.Y, instance.Z));
            entity.Set(FieldBatchId, batchId ?? string.Empty);

            element.SetEntity(entity);
        }

        /// <summary>Reads the stamp back, or null when the element carries none.</summary>
        public static StampData Read(Element element)
        {
            if (element == null)
            {
                return null;
            }

            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null)
            {
                return null;
            }

            Entity entity = element.GetEntity(schema);
            if (entity == null || !entity.IsValid())
            {
                return null;
            }

            return new StampData
            {
                SourceFile = entity.Get<string>(FieldSourceFile),
                Storey = entity.Get<string>(FieldStorey),
                BlockId = entity.Get<string>(FieldBlockId),
                Key = entity.Get<string>(FieldKey),
                BatchId = entity.Get<string>(FieldBatchId)
            };
        }
    }

    /// <summary>The stamp read back off an element.</summary>
    public sealed class StampData
    {
        public string SourceFile { get; set; }

        public string Storey { get; set; }

        public string BlockId { get; set; }

        public string Key { get; set; }

        public string BatchId { get; set; }
    }
}
