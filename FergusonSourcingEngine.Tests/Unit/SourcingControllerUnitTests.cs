using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Unit
{
    public class SourcingControllerUnitTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);
        private static ShippingController shippingController = new ShippingController(_logger, itemController);
        private static LocationController locationController = new LocationController(_logger, shippingController);
        private static SourcingController sourcingController = new SourcingController(_logger, itemController, locationController, shippingController);

        public static List<int> mpns = new List<int>() { 3951216, 7507761, 171882 };
        public static SingleLine line1 = new SingleLine(mpns[0], 1, 0);
        public static Location frontRoyal = CreateLocation("423");
        public static Item item1 = new Item();
        public static Item item2 = new Item();
        public static Item item3 = new Item();


        public SourcingControllerUnitTests()
        {
            var items = new List<Item>() { item1, item2, item3 };

            SourcingEngine.atgOrderReq = new AtgOrderReq() { items = items };
        }

        [Fact]
        public void Test_GroupBySourcingGuide()
        {
            // Build the item dictionary
            var itemData1 = new ItemData()
            {
                SourcingGuideline = "DC",
                MPN = "123456"
            };

            var itemData2 = new ItemData()
            {
                MPN = "789654",
                SourcingGuideline = "FEI"
            };

            itemController.items.ItemDict.Add(itemData1.MPN, itemData1);
            itemController.items.ItemDict.Add(itemData2.MPN, itemData2);

            // Create a list of items to imitate the items on an incoming order
            var item1 = new Item()
            {
                masterProdId = "123456",
                quantity = "6"
            };

            var item2 = new Item()
            {
                masterProdId = "789654",
                quantity = "9"
            };
            var itemRes1 = new ItemRes(item1);
            var itemRes2 = new ItemRes(item2);

            var allLines = sourcingController.GroupBySourcingGuide(new List<ItemRes>() { itemRes1, itemRes2 });

            var itemLine1 = allLines.lineDict[itemData1.SourcingGuideline].FirstOrDefault(i => i.MasterProductNumber == itemData1.MPN);
            var itemLine2 = allLines.lineDict[itemData2.SourcingGuideline].FirstOrDefault(i => i.MasterProductNumber == itemData2.MPN);

            Assert.Equal(6, itemLine1.Quantity);
            Assert.Equal(9, itemLine2.Quantity);
        }


        [Fact]
        public void Test_GroupBySourcingGuide_BrokenBulkPack()
        {
            // Build the item dictionary
            var itemData1 = new ItemData()
            {
                MPN = "123456",
                SourcingGuideline = "Branch",
                BulkPack = true,
                BulkPackQuantity = 10
            };

            itemController.items.ItemDict.Add(itemData1.MPN, itemData1);

            // Create a list of items to imitate the items on an incoming order
            var item1 = new Item()
            {
                masterProdId = "123456",
                quantity = "15"

            };
            var itemRes1 = new ItemRes(item1);

            var allLines = sourcingController.GroupBySourcingGuide(new List<ItemRes>() { itemRes1 });

            var itemLine1 = allLines.lineDict["DC"].FirstOrDefault(i => i.MasterProductNumber == itemData1.MPN);

            Assert.Equal(15, itemLine1.Quantity);
        }


        [Fact]
        public void Test_ValidatePickupLocation()
        {
            // Craete pickup lines
            var line1 = new SingleLine(123456, 10, 0);
            var line2 = new SingleLine(789654, 50, 1);

            var branchLines = new List<SingleLine>() { line1 };
            var feiLines = new List<SingleLine>() { line2 };

            var allLines = new Dictionary<string, List<SingleLine>>()
            {
                { "FEI", feiLines },
                { "Branch", branchLines }
            };

            var pickupLocation = "107";

            // Add inventory
            var item1Inventory = new ItemInventory();
            item1Inventory.Available.Add(pickupLocation, 80);

            var item2Inventory = new ItemInventory();
            item2Inventory.Available.Add(pickupLocation, 3);

            itemController.inventory.InventoryDict.Add(123456, item1Inventory);
            itemController.inventory.InventoryDict.Add(789654, item2Inventory);

            var pickupLocationHasInventory = sourcingController.ValidatePickupInventory(pickupLocation, allLines);

            var noInventoryFEILines = feiLines
                .Where(l => sourcingController.GetOrderItemByLineId(l.LineId).sourcingMessage == "This pickup location does not have the required quantity of inventory for the order line.")
                .Select(l => l);

            var noInventoryBranchLines = branchLines
                .Where(l => sourcingController.GetOrderItemByLineId(l.LineId).sourcingMessage == "This pickup location does not have the required quantity of inventory for the order line.")
                .Select(l => l);

            Assert.False(pickupLocationHasInventory);
            Assert.Empty(noInventoryBranchLines);
            Assert.Single(noInventoryFEILines);
        }


        [Fact]
        public void Test_GetClosestStockingLocation()
        {
            // Create list of branches
            var branchNumbers = new List<string>() { "423", "474", "533" };

            // Add stocking statuses
            var mpn1 = "123456";
            itemController.inventory.InventoryDict.Add(mpn1, new ItemInventory());

            var mpn1StockingStatusDict = itemController.inventory.InventoryDict[mpn1].StockStatus;
            mpn1StockingStatusDict.Add("423", false);
            mpn1StockingStatusDict.Add("474", true);
            mpn1StockingStatusDict.Add("533", false);

            var closestStockingLocation = locationController.GetClosestStockingLocation(branchNumbers, mpn1);

            Assert.Equal("474", closestStockingLocation);
        }


        [Fact]
        public void Test_GetOrderItemByLineId()
        {
            Assert.Equal(item1, sourcingController.GetOrderItemByLineId(0));
            Assert.Equal(item2, sourcingController.GetOrderItemByLineId(1));
            Assert.Equal(item3, sourcingController.GetOrderItemByLineId(2));
        }


        [Fact]
        public void Test_BackOrderToLocation()
        {
            line1.Quantity = 10;
            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);

            sourcingController.BackOrderToLocation(line1, frontRoyal.BranchNumber);

            var orderItem = SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == line1.LineId);

            Assert.Equal(orderItem.shipFrom, frontRoyal.BranchNumber);
            Assert.Contains("No available locations", orderItem.sourcingMessage);
            Assert.Equal(9, line1.ShipFromInventory);
            Assert.Equal(1, line1.QuantityBackordered);
        }


        static Location CreateLocation(string branchNumber)
        {
            var location = new Location();
            location.BranchNumber = branchNumber;

            return location;
        }


        static void AddInventory(string locationId, int quantity, int mpn)
        {
            var itemInventory = new ItemInventory();
            itemInventory.Available.Add(locationId, quantity);

            var res = itemController.inventory.InventoryDict.TryAdd(mpn, itemInventory);

            if (!res)
            {
                itemController.inventory.InventoryDict[mpn].Available.Add(locationId, quantity);
            }

        }
    }
}
