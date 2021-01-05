using Xunit;
using FergusonSourcingCore.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using RestSharp;
using Newtonsoft.Json;

namespace FergusonSourcingEngine.Tests.End_to_End
{
    public class SourcingEngineEndToEndTest
    {
        [Fact]
        public void Test_SourceOrder_NonBrokenBulkPack_MultiItem()
        {
            var sellWhse = "3503";
            var shipPrice = "100.0";
            var shipType = "Standard";
            var orderId = "Multi item Bulk Pack quantity example should be DC eligible";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "1995 Poplar Ridge Road",
                city = "Aurora",
                state = "NY",
                zip = "13026"
            };

            var item1 = new Item() { masterProdId = "6085241", quantity = "40" };
            var item2 = new Item() { masterProdId = "2050450", quantity = "2" };
            var items = new List<Item>() { item1, item2 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("2920", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Equal("533", response.items.FirstOrDefault(i => i.masterProdId == item2.masterProdId).shipFrom);
            Assert.Contains("Order has multiple sources", response.sourcingMessage);
        }


        [Fact]
        public void Test_SourceOrder_NonBrokenBulkPack()
        {
            var sellWhse = "3503";
            var shipPrice = "5.0";
            var shipType = "Standard";
            var orderId = "Bulk Pack quantity example should be DC eligible";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "1995 Poplar Ridge Road",
                city = "Aurora",
                state = "NY",
                zip = "13026"
            };

            var item1 = new Item() { masterProdId = "6085241", quantity = "20" };
            var items = new List<Item>() { item1 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("2920", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
        }


        [Fact]
        public void Test_SourceOrder_BrokenBulkPack_MultiItem()
        {
            var sellWhse = "3503";
            var shipPrice = "50.0";
            var shipType = "Standard";
            var orderId = "Multi item Bulk Pack quantity example Branch";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "1995 Poplar Ridge Road",
                city = "Aurora",
                state = "NY",
                zip = "13026"
            };

            var item1 = new Item() { masterProdId = "6085241", quantity = "38" };
            var item2 = new Item() { masterProdId = "2050450", quantity = "1" };
            var items = new List<Item>() { item1, item2 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("3020", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Equal("361", response.items.FirstOrDefault(i => i.masterProdId == item2.masterProdId).shipFrom);
            Assert.Contains("Order has multiple sources", response.sourcingMessage);
        }


        [Fact]
        public void Test_SourceOrder_BrokenBulkPack()
        {
            var sellWhse = "3503";
            var shipPrice = "50.0";
            var shipType = "Standard";
            var orderId = "Non-Bulk Pack quantity example";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "1995 Poplar Ridge Road",
                city = "Aurora",
                state = "NY",
                zip = "13026"
            };

            var item1 = new Item() { masterProdId = "6085241", quantity = "18" };
            var items = new List<Item>() { item1 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("114", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
        }


        [Fact]
        public void Test_SourceOrder_BranchAndFEI_Florida()
        {
            var sellWhse = "107";
            var shipPrice = "15.0";
            var shipType = "Standard";
            var orderId = "Branch and DC item in Florida Logon";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "4715 Frederick Dr. SW",
                city = "Atlanta",
                state = "GA",
                zip = "30336"
            };

            var item1 = new Item() { masterProdId = "7297432", quantity = "38" };
            var item2 = new Item() { masterProdId = "3764920", quantity = "1" };
            var items = new List<Item>() { item1, item2 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("533", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Equal("107", response.items.FirstOrDefault(i => i.masterProdId == item2.masterProdId).shipFrom);
            Assert.Contains("Order has multiple sources", response.sourcingMessage);
        }


        [Fact]
        public void Test_SourceOrder_DCStockItem_Florida()
        {
            var sellWhse = "107";
            var shipPrice = "15.0";
            var shipType = "Standard";
            var orderId = "DC stock item FLorida Logon";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "4715 Frederick Dr. SW",
                city = "Atlanta",
                state = "GA",
                zip = "30336"
            };

            var item1 = new Item() { masterProdId = "7949210", quantity = "15" };
            var items = new List<Item>() { item1 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("761", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
        }


        [Fact]
        public void Test_SourceOrder_VendorDirect()
        {
            var sellWhse = "3020";
            var shipPrice = "25.0";
            var shipType = "Standard";
            var orderId = "Vendor Direct 2.0";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "2101 Pennsylvania Ave",
                city = "Philadelphia",
                state = "PA",
                zip = "19130"
            };

            var item1 = new Item() { masterProdId = "7035783", quantity = "5" };
            var items = new List<Item>() { item1 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("3020", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Contains("sourced direct", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).sourcingMessage);
        }


        [Fact]
        public void Test_SourceOrder_VendorDirectAndDC()
        {
            var sellWhse = "3020";
            var shipPrice = "25.0";
            var shipType = "Standard";
            var orderId = "Vendor Direct and DC Item";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "2101 Pennsylvania Ave",
                city = "Philadelphia",
                state = "PA",
                zip = "19130"
            };

            var item1 = new Item() { masterProdId = "7035783", quantity = "5" };
            var item2 = new Item() { masterProdId = "7949210", quantity = "1" };
            var items = new List<Item>() { item1, item2 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("3020", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Contains("sourced direct", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).sourcingMessage);

            Assert.Equal("2920", response.items.FirstOrDefault(i => i.masterProdId == item2.masterProdId).shipFrom);
        }


        [Fact]
        public void Test_SourceOrder_LargeOrder()
        {
            var sellWhse = "216";
            var shipPrice = "500.0";
            var shipType = "Standard";
            var orderId = "Large Order";

            var shipTo = new ShippingAddress()
            {
                addressLine1 = "1995 Poplar Ridge Road",
                city = "Wichita",
                state = "KS",
                zip = "67213"
            };

            var item1 = new Item() { masterProdId = "5060790", quantity = "25" };
            var item2 = new Item() { masterProdId = "4377954", quantity = "186" };
            var item3 = new Item() { masterProdId = "7232009", quantity = "3" };
            var item4 = new Item() { masterProdId = "3469273", quantity = "1" };
            var item5 = new Item() { masterProdId = "7292287", quantity = "2" };
            var item6 = new Item() { masterProdId = "2880303", quantity = "2" };
            var items = new List<Item>() { item1, item2, item3, item4, item5, item6 };

            var order = CreateATGOrder(orderId, sellWhse, shipPrice, shipType, shipTo, items);

            var response = SourceOrderFromSite(order);

            Assert.Equal("474", response.items.FirstOrDefault(i => i.masterProdId == item1.masterProdId).shipFrom);
            Assert.Equal("474", response.items.FirstOrDefault(i => i.masterProdId == item2.masterProdId).shipFrom);
            Assert.Equal("986", response.items.FirstOrDefault(i => i.masterProdId == item3.masterProdId).shipFrom);
            Assert.Equal("986", response.items.FirstOrDefault(i => i.masterProdId == item4.masterProdId).shipFrom);
            Assert.Equal("474", response.items.FirstOrDefault(i => i.masterProdId == item5.masterProdId).shipFrom);
            Assert.Equal("423", response.items.FirstOrDefault(i => i.masterProdId == item6.masterProdId).shipFrom);
            Assert.Contains("Order has multiple sources", response.sourcingMessage);
        }


        public static AtgOrderReq CreateATGOrder(string orderId, string sellWhse, string shipPrice, string shipType, ShippingAddress shipTo, List<Item> items)
        {
            var order = new AtgOrderReq()
            {
                atgOrderId = orderId,
                shipFromWhse = sellWhse,
                sellWhse = sellWhse,
                notes = shipType,
                items = items,
                custAccountId = "POLLARDWTR",
                customerId = "54645",
                customerName = "Gene Parmesan",
                orderSubmitDate = "2020-07-30 08:16:58",
                sourceSystem = "B2S",
                orderRequiredDate = "2020-07-30",
                paymentOnAccount = new PaymentOnAccount()
                {
                    payment = new Payment()
                    {
                        address1 = "4715 Frederick Dr. SW",
                        cardType = "VI",
                        city = "Atlanta",
                        state = "GA",
                        zip = "30336"
                    }
                }
            };

            order.shipping = new Shipping()
            {
                price = shipPrice,
                shipTo = new ShipTo()
                {
                    address1 = shipTo.addressLine1,
                    city = shipTo.city,
                    state = shipTo.state,
                    zip = shipTo.zip,
                    country = "US",
                    name = "Gene Parmesan",
                    shipInstructionsPhoneNumberAreaDialing = "315",
                    shipInstructionsPhoneNumberDialNumber = "7295356"
                }
            };

            // Set static item fields
            for (var i = 0; i < order.items.Count; i++)
            {
                order.items[i].unitPriceCode = "EA";
                order.items[i].description = "ci8318000259";
            }

            return order;
        }

        public static AtgOrderRes SourceOrderFromSite(AtgOrderReq order)
        {
            var authCode = Environment.GetEnvironmentVariable("AZURE_SOURCE_ORDER_KEY");

            var baseURL = "https://fergusonsourcingengine.azurewebsites.net/api/order/source?code=";

            var jsonRequest = JsonConvert.SerializeObject(order);

            var client = new RestClient(baseURL + authCode);

            var request = new RestRequest(Method.POST);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json; charset=utf-8", jsonRequest, ParameterType.RequestBody);

            var jsonResponse = client.Execute(request).Content;

            var orderResponse = JsonConvert.DeserializeObject<AtgOrderRes>(jsonResponse);

            return orderResponse;
        }
    }
}
