using System;
using System.Collections.Generic;

namespace FergusonSourcingCore.Models
{
    public class AllItems
    {
        // Key will be Master Product Number
        public Dictionary<string, ItemData> ItemDict = new Dictionary<string, ItemData>();
    }


    public class ItemData
    {
        public string MPN { get; set; }

        public string ALT1Code { get; set; }

        public string ItemCategory { get; set; }

        public string ItemDescription { get; set; }

        public string Manufacturer { get; set; }

        public string Vendor { get; set; }

        public bool BulkPack { get; set; }

        public int BulkPackQuantity { get; set; }

        public double Weight { get; set; }

        public string SourcingGuideline { get; set; }

        public string PreferredShippingMethod { get; set; } // value from the items table

        // Parses the number used in the preferred shipping method, i.e. "Ground4LTL" will return 4
        public int GroundQuantityThreshold
        {
            get
            {
                var thresholdQty = "";

                for (int i = 0; i < PreferredShippingMethod.Length; i++)
                {
                    if (char.IsDigit(PreferredShippingMethod[i]))
                    {
                        thresholdQty += PreferredShippingMethod[i];
                    }
                }

                if (thresholdQty == "") return 0;

                return int.Parse(thresholdQty);
            }
        }

        // Requirements
        public bool OverpackRequired { get; set; }
    }



    public class ItemResponse
    {
        public Dictionary<string, ItemData> ItemDataDict { get; set; } = new Dictionary<string, ItemData>();

        public Dictionary<string, Dictionary<string, bool>> StockingStatusDict { get; set; } = new Dictionary<string, Dictionary<string, bool>>();
    }
}