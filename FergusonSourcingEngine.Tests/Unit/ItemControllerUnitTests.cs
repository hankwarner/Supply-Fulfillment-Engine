using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Unit
{
    public class ItemControllerUnitTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);
        private static ShippingController shippingController = new ShippingController(_logger, itemController);
        private static LocationController locationController = new LocationController(_logger, shippingController);
        private static SourcingController sourcingController = new SourcingController(_logger, itemController, locationController, shippingController);

        public ItemControllerUnitTests()
        {
            itemController.mpns = new List<string>() { "1704368", "6574716" };
        }

        [Fact]
        public void Test_AddItemDataToDict()
        {
            var itemData = new ItemData();
            itemData.MPN = 123456;
            itemData.SourcingGuideline = "DC";

            var itemDataResponse = new Dictionary<string, ItemData>();
            itemDataResponse.Add(itemData.MPN.ToString(), itemData);

            itemController.AddItemDataToDict(itemDataResponse);

            var itemDict = itemController.items.ItemDict;

            Assert.Equal(itemDict[itemData.MPN].SourcingGuideline, itemData.SourcingGuideline);
        }

        [Fact]
        public void Test_InitializeInventoryDict()
        {
            itemController.InitializeInventoryDict();

            var inventoryDict = itemController.inventory.InventoryDict;

            Assert.True(inventoryDict.TryGetValue(37572, out ItemInventory itemInventory1));
            Assert.True(inventoryDict.TryGetValue(8050064, out ItemInventory itemInventory2));
        }

        [Fact]
        public void Test_AddStockingStatusesToInventoryDict()
        {
            var itemData1 = new ItemData()
            {
                MPN = int.Parse(itemController.mpns[0]),
                StockingStatus2911 = true
            };

            var itemData2 = new ItemData()
            {
                MPN = int.Parse(itemController.mpns[1]),
                StockingStatus2911 = true
            };

            itemController.items.ItemDict.Add(itemData1.MPN, itemData1);
            itemController.items.ItemDict.Add(itemData2.MPN, itemData2);

            // Create entries in the inventoryDict for each MPN from the payload
            itemController.mpns.ForEach(mpn =>
            {
                itemController.inventory.InventoryDict.TryAdd(int.Parse(mpn), new ItemInventory());
            });

            itemController.AddStockingStatusesToInventoryDict();

            Assert.True(itemController.inventory.InventoryDict[itemData1.MPN].StockStatus["2911"]);
            Assert.True(itemController.inventory.InventoryDict[itemData2.MPN].StockStatus["423"]);
        }


        [Fact]
        public void Test_GetInventoryData()
        {
            var inventoryResponse = itemController.GetInventoryData();

            Assert.Equal(2, inventoryResponse.Count);
        }


        [Fact]
        public void Test_ParseInventoryResponse()
        {
            var inventoryLine1 = new Dictionary<string, string>()
            {
                { "MPID", "37572" },
                { "1", "21" }
            };

            var inventoryLine2 = new Dictionary<string, string>()
            {
                { "MPID", "8050064" },
                { "251", "128" }
            };

            var inventoryData = new List<Dictionary<string, string>>() { inventoryLine1, inventoryLine2 };

            var jsonInventory = JsonConvert.SerializeObject(inventoryData);

            var inventoryResponse = JArray.Parse(jsonInventory);

            var domInventory = itemController.ParseInventoryResponse(inventoryResponse);

            domInventory.InventoryData.ForEach(dict =>
            {
                Assert.True(dict["MPID"] == "37572" || dict["MPID"] == "8050064");
            });

            Assert.Equal(2, domInventory.InventoryData.Count());
        }


        [Fact]
        public void Test_AddAvailableInventoryToDict()
        {
            itemController.mpns.ForEach(mpn =>
            {
                itemController.inventory.InventoryDict.TryAdd(int.Parse(mpn), new ItemInventory());
            });

            var inventoryLine1 = new Dictionary<string, string>()
            {
                { "MPID", "37572" },
                { "1", "21" }
            };

            var inventoryLine2 = new Dictionary<string, string>()
            {
                { "MPID", "8050064" },
                { "251", "128" }
            };

            var inventoryData = new List<Dictionary<string, string>>() { inventoryLine1, inventoryLine2 };

            itemController.AddAvailableInventoryToDict(inventoryData);

            Assert.Equal(21, itemController.inventory.InventoryDict[37572].Available["1"]);
            Assert.Equal(128, itemController.inventory.InventoryDict[8050064].Available["251"]);
        }


        [Fact]
        public void Test_AddAvailableInventory_FromDOMInventory()
        {
            var inventoryList = CreateInventoryList("DOM");

            itemController.AddAvailableInventoryToDict(inventoryList);

            var availableAtMcGregor = itemController.inventory
                .InventoryDict[int.Parse(itemController.mpns[0])]
                .Available["474"];

            var availableAtBeltsville = itemController.inventory
                .InventoryDict[int.Parse(itemController.mpns[1])]
                .Available["2"];

            Assert.Equal(709, availableAtMcGregor);
            Assert.Equal(0, availableAtBeltsville);
        }


        private List<Dictionary<string, string>> CreateInventoryList(string type)
        {
            var invLine1 = new Dictionary<string, string>()
            {
                { type == "DOM" ? "MPID" : "internalid", itemController.mpns[0] },
                { "1", "16" },
                { "2", "22" },
                { "474", "709" }
            };

            var invLine2 = new Dictionary<string, string>()
            {
                { type == "DOM" ? "MPID" : "internalid", itemController.mpns[1] },
                { "2", "0" }
            };

            return new List<Dictionary<string, string>>() { invLine1, invLine2 };
        }
    }
}
