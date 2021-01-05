using System;
using System.Collections.Generic;

namespace FergusonSourcingCore.Models
{
    public class AllLines
    {
        // Key = sourcing guide
        public Dictionary<string, List<SingleLine>> lineDict = new Dictionary<string, List<SingleLine>>();
    }


    public class SingleLine
    {
        public SingleLine(string mpn, int quantity, int lineId, string sourcingGuide)
        {
            MasterProductNumber = mpn;
            Quantity = quantity;
            LineId = lineId;
            SourcingGuide = sourcingGuide;
        }
        
        public string MasterProductNumber { get; set; }

        public int Quantity { get; set; }

        public int LineId { get; set; }

        public string ShipFrom { get; set; }

        public int ShipFromInventory { get; set; }

        public int QuantityAvailable { get; set; }

        public int QuantityBackordered { get; set; } = 0;

        public string SourcingGuide { get; set; }

        public string SourcingMessage { get; set; }

        public bool isMultiLineItem { get; set; }

        // Key = branch number
        public Dictionary<string, Location> Locations = new Dictionary<string, Location>();

        public Dictionary<string, bool> Requirements { get; set; } = new Dictionary<string, bool>();
    }
}
