using System;
using System.Collections.Generic;
using System.Linq;
using FergusonSourcingCore.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Polly;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Net;

namespace FergusonSourcingEngine.Controllers
{
    public class ItemController
    {
        public List<string> mpns = new List<string>();
        public Inventory inventory = new Inventory();
        public AllItems items = new AllItems();
        private ILogger _logger;

        public ItemController(ILogger logger)
        {
            _logger = logger;
        }


        /// <summary>
        ///     Calls the ItemMicroservice to build the item dictionary and stocking status dictionary.
        /// </summary>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public async Task InitializeItems(AtgOrderRes atgOrderRes)
        {
            try
            {
                _logger.LogInformation("InitializeItems start");

                await SetMPNs(atgOrderRes);

                var itemAndStockingData = await GetItemAndStockingData();

                // Write data from the response
                items.ItemDict = itemAndStockingData.ItemDataDict;

                await Task.WhenAll(
                    SetItemDetailsOnOrder(atgOrderRes), 
                    AddStockingStatusesToInventoryDict(itemAndStockingData.StockingStatusDict)
                );

                _logger.LogInformation("InitializeItems finish");
            }
            catch(Exception ex)
            {
#if !DEBUG
                var title = "Error in InitializeItems";
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
                _logger.LogError(ex, "Error in InitializeItems");
                throw;
            }
        }


        /// <summary>
        ///     Initializes the MPNs global variable from the items on the ATG order.
        /// </summary>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public async Task SetMPNs(AtgOrderRes atgOrderRes)
        {
            atgOrderRes.items.ForEach(async item =>
            {
                var mpn = item.masterProdId;
                var qty = int.Parse(item.quantity);

                if (!string.IsNullOrEmpty(mpn) && qty != 0) 
                    mpns.Add(mpn);
            });

            // If all items are missing MPN's, there is nothing further we can do
            if (mpns.Count() == 0) 
                throw new NullReferenceException("masterProdId and quantity are required");
        }


        /// <summary>
        ///     Calls the ItemMicroservice to get item data (weight, guideline, vendor, etc.) and stocking status data.
        /// </summary>
        /// <returns>The item data response.</returns>
        public async Task<ItemResponse> GetItemAndStockingData()
        {
            var retryPolicy = Policy.Handle<Exception>().Retry(3, (ex, count) =>
            {
                var title = "Error in GetItemAndStockingData";
                _logger.LogWarning($"{title}. Retrying...");

                if (count == 3)
                {
                    var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                    teamsMessage.LogToTeams(teamsMessage);
                    _logger.LogError(ex, title);
                }
            });

            return await retryPolicy.Execute(async () =>
            {
                var url = @"https://item-microservices.azurewebsites.net/api/item";
                var client = new RestClient(url);
                var request = new RestRequest(Method.GET)
                    .AddQueryParameter("code", Environment.GetEnvironmentVariable("ITEM_MICROSERVICES_KEY"));

                mpns.ForEach(mpn => request.AddQueryParameter("mpn", mpn.ToString()));

                var response = await client.ExecuteAsync(request);
                var jsonResponse = response.Content;
                _logger.LogInformation(@"Item microservices status code: {0}. Response: {1}", response.StatusCode, jsonResponse);

                if (response?.StatusCode != HttpStatusCode.OK || string.IsNullOrEmpty(jsonResponse))
                {
                    throw new Exception("Item Data returned null.");
                }

                var itemAndStockingData = JsonConvert.DeserializeObject<ItemResponse>(jsonResponse);

                // Throw exception if item data is null
                foreach (var item in itemAndStockingData.ItemDataDict)
                {
                    if (item.Value == null)
                    {
                        throw new ArgumentNullException("itemDataDict");
                    }
                }

                return itemAndStockingData;
            });
        }


        /// <summary>
        ///     Initilizes the stocking status dictionary in the inventory variable.
        /// </summary>
        /// <param name="stockingStatusDict">Dictionary of branch numbers and its stocking status of each item.</param>
        public async Task AddStockingStatusesToInventoryDict(Dictionary<string, Dictionary<string, bool>> stockingStatusDict)
        {
            try
            {
                foreach (var stockingStatus in stockingStatusDict)
                {
                    var mpn = stockingStatus.Key;

                    inventory.InventoryDict.TryAdd(mpn, new ItemInventory());
                    inventory.InventoryDict[mpn].StockStatus = stockingStatus.Value;
                }
            }
            catch (Exception ex)
            {
                var title = "Error in AddStockingStatusesToInventoryDict";
                var teamsMessage = new TeamsMessage(title, $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Sets any relevant item details, such as description and ship method, for each line item on the order.
        /// </summary>
        /// <param name="atgOrderRes">The ATG Order response object that will be written to Cosmos DB.</param>
        public async Task SetItemDetailsOnOrder(AtgOrderRes atgOrderRes)
        {
            try
            {
                foreach(var item in atgOrderRes.items)
                {
                    var mpn = item.masterProdId;

                    item.itemDescription = items.ItemDict[mpn].ItemDescription;
                    item.alt1Code = items.ItemDict[mpn].ALT1Code;

                    await SetItemShippingValues(item, mpn);
                }
            }
            catch (Exception ex)
            {
                var title = "Error in SetItemDetailsOnOrder";
                var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        public async Task SetItemShippingValues(ItemRes item, string mpn)
        {
            try
            {
                item.preferredShippingMethod = items.ItemDict[mpn].PreferredShippingMethod;

                var shippingController = new ShippingController(_logger, this);
                
                var shipViaTask = shippingController.GetItemPreferredShipVia(item.preferredShippingMethod, item.masterProdId, int.Parse(item.quantity));
                item.preferredShipVia = await shipViaTask;
            }
            catch (Exception ex)
            {
                var title = $"Error in SetShippingValues for MPN {item.masterProdId}";
                var text = $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var teamsMessage = new TeamsMessage(title, text, "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(ex, title);
            }
        }


        /// <summary>
        ///     Sets the invalidMPN flag to true on the ATG order line for the provided MPN.
        /// </summary>
        /// <param name="mpn">Master Product Number that was deemed invalid.</param>
        /// <param name="atgOrderRes">The ATG Order response object.</param>
        public void FlagInvalidMPN(string mpn, AtgOrderRes atgOrderRes)
        {
            try
            {
                var orderItems = SourcingController.GetOrderItemsByMPN(mpn, atgOrderRes);

                orderItems.ForEach(item => item.invalidMPN = true);

                var title = "Item is missing data.";
                var teamsMessage = new TeamsMessage(title, $"Item {mpn} is missing data.", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
            catch(Exception ex)
            {
                var title = "Error in FlagInvalidMPNs";
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Gets inventory from Manhattan tables and adds it to the Inventory Dictionary under the Available property,
        ///     resulting in a dictionary of branch number/inventory key value pairs for each location.
        /// </summary>
        public async Task InitializeInventory(AtgOrderRes atgOrderRes)
        {
            try
            {
                _logger.LogInformation("InitializeInventory start");

                // Response will be an array of JS objects
                var inventoryResponse = await GetInventoryData();

                var domInventory = await ParseInventoryResponse(inventoryResponse);

                // Add DOM inventory to inventoryDict
                await AddAvailableInventoryToDict(domInventory.InventoryData);

                _logger.LogInformation("InitializeInventory finish");
            }
            catch(Exception ex)
            {
                var title = $"Error in InitializeInventory. Order Id: {atgOrderRes.atgOrderId}";
#if RELEASE
                var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
                _logger.LogError(ex, title);
                throw;
            }
        }


        public async Task<JArray> GetInventoryData()
        {
            var retryPolicy = Policy.Handle<Exception>().Retry(5, (ex, count) =>
            {
                var title = "Error in GetInventoryData";
                _logger.LogWarning(ex, $"{title} . Retrying...");

                if (count == 5)
                {
                    var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                    teamsMessage.LogToTeams(teamsMessage);
                    _logger.LogError(ex, title);
                }
            });

            return await retryPolicy.Execute(async () =>
            {
                var requestBody = new DOMInventoryRequest(mpns);
                var jsonRequest = JsonConvert.SerializeObject(requestBody);

                var url = @"https://erebus.nbsupply.com:443/WebServices/Inventory/RequestInventoryFromDomB2CStorefront";

                var client = new RestClient(url)
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                };

                var request = new RestRequest(Method.POST)
                    .AddHeader("Content-Type", "text/plain")
                    .AddParameter("application/json; charset=utf-8", jsonRequest, ParameterType.RequestBody);

                var inventoryDataTask = client.ExecuteAsync(request);

                var response = await inventoryDataTask;
                _logger.LogInformation($"Raw response Content: {response?.Content}");
                var jsonResponse = response.Content;

                if (jsonResponse.Substring(0, 1) == "<" || response?.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception($"Erebus returned an invalid response: {jsonResponse}");
                }

                // Response should be an array of JS objects with MPN on the first line
                JArray inventoryResponse;
                try
                {
                    inventoryResponse = JArray.Parse(jsonResponse);
                }
                catch(JsonReaderException ex)
                {
                    _logger.LogWarning("Inventory API did not return a json array.", ex);
                    throw;
                }

                return inventoryResponse;
            });
        }


        public async Task<InventoryResponse> ParseInventoryResponse(JArray inventoryResponse)
        {
            var domInventory = new InventoryResponse();

            try
            {
                // Parse response into a list of dictionaries
                foreach (var inventoryContent in inventoryResponse.Children<JObject>())
                {
                    var branchNumAndQuantityDict = new Dictionary<string, string>();

                    foreach (var inventoryLine in inventoryContent.Properties())
                    {
                        branchNumAndQuantityDict.TryAdd(inventoryLine.Name, inventoryLine.Value.ToString());
                    }

                    domInventory.InventoryData.Add(branchNumAndQuantityDict);
                }
            }
            catch (Exception ex)
            {
                var title = "Error in ParseInventoryResponse";
                var teamsMessage = new TeamsMessage(title, $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }

            return domInventory;
        }


        public async Task AddAvailableInventoryToDict(List<Dictionary<string, string>> inventoryList)
        {
            try
            {
                inventoryList.ForEach(locationDict =>
                {
                    var mpn = locationDict.FirstOrDefault(x => x.Key == "MPID").Value;

                    foreach (var line in locationDict)
                    {
                        var branchNumber = line.Key;

                        // Skip lines where the key is not a branch number
                        if (branchNumber == "MPID" || branchNumber == "RetrievedFromDOM") continue;

                        var inventoryAtLocation = int.Parse(line.Value);

                        inventory.InventoryDict.TryAdd(mpn, new ItemInventory());

                        inventory.InventoryDict[mpn].Available.TryAdd(branchNumber, inventoryAtLocation);

                        // Make a copy of available inventory that can be decremented for multi line item use
                        inventory.InventoryDict[mpn].MultiLineAvailable.TryAdd(branchNumber, inventoryAtLocation);
                    }
                });
            }
            catch(Exception ex)
            {
                var title = "Error in AddAvailableInventoryToDict";
                var text = $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Determines if the given item and quantity is in stock at any location in the locations dictionary.
        /// </summary>
        /// <returns>Returns true if given item is in stock at any valid location.</returns>
        public bool IsItemInStockAtAnyLocation(string mpn, int quantity)
        {
            try
            {
                KeyValuePair<string, int>? locationWithInventory = inventory.InventoryDict[mpn].Available
                    .FirstOrDefault(location => location.Value >= quantity);

                if (locationWithInventory != null) return true;
            }
            catch (Exception ex)
            {
                var title = "Error in IsItemInStockAtAnyLocation";
                var teamsMessage = new TeamsMessage(title, $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }

            return false;
        }
    }
}
