using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Integration
{
    public class SourcingControllerIntegrationTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);
        private static ShippingController shippingController = new ShippingController(_logger, itemController);
        private static LocationController locationController = new LocationController(_logger, shippingController);
        private static SourcingController sourcingController = new SourcingController(_logger, itemController, locationController, shippingController);

        public static List<int> mpns = new List<int>() { 3951216, 7507761, 171882 };
        public static SingleLine line1 = new SingleLine(mpns[0], 1, 0);
        public static SingleLine line2 = new SingleLine(mpns[1], 1, 1);
        public static SingleLine line3 = new SingleLine(mpns[2], 1, 2);

        public static Item orderItem1 = new Item() { masterProdId = mpns[0].ToString() };
        public static Item orderItem2 = new Item() { masterProdId = mpns[1].ToString() };
        public static Item orderItem3 = new Item() { masterProdId = mpns[2].ToString() };

        public static Location frontRoyal = CreateLocation("423");
        public static Location mcGregor = CreateLocation("474");
        public static Location fortPayne = CreateLocation("533");
        public static Location frostProof = CreateLocation("761");
        public static List<string> sortedLocationsList = new List<string>() { frontRoyal.BranchNumber, mcGregor.BranchNumber, fortPayne.BranchNumber };

        public SourcingControllerIntegrationTests()
        {
            SourcingEngine.atgOrderReq = new AtgOrderReq();

            // Add ship to address on the order
            var shipTo = new ShipTo()
            {
                address1 = "2294 Cloverdale Dr",
                city = "Atlanta",
                state = "GA",
                zip = "30316"
            };
            SourcingEngine.atgOrderReq.shipping = new Shipping() { shipTo = shipTo };

            // Add location shipping addresses
            frontRoyal.Address1 = "620 Fairground Rd";
            frontRoyal.City = "Front Royal";
            frontRoyal.State = "VA";
            frontRoyal.Zip = "22630";

            mcGregor.Address1 = "2055 S Main St";
            mcGregor.City = "McGregor";
            mcGregor.State = "TX";
            mcGregor.Zip = "76657";

            fortPayne.Address1 = "2500 Jordan Rd S";
            fortPayne.City = "Fort Payne";
            fortPayne.State = "AL";
            fortPayne.Zip = "35968";

            frostProof.Address1 = "1225 Scenic Hwy S";
            frostProof.City = "Frostproof";
            frostProof.State = "FL";
            frostProof.Zip = "33843-9201";

            locationController.locations.LocationDict.Add("423", frontRoyal);
            locationController.locations.LocationDict.Add("474", mcGregor);
            locationController.locations.LocationDict.Add("533", fortPayne);
            locationController.locations.LocationDict.Add("761", frostProof);

        }

        [Fact]
        public void Test_SourceByLine_NoBackOrder()
        {
            var items = new List<Item>() { orderItem1, orderItem2, orderItem3 };
            SourcingEngine.atgOrderReq.items = items;

            itemController.items.ItemDict.Add(orderItem1.masterProdId, new ItemData());
            itemController.items.ItemDict.Add(orderItem2.masterProdId, new ItemData());
            itemController.items.ItemDict.Add(orderItem3.masterProdId, new ItemData());

            itemController.items.ItemDict[orderItem1.masterProdId].Weight = 15;
            itemController.items.ItemDict[orderItem2.masterProdId].Weight = 5;
            itemController.items.ItemDict[orderItem3.masterProdId].Weight = 10.3;

            line1.Quantity = 10;
            AddLocations(line1, sortedLocationsList);
            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 20, mpns[0]);

            line2.Quantity = 15;
            AddLocations(line2, sortedLocationsList);
            AddInventory(frontRoyal.BranchNumber, 100, mpns[1]);
            AddInventory(mcGregor.BranchNumber, 90, mpns[1]);
            AddInventory(fortPayne.BranchNumber, 20, mpns[1]);

            line3.Quantity = 30;
            AddLocations(line3, sortedLocationsList);
            AddInventory(frontRoyal.BranchNumber, 29, mpns[2]);
            AddInventory(mcGregor.BranchNumber, 50, mpns[2]);
            AddInventory(fortPayne.BranchNumber, 300, mpns[2]);

            var currentLines = new List<SingleLine>() { line1, line2, line3 };

            sourcingController.SourceByLine(currentLines);

            Assert.Equal(fortPayne.BranchNumber, SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 0).shipFrom);
            Assert.Equal(frontRoyal.BranchNumber, SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 1).shipFrom);
            Assert.Equal(mcGregor.BranchNumber, SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 2).shipFrom);
        }

        [Fact]
        public void Test_SourceByLine_BackOrderToClosestStockingLocation()
        {
            var items = new List<Item>() { orderItem1 };
            SourcingEngine.atgOrderReq.items = items;

            itemController.items.ItemDict.Add(int.Parse(orderItem1.masterProdId), new ItemData());
            itemController.items.ItemDict[int.Parse(orderItem1.masterProdId)].Weight = 15;

            line1.Quantity = 10;

            AddLocations(line1, sortedLocationsList);

            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 2, mpns[0]);

            AddStockingStatus(frontRoyal.BranchNumber, false, mpns[0]);
            AddStockingStatus(mcGregor.BranchNumber, true, mpns[0]);
            AddStockingStatus(fortPayne.BranchNumber, true, mpns[0]);

            var currentLines = new List<SingleLine>() { line1 };

            sourcingController.SourceByLine(currentLines);

            Assert.Equal(mcGregor.BranchNumber, SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 0).shipFrom);
            Assert.Contains("No available locations", SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 0).sourcingMessage);
        }


        [Fact]
        public void Test_SourceByLine_BackOrderToClosestLocation()
        {
            var items = new List<Item>() { orderItem1 };
            SourcingEngine.atgOrderReq.items = items;

            itemController.items.ItemDict.Add(int.Parse(orderItem1.masterProdId), new ItemData());
            itemController.items.ItemDict[int.Parse(orderItem1.masterProdId)].Weight = 15;

            line1.Quantity = 10;

            AddLocations(line1, sortedLocationsList);

            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 2, mpns[0]);

            AddStockingStatus(frontRoyal.BranchNumber, false, mpns[0]);
            AddStockingStatus(mcGregor.BranchNumber, false, mpns[0]);
            AddStockingStatus(fortPayne.BranchNumber, false, mpns[0]);

            var currentLines = new List<SingleLine>() { line1 };

            sourcingController.SourceByLine(currentLines);

            Assert.Equal(frontRoyal.BranchNumber, SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 0).shipFrom);
            Assert.Contains("No available locations", SourcingEngine.orderRes.items.FirstOrDefault(i => i.lineId == 0).sourcingMessage);
        }

        [Fact]
        public void Test_SourceLinesComplete_Successful()
        {
            line1.Quantity = 1;
            AddLocations(line1, sortedLocationsList);

            line2.Quantity = 1;
            sortedLocationsList.RemoveAt(0);
            AddLocations(line2, sortedLocationsList);

            sortedLocationsList.Add(frostProof.BranchNumber);
            line3.Quantity = 1;
            AddLocations(line3, sortedLocationsList);

            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 0, mpns[0]);

            AddInventory(frontRoyal.BranchNumber, 1, mpns[1]);
            AddInventory(mcGregor.BranchNumber, 10, mpns[1]);
            AddInventory(fortPayne.BranchNumber, 7, mpns[1]);

            AddInventory(frontRoyal.BranchNumber, 4, mpns[2]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[2]);
            AddInventory(fortPayne.BranchNumber, 3, mpns[2]);

            var currentLines = new List<SingleLine>() { line1, line2, line3 };

            var linesToBeSourced = new LinesToBeSourced();
            linesToBeSourced.CurrentLines = currentLines;

            sourcingController.SourceLinesComplete(linesToBeSourced, "FEI");

            Assert.Equal(mcGregor.BranchNumber, linesToBeSourced.SourcedLines[0].ShipFrom);
            Assert.Equal(mcGregor.BranchNumber, linesToBeSourced.SourcedLines[1].ShipFrom);
            Assert.Equal(mcGregor.BranchNumber, linesToBeSourced.SourcedLines[2].ShipFrom);
        }

        [Fact]
        public void Test_SourceLinesComplete_BackOrder()
        {
            AddLocations(line1, sortedLocationsList);
            AddLocations(line2, sortedLocationsList);
            AddLocations(line3, sortedLocationsList);

            line1.Quantity = 18;
            line2.Quantity = 1;
            line3.Quantity = 1;

            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 0, mpns[0]);

            AddInventory(frontRoyal.BranchNumber, 1, mpns[1]);
            AddInventory(mcGregor.BranchNumber, 10, mpns[1]);
            AddInventory(fortPayne.BranchNumber, 7, mpns[1]);

            AddInventory(frontRoyal.BranchNumber, 4, mpns[2]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[2]);
            AddInventory(fortPayne.BranchNumber, 3, mpns[2]);

            var currentLines = new List<SingleLine>() { line1, line2, line3 };

            var linesToBeSourced = new LinesToBeSourced();
            linesToBeSourced.CurrentLines = currentLines;

            sourcingController.SourceLinesComplete(linesToBeSourced, "FEI");

            Assert.Equal(frontRoyal.BranchNumber, linesToBeSourced.SourcedLines[0].ShipFrom);
            Assert.Equal(9, linesToBeSourced.SourcedLines[0].QuantityBackordered);
            Assert.Equal(frontRoyal.BranchNumber, linesToBeSourced.SourcedLines[1].ShipFrom);
            Assert.Equal(frontRoyal.BranchNumber, linesToBeSourced.SourcedLines[2].ShipFrom);
        }

        [Fact]
        public void Test_SourceLinesComplete_NoCommonLocations()
        {
            // Remove McGregor for lines 1 and 3
            sortedLocationsList.RemoveAt(1);
            AddLocations(line1, sortedLocationsList);
            AddLocations(line3, sortedLocationsList);

            // Remove all lines except McGregor for line 2
            AddLocations(line2, new List<string>() { mcGregor.BranchNumber });

            line1.Quantity = 1;
            line2.Quantity = 1;
            line3.Quantity = 1;

            AddInventory(frontRoyal.BranchNumber, 9, mpns[0]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[0]);
            AddInventory(fortPayne.BranchNumber, 0, mpns[0]);

            AddInventory(frontRoyal.BranchNumber, 1, mpns[1]);
            AddInventory(mcGregor.BranchNumber, 10, mpns[1]);
            AddInventory(fortPayne.BranchNumber, 7, mpns[1]);

            AddInventory(frontRoyal.BranchNumber, 4, mpns[2]);
            AddInventory(mcGregor.BranchNumber, 5, mpns[2]);
            AddInventory(fortPayne.BranchNumber, 3, mpns[2]);

            var currentLines = new List<SingleLine>() { line1, line2, line3 };

            var linesToBeSourced = new LinesToBeSourced();
            linesToBeSourced.CurrentLines = currentLines;

            sourcingController.SourceLinesComplete(linesToBeSourced, "FEI");

            Assert.Equal(frontRoyal.BranchNumber, linesToBeSourced.SourcedLines[0].ShipFrom);
            Assert.Equal(frontRoyal.BranchNumber, linesToBeSourced.SourcedLines[1].ShipFrom);
            Assert.Equal(mcGregor.BranchNumber, linesToBeSourced.SourcedLines[2].ShipFrom);
        }


        static void AddStockingStatus(string locationId, bool isStocking, int mpn)
        {
            var itemInventory = new ItemInventory();
            itemInventory.StockStatus.Add(locationId, isStocking);

            var res = itemController.inventory.InventoryDict.TryAdd(mpn, itemInventory);

            if (!res)
            {
                itemController.inventory.InventoryDict[mpn].StockStatus.Add(locationId, isStocking);
            }
        }


        static Location CreateLocation(string branchNumber)
        {
            var location = new Location();
            location.BranchNumber = branchNumber;

            return location;
        }


        static void AddLocations(SingleLine line, List<string> branchNumbers)
        {
            branchNumbers.ForEach((b) =>
            {
                var location = new Location();
                location.BranchNumber = b;
                line.Locations.Add(b, location);
            });
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
