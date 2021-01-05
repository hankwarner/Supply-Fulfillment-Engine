using System;
using FergusonSourcingCore.Models;
using RestSharp;
using Polly;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;

namespace FergusonSourcingEngine.Controllers
{
    public class ShippingController
    {
        private ILogger _logger;
        private ItemController itemController { get; set; }

        public ShippingController(ILogger logger, ItemController itemController)
        {
            _logger = logger;
            this.itemController = itemController;
        }

        public async Task<double> EstimateShippingCost(double weight, ShippingAddress shipTo, ShippingAddress shipFrom, AtgOrderRes atgOrderRes)
        {
            _logger.LogInformation("EstimateShippingCost start");
            var retryPolicy = Policy.Handle<Exception>().Retry(3, (ex, count) =>
            {
                var title = "Error in EstimateShippingCost";
                _logger.LogWarning($"{title}. Retrying...");

                if (count == 3)
                {
                    var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                    teamsMessage.LogToTeams(teamsMessage);
                    _logger.LogError(ex, title);
                }
            });

            return await retryPolicy.Execute(async () =>
            {
                var requestBody = new ShipQuoteRequest
                {
                    RateType = "Ground", // Default to Ground unless shipping next day or second day
                    OriginAddress = shipFrom,
                    DestinationAddress = shipTo,
                    Package = new Package(){ Weight = weight }
                };

                if (atgOrderRes.shipping.shipViaCode == "SECOND_DAY")
                {
                    requestBody.RateType = "Second Day Air";
                }
                else if (atgOrderRes.shipping.shipViaCode == "NEXT_DAY")
                {
                    requestBody.RateType = "Next Day Air";
                }

                var jsonRequest = JsonConvert.SerializeObject(requestBody);

                var baseUrl = "https://ups-microservices.azurewebsites.net/api/rating";
                var client = new RestClient(baseUrl);

                var request = new RestRequest(Method.POST)
                    .AddHeader("Content-Type", "application/json")
                    .AddQueryParameter("code", Environment.GetEnvironmentVariable("QUOTE_SHIPMENT_KEY"))
                    .AddParameter("application/json; charset=utf-8", jsonRequest, ParameterType.RequestBody);

                var response = await client.ExecuteAsync(request);
                _logger.LogInformation(@"Quote shipment response status code: {0}. Content: {1}", response.StatusCode, response.Content);

                if (response?.StatusCode != HttpStatusCode.OK)
                {
                    throw new ArgumentException("Est Shipping Cost returned bad request object.");
                }

                double.TryParse(response.Content, out double rate);
                _logger.LogInformation($"Branch {shipFrom.branchNumber} Estimated Shipping Cost: {rate}");

                if (rate == 0)
                {
                    var errorTitle = "Warning in EstimateShippingCost.";
                    var message = "Ship Quote returned $0 estimate.";
                    var teamsMessage = new TeamsMessage(errorTitle, message, "yellow", SourcingEngineFunctions.errorLogsUrl);
                    teamsMessage.LogToTeams(teamsMessage);
                    _logger.LogWarning(message);
                }

                _logger.LogInformation("EstimateShippingCost finish");
                return rate;
            });
        }


        public double GetCumulativeItemWeight(IGrouping<string, SingleLine> groupedLine, Dictionary<string, ItemData> itemDict)
        {
            var totalItemWeight = 0.0;

            try
            {
                foreach (var line in groupedLine)
                {
                    var mpn = line.MasterProductNumber;
                    var weight = itemDict[mpn].Weight;

                    totalItemWeight += weight;
                }
            }
            catch (Exception ex)
            {
                var errorTitle = "Error in GetCumulativeItemWeight";
                var teamsMessage = new TeamsMessage(errorTitle, $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(ex, errorTitle);
            }

            return totalItemWeight;
        }


        /// <summary>
        ///     Determines how the item will ship based on the item preferred ship method and quantity.
        /// </summary>
        /// <param name="prefShipMethod">The preferred shipping method of the item.</param>
        /// <param name="lineQty">Item quantity on the line.</param>
        /// <param name="mpn">Master Product Number of the item.</param>
        /// <returns>The method that the order will be shipped.</returns>
        public async Task<string> GetItemPreferredShipVia(string prefShipMethod, string mpn, int lineQty)
        {
            prefShipMethod = prefShipMethod.ToUpper();

            // Next day and 2nd day orders are hard-mapped to values
            if (prefShipMethod == "2ND DAY AIR") 
                return "UP2";

            if (prefShipMethod == "OVERNIGHT") 
                return "UNN";

            itemController.items.ItemDict.TryGetValue(mpn, out ItemData item);

            if (item == null)
                return "Item data not available.";

            // Handles LTL, Ground2LTL, and Ground4LTL
            if (prefShipMethod == "LTL" || ((prefShipMethod == "GROUND2LTL" || prefShipMethod == "GROUND4LTL") && lineQty > item.GroundQuantityThreshold))
                return "LTL";

            // Handles Ground and Shippers Choice
            if (prefShipMethod == "GROUND" || prefShipMethod.Contains("CHOICE") || lineQty <= item.GroundQuantityThreshold)
                return "UPS";

            return "N/A";
        }


        public string GetOrderShipVia(string shipViaCode)
        {
            string trilogieShipVia;

            switch (shipViaCode)
            {
                case "NEXT_DAY":
                    trilogieShipVia = "UNN";
                    break;
                case "SECOND_DAY":
                    trilogieShipVia = "UP2";
                    break;
                case "LTL":
                    trilogieShipVia = "LTL";
                    break;
                case "STANDARD":
                    trilogieShipVia = "UPS";
                    break;
                case "CPU":
                    trilogieShipVia = "WCL";
                    break;
                default:
                    trilogieShipVia = "N/A";
                    break;
            }

            return trilogieShipVia;
        }
    }
}