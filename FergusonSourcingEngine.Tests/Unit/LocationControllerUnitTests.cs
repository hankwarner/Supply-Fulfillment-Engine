using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Unit
{
    public class LocationControllerUnitTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);
        private static ShippingController shippingController = new ShippingController(_logger, itemController);
        private static LocationController locationController = new LocationController(_logger);
        private static SourcingController sourcingController = new SourcingController(_logger, itemController, locationController, shippingController);

        private static Location dcLocation = new Location()
        {
            FedExESTCutoffTimes = "23:59:00.0000000",
            ProcessingTime = 0,
            SaturdayDelivery = true
        };
        private static Location branchLocation = new Location()
        {
            FedExESTCutoffTimes = "00:01:00.0000000",
            ProcessingTime = 1
        };

        public LocationControllerUnitTests()
        {
            itemController.mpns = new List<string>() { "37572", "8050064" };

            // Create entries for each MPN from the payload
            itemController.mpns.ForEach(mpn =>
            {
                itemController.inventory.InventoryDict.TryAdd(mpn, new ItemInventory());
            });
        }


        [Fact]
        public void Test_GetBranchLogonID()
        {
            var sellingWarehouse = "107";

            var branchLogonID = locationController.GetBranchLogonID(sellingWarehouse);

            Assert.Equal("D20 E BLENDED FLORIDA", branchLogonID);
        }


        [Fact]
        public void Test_GetEstShipDate_Today()
        {
            var estShipDate = locationController.GetEstShipDate(dcLocation, DateTime.Now);

            Assert.Equal(DateTime.Now.Date, estShipDate);
        }

        [Fact]
        public void Test_GetEstShipDate_Holiday()
        {
            var estShipDate = locationController.GetEstShipDate(dcLocation, DateTime.Parse("Sep 7, 2020"));

            Assert.Equal(DateTime.Parse("Sep 8, 2020"), estShipDate);
        }

        [Fact]
        public void Test_GetEstShipDate_Weekend()
        {
            var estShipDate = locationController.GetEstShipDate(dcLocation, DateTime.Parse("Oct 24, 2020")); // friday

            Assert.Equal(DateTime.Parse("Oct 26, 2020"), estShipDate); // monday
        }


        [Fact]
        public void Test_GetEstShipDate_HolidayAndWeekend()
        {
            var estShipDate = locationController.GetEstShipDate(dcLocation, DateTime.Parse("Dec 25, 2020"));

            Assert.Equal(DateTime.Parse("Dec 28, 2020"), estShipDate);
        }


        [Fact]
        public void Test_GetEstShipDate_PastCutoff()
        {
            var estShipDate = locationController.GetEstShipDate(branchLocation, DateTime.Parse("Oct 27, 2020"));

            Assert.Equal(DateTime.Parse("Oct 28, 2020"), estShipDate);
        }


        [Fact]
        public void Test_GetEstShipDate_ProcessingTime()
        {
            branchLocation.FedExESTCutoffTimes = "23:59:00.0000000";

            var estShipDate = locationController.GetEstShipDate(branchLocation, DateTime.Parse("Oct 27, 2020"));

            Assert.Equal(DateTime.Parse("Oct 28, 2020"), estShipDate);
        }


        [Fact]
        public void Test_GetEstDeliveryDate()
        {
            branchLocation.BusinessDaysInTransit = 2;
            branchLocation.EstShipDate = DateTime.Parse("Oct 27, 2020");

            var estDeliveryDate = locationController.GetEstDeliveryDate(branchLocation);

            Assert.Equal(DateTime.Parse("Oct 29, 2020"), estDeliveryDate);
        }


        [Fact]
        public void Test_GetEstDeliveryDate_Holiday()
        {
            branchLocation.BusinessDaysInTransit = 1;
            branchLocation.EstShipDate = DateTime.Parse("Sep 4, 2020");

            var estDeliveryDate = locationController.GetEstDeliveryDate(branchLocation);

            Assert.Equal(DateTime.Parse("Sep 8, 2020"), estDeliveryDate);
        }


        [Fact]
        public void Test_GetEstDeliveryDate_Weekend()
        {
            branchLocation.BusinessDaysInTransit = 4;
            branchLocation.EstShipDate = DateTime.Parse("Oct 27, 2020");

            var estDeliveryDate = locationController.GetEstDeliveryDate(branchLocation);

            Assert.Equal(DateTime.Parse("Nov 2, 2020"), estDeliveryDate);
        }


        [Fact]
        public void Test_GetDistanceData()
        {
            locationController.locations.LocationDict.Add("423", new Location());
            locationController.locations.LocationDict.Add("2911", new Location());

            var distanceData = locationController.GetDistanceData("30316");

            Assert.Equal(600, distanceData["423"]);
            Assert.Equal(591, distanceData["2911"]);
        }


        [Fact]
        public void Test_AddDistanceDataToLocationDict()
        {
            var frontRoyal = "423";
            var celina = "2911";

            locationController.locations.LocationDict.Add(frontRoyal, new Location());
            locationController.locations.LocationDict.Add(celina, new Location());

            var distanceData = new Dictionary<string, double>();
            distanceData.Add(frontRoyal, 600);
            distanceData.Add(celina, 591);

            locationController.AddDistanceDataToLocationDict(distanceData);

            Assert.Equal(600, locationController.locations.LocationDict[frontRoyal].Distance);
            Assert.Equal(591, locationController.locations.LocationDict[celina].Distance);
        }


        [Fact]
        public void Test_SetPreferredLocationFlag()
        {
            locationController.SetPreferredLocationFlag("CA", "90210");

            Assert.True(locationController.locations.LocationDict["688"].IsPreferred);
        }


        [Fact]
        public void Test_SortLocations()
        {
            var locationDict = locationController.locations.LocationDict;

            var shortestLeadTimeLocation = new Location()
            {
                BranchNumber = "423",
                Distance = 100,
                WarehouseManagementSoftware = true,
                IsPreferred = false,
                EstDeliveryDate = Convert.ToDateTime("07/2/1988")
            };
            locationDict.Add("423", shortestLeadTimeLocation);

            var preferredLocation = new Location()
            {
                BranchNumber = "533",
                Distance = 150,
                WarehouseManagementSoftware = false,
                IsPreferred = true,
                EstDeliveryDate = Convert.ToDateTime("07/5/1988")
            };
            locationDict.Add("533", preferredLocation);

            var location3 = new Location()
            {
                BranchNumber = "474",
                Distance = 50,
                WarehouseManagementSoftware = true,
                IsPreferred = false,
                EstDeliveryDate = Convert.ToDateTime("07/3/1988")
            };
            locationDict.Add("474", location3);

            locationController.SortLocations();

            var firstLocation = locationController.locations.LocationDict.First();
            var lastLocation = locationController.locations.LocationDict.Last();

            Assert.Equal(preferredLocation.BranchNumber, firstLocation.Key);
            Assert.Equal(location3.BranchNumber, lastLocation.Key);
        }


        [Fact]
        public void Test_SetAvailableLocationsOnAllLines()
        {
            SourcingEngine.allLines = new AllLines();

            var dcLine = new SingleLine(123456, 8, 0);
            var dcLines = new List<SingleLine>() { dcLine };
            SourcingEngine.allLines.lineDict.Add("DC", dcLines);

            var feiLine = new SingleLine(789654, 10, 1);
            var feiLines = new List<SingleLine>() { feiLine };
            SourcingEngine.allLines.lineDict.Add("FEI", feiLines);

            var locationDict = locationController.locations.LocationDict;

            var dcLocation = new Location();
            dcLocation.DCLocation = true;
            locationDict.Add("423", dcLocation);

            var branchLocation = new Location();
            branchLocation.BranchLocation = true;
            locationDict.Add("27", branchLocation);

            locationController.SetAvailableLocationsOnAllLines();

            Assert.Single(SourcingEngine.allLines.lineDict["DC"]);


            SourcingEngine.allLines.lineDict["DC"].ForEach(l =>
            {
                var branchNumbers = l.Locations.Select(x => x.Key);

                Assert.Single(branchNumbers);
            });

            SourcingEngine.allLines.lineDict["FEI"].ForEach(l =>
            {
                var branchNumbers = l.Locations.Select(x => x.Key);

                Assert.Equal(2, branchNumbers.Count());
            });
        }
    }
}
