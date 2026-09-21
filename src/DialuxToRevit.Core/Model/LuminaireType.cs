using System;
using System.Collections.Generic;
using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>One row of the DIALux luminaire list.</summary>
    public sealed class LuminaireType
    {
        public LuminaireType()
        {
            Columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The number shown next to each luminaire, matching the LUM layer.</summary>
        public int Index { get; set; }

        public StoreyKey Storey { get; set; }

        public string Manufacturer { get; set; }

        public string ArticleName { get; set; }

        public string ItemNumber { get; set; }

        public string Fitting { get; set; }

        public string LuminousFlux { get; set; }

        public string MaintenanceFactor { get; set; }

        public string ConnectedLoad { get; set; }

        /// <summary>Quantity as declared by the list; null when unreadable.</summary>
        public int? Quantity { get; set; }

        /// <summary>The block name this type is drawn with, e.g. "39794_2".</summary>
        public string ProductBlock { get; set; }

        /// <summary>Every column of the row, for anything not mapped above.</summary>
        public Dictionary<string, string> Columns { get; private set; }

        /// <summary>
        /// The product name.
        ///
        /// DIALux is not consistent about which column holds it: in the sample
        /// export type 2 leaves "Article name" empty and writes PLPA40L-E/65
        /// under "Item number", while the other types do the reverse. Both are
        /// consulted so no type ends up nameless in the mapping grid.
        /// </summary>
        public string Product
        {
            get
            {
                if (!string.IsNullOrEmpty(ArticleName))
                {
                    return ArticleName;
                }

                if (!string.IsNullOrEmpty(ItemNumber))
                {
                    return ItemNumber;
                }

                return Fitting ?? string.Empty;
            }
        }

        /// <summary>One line for the mapping grid, so the user can see what the type is.</summary>
        public string Description
        {
            get
            {
                List<string> parts = new List<string>();
                if (!string.IsNullOrEmpty(Manufacturer))
                {
                    parts.Add(Manufacturer);
                }

                string product = Product;
                if (!string.IsNullOrEmpty(product))
                {
                    parts.Add(product);
                }

                if (!string.IsNullOrEmpty(Fitting) && !parts.Contains(Fitting))
                {
                    parts.Add(Fitting);
                }

                string head = string.Join(" / ", parts.ToArray());

                List<string> extras = new List<string>();
                if (!string.IsNullOrEmpty(LuminousFlux))
                {
                    extras.Add(LuminousFlux);
                }

                if (!string.IsNullOrEmpty(ConnectedLoad))
                {
                    extras.Add(ConnectedLoad);
                }

                if (extras.Count == 0)
                {
                    return head;
                }

                return head + " - " + string.Join(" - ", extras.ToArray());
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", Index, Description);
        }
    }
}
