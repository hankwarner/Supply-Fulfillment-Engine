using Xunit;
using FergusonSourcingEngine.Controllers;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Tests.Unit
{
    public class ShippingControllerUnitTests
    {
        private static ILogger _logger;
        private static ItemController itemController = new ItemController(_logger);
        private static ShippingController shippingController = new ShippingController(_logger, itemController);

        public ShippingAddress shipTo { get; set; }
        public ShippingAddress shipFrom { get; set; }

        public ShippingControllerUnitTests()
        {
            shipTo = new ShippingAddress()
            {
                addressLine1 = "2294 Cloverdale Dr",
                city = "Atlanta",
                state = "GA",
                zip = "30316"
            };

            shipFrom = new ShippingAddress()
            {
                addressLine1 = "13890 Lowe St",
                city = "Chantilly",
                state = "VA",
                zip = "20151"
            };
        }

        [Fact]
        public void Test_EstimateShippingCost()
        {
            SourcingEngine.atgOrderReq = new AtgOrderReq() { notes = "Standard" };
            var estShippingCost = shippingController.EstimateShippingCost(25, shipTo, shipFrom);

            Assert.Equal(8.72, estShippingCost);
        }
    }
}
