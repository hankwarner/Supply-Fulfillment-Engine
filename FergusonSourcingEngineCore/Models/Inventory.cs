using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace FergusonSourcingCore.Models
{
    public class Inventory
    {
        // Key = mpn
        public Dictionary<string, ItemInventory> InventoryDict = new Dictionary<string, ItemInventory>();
    }


    public class ItemInventory
    {
        public Dictionary<string, int> Available = new Dictionary<string, int>();

        public Dictionary<string, int> MultiLineAvailable = new Dictionary<string, int>();

        public Dictionary<string, bool> StockStatus = new Dictionary<string, bool>();
    }


    public class DOMInventoryRequest
    {
        public DOMInventoryRequest(List<string> masterProductNumbers)
        {
            MasterProductNumbers = masterProductNumbers;
        }

        [JsonProperty("masterProductNumbers")]
        public List<string> MasterProductNumbers { get; set; }
    }


    public class InventoryResponse
    {
        public List<Dictionary<string, string>> InventoryData = new List<Dictionary<string, string>>();
    }
}
