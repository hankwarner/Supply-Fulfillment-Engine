using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Integration
{
    public class ItemControllerIntegrationTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);

        public ItemControllerIntegrationTests()
        {
            itemController.mpns = new List<string>() { "37572", "8050064" };

            // Create entries for each MPN from the payload
            itemController.mpns.ForEach(mpn =>
            {
                itemController.inventory.InventoryDict.TryAdd(mpn, new ItemInventory());
            });
        }

        [Fact]
        public void Test_InitializeInventory()
        {
            itemController.InitializeInventory();

            Assert.Equal(2, itemController.inventory.InventoryDict.Count());
            Assert.Equal(1078, itemController.inventory.InventoryDict["37572"].Available.Count());
            Assert.Equal(7, itemController.inventory.InventoryDict["8050064"].Available.Count());
        }

    }
}

