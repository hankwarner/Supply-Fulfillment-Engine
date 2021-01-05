using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FergusonSourcingCore.Models;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Logging;

namespace FergusonSourcingEngine.Controllers
{
    public class SourcingController
    {
        private static ILogger _logger;
        private ItemController itemController { get; set; }
        private LocationController locationController { get; set; }
        private ShippingController shippingController { get; set; }
        private OrderController orderController { get; set; }
        private RequirementController requirementController { get; set; }

        public SourcingController(ILogger logger, ItemController itemController, LocationController locationController, ShippingController shippingController, OrderController orderController, RequirementController requirementController)
        {
            _logger = logger;
            this.itemController = itemController;
            this.locationController = locationController;
            this.shippingController = shippingController;
            this.orderController = orderController;
            this.requirementController = requirementController;
        }


        /// <summary>
        ///     Runs the entire Sourcing Engine.
        /// </summary>
        public async Task StartSourcing(DocumentClient documentClient, AtgOrderRes atgOrderRes)
        {
            try
            {
                await SourceOrder(atgOrderRes);

                _logger.LogInformation(@"Sourced order: {Order}", atgOrderRes);

                atgOrderRes.SetTotalRuntime();

                // Write order to Cosmos DB
                var ordersContainerName = Environment.GetEnvironmentVariable("ORDERS_CONTAINER_NAME");
                _logger.LogInformation($"ordersContainerName: {ordersContainerName}");

                var collectionUri = UriFactory.CreateDocumentCollectionUri("sourcing-engine", ordersContainerName);

                await documentClient.UpsertDocumentAsync(collectionUri, atgOrderRes);
            }
            catch (NullReferenceException ex)
            {
                var title = "Missing required field: {E}";
                _logger.LogWarning(@"{0}: {1}", title, ex);
#if !DEBUG
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Warning message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
            }
            catch (Exception ex)
            {
                var title = "Error in StartSourcing";
                _logger.LogError(@"{0}: {1}", title, ex);
#if !DEBUG
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
            }
        }


        /// <summary>
        ///     Accepts an order from ATG and populates the location where each item should ship from based on item requirments,
        ///     current inventory, and customer location.
        /// </summary>
        /// <param name="atgOrderRes">The original order from ATG, stored in the atg-orders container.</param>
        public async Task SourceOrder(AtgOrderRes atgOrderRes)
        {
            try
            {
                atgOrderRes.sourcingMessage.Replace("Order received.", "");
                atgOrderRes.shipping.shipVia = shippingController.GetOrderShipVia(atgOrderRes.shipping.shipViaCode);

                await InitilizeData(atgOrderRes);

                // Build All Lines and separate by Sourcing Guide
                var allLines = GroupBySourcingGuide(atgOrderRes.items);

                var hasFEILines = allLines.lineDict.Any(x => x.Key == "FEI" || x.Key == "Branch");
                var isPickupOrder = atgOrderRes.shipping.shipViaCode == "WCL" || atgOrderRes.shipping.shipViaCode == "CPU";

                if (hasFEILines && !isPickupOrder)
                {
                    SetLineLocationsAndRequirements(allLines, atgOrderRes);
                }

                if (isPickupOrder)
                {
                    SourcePickupOrders(allLines.lineDict, atgOrderRes);
                }
                else
                {
                    // Run sourcing logic to set the Ship From on each line
                    RunSourcingEngine(allLines.lineDict, atgOrderRes, Enums.SourcingType.SourceByLine);

                    // Check if the estimated shipping cost is greater than the order shipping cost plus 10%
                    var combinedLines = allLines.lineDict.Values.SelectMany(x => x).ToList();

                    if (!string.IsNullOrEmpty(atgOrderRes.shipping?.price))
                    {
                        var estimatedShippingCost = await GetEstimatedShippingCostOfOrder(combinedLines, atgOrderRes);

                        atgOrderRes.exceedsShippingCost = DoesShippingCostExceedThreshold(estimatedShippingCost, atgOrderRes);
                    }

                    // If shipping cost exceeds what is on the order, then source complete
                    if (atgOrderRes.exceedsShippingCost)
                    {
                        RunSourcingEngine(allLines.lineDict, atgOrderRes, Enums.SourcingType.SourceComplete);
                    }

                    SetSourcingMessagesOnOrder(atgOrderRes);
                }
#if DEBUG
                SetProcessSourcing(atgOrderRes);
#endif
                SetVendorOnOrderLines(atgOrderRes);
            }
            catch(KeyNotFoundException ex)
            {
                var title = "KeyNotFoundException in SourceOrder";
                _logger.LogError(@"{0}: {1}", title, ex);
#if !DEBUG
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
            }
            catch (Exception ex)
            {
                var title = "Exception in SourceOrder";
                _logger.LogError(@"{0}: {1}", title, ex);
#if !DEBUG
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
                throw;
            }
        }


        /// <summary>
        ///     Runs asynchronously to initilize item, inventory and locations data needed before sourcing can begin.
        /// </summary>
        /// <param name="atgOrderRes">The original order from ATG, stored in the atg-orders container.</param>
        public async Task InitilizeData(AtgOrderRes atgOrderRes)
        {
            try
            {
                var initializeDataTasks = new List<Task>()
                {
                    itemController.InitializeItems(atgOrderRes),
                    itemController.InitializeInventory(atgOrderRes)
                };

                var requiresLocations = !string.IsNullOrEmpty(atgOrderRes.shipping.shipTo.zip);

                if (requiresLocations)
                {
                    initializeDataTasks.Add(locationController.InitializeLocations(atgOrderRes));
                }

                await Task.WhenAll(initializeDataTasks);

                LogLocationAndItemInventory();
            }
            catch(Exception ex)
            {
                var title = "Error Initilizing Data";
                _logger.LogError(@"{0}: {1}", title, ex);
#if !DEBUG
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
                throw;
            }
        }


        /// <summary>
        ///     Assigns a Ship From location to each item based on the sourcing guide, estimated shipping cost and time in transit.
        /// </summary>
        /// <param name="allLines">Item lines separated by sourcing guide</param>
        /// <param name="sourcingType">Enum for which sourcing logic to run (source by line or source complete).</param>
        public void RunSourcingEngine(Dictionary<string, List<SingleLine>> allLines, AtgOrderRes atgOrderRes, Enums.SourcingType sourcingType)
        {
            try
            {
                _logger.LogInformation("RunSourcingEngine start");

                foreach (var itemGroupByGuide in allLines)
                {
                    var guide = itemGroupByGuide.Key;
                    var lines = itemGroupByGuide.Value;

                    if (guide == "SOD" || guide == "No Source" || guide == "Vendor Direct")
                    {
                        SetShipFromOnNonFEILines(lines, guide, atgOrderRes);
                        continue;
                    }

                    if(sourcingType == Enums.SourcingType.SourceByLine)
                    {
                        SourceByLine(lines, atgOrderRes);
                    }
                    else
                    {                        
                        SourceComplete(lines, guide, atgOrderRes);
                    }
                }

                _logger.LogInformation("RunSourcingEngine finish");
            }
            catch (Exception ex)
            {
                var title = "Error in RunSourcingEngine";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(title);
            }
        }


        /// <summary>
        ///     Sets messaging related to the sourcing outcome, such as backordered, in stock, customer pickup, etc.
        /// </summary>
        public void SetSourcingMessagesOnOrder(AtgOrderRes atgOrderRes)
        {
            try
            {
                SetOrderLevelMessaging(atgOrderRes);

                SetItemLevelMessaging(atgOrderRes);
            }
            catch(Exception ex)
            {
                var title = "Error in SetSourcingMessageOnOrder";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Sets sourcing messaging at the order body level. For example, if the order has multiple sources.
        /// </summary>
        public void SetOrderLevelMessaging(AtgOrderRes atgOrderRes)
        {
            try
            {
                atgOrderRes.sourcingMessage.Replace("Order received.", "");

                if (!atgOrderRes.validSellWarehouse || string.IsNullOrEmpty(atgOrderRes.sellWhse))
                {
                    if (!atgOrderRes.sourcingMessage.Contains("sell warehouse"))
                    {
                        atgOrderRes.sourcingMessage += "Invalid sell warehouse. ";
                    }
                }

                var validLines = atgOrderRes.items.Where(item => !string.IsNullOrEmpty(item.shipFrom));
                var hasMultipleSources = false;
                var firstShipFrom = validLines.FirstOrDefault()?.shipFrom;

                if(firstShipFrom != null)
                {
                    hasMultipleSources = validLines.Any(item => item.shipFrom != firstShipFrom);
                }

                if (hasMultipleSources)
                {
                    if (!atgOrderRes.sourcingMessage.Contains("multiple sources"))
                    {
                        atgOrderRes.sourcingMessage += "Order has multiple sources.";
                    }
                }
                else
                {
                    atgOrderRes.sourceComplete = true;
                    atgOrderRes.sourceFrom = atgOrderRes.items[0].shipFrom;
                }
            }
            catch (Exception ex)
            {
                var title = "Error in SetOrderLevelMessaging";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "yellow";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Sets sourcing messaging at the item level. For example, backordered vs in stock, warehouse management system status, etc.
        /// </summary>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public void SetItemLevelMessaging(AtgOrderRes atgOrderRes)
        {
            try
            {
                atgOrderRes.items.ForEach(item =>
                {
                    var branchNum = item.shipFrom;
                    var guide = item.sourcingGuide;
                    var requirements = item.requirements;
                    requirements.TryGetValue("Overpack", out bool requiresOverpack);

                    if (guide == "SOD")
                    {
                        item.sourcingMessage = "Source line through SOD.";
                    }
                    else if (guide == "No Source" || string.IsNullOrEmpty(item.shipFrom) || item.invalidMPN)
                    {
                        item.sourcingMessage = "This item has no location to source from. ";

                        if (string.IsNullOrEmpty(item.masterProdId))
                            item.sourcingMessage += "MasterProdId is required.";
                        else if (string.IsNullOrEmpty(item.quantity) || item.quantity == "0")
                            item.sourcingMessage += "Quantity is required.";
                        else if (item.invalidMPN)
                            item.sourcingMessage += "Invalid MPN.";
                    }
                    else if (guide == "Vendor Direct")
                    {
                        item.sourcingMessage = "This line should be sourced directly from the vendor.";
                    }
                    else if (item.backordered)
                    {
                        var mpn = item.masterProdId;
                        var qty = int.Parse(item.quantity);

                        var hasStockAtAnyLocation = itemController.IsItemInStockAtAnyLocation(mpn, qty);
                        _logger.LogInformation($"hasStockInAnyLocation {hasStockAtAnyLocation}");

                        if (atgOrderRes.exceedsShippingCost && hasStockAtAnyLocation)
                        {
                            item.sourcingMessage = "Backordered. Sourced from this location because the shipping cost was too high in the in-stock location.";
                        }
                        else if (requiresOverpack)
                        {
                            if (item.noLocationsMeetRequirements)
                                item.sourcingMessage = "Backordered due to no available overpack locations. Ship From set to the sell warehouse.";
                            else
                                item.sourcingMessage = "Backordered due to no inventory in an overpack location.";
                        }
                        else if (item.noLocationsMeetRequirements)
                        {
                            item.sourcingMessage = "Backordered. No locations meet item requirements.";
                        }
                        else
                        {
                            item.sourcingMessage = "Backordered.";
                        }
                    }
                    else if (!item.backordered)
                    {
                        if (item.noLocationsMeetRequirements)
                        {
                            if (requiresOverpack)
                                item.sourcingMessage = "No available overpack locations. Ship From set to an in stock location.";
                            else
                                item.sourcingMessage = "No locations meet item requirements. Ship From set to an in stock location.";
                        }
                        else
                        {
                            item.sourcingMessage = "In Stock.";

                            locationController.locations.LocationDict.TryGetValue(branchNum, out Location location);
                            var hasWMS = location?.WarehouseManagementSoftware;

                            if (hasWMS == false && !item.sourcingMessage.Contains("non WMS"))
                            {
                                item.sourcingMessage = "This is a non WMS location please verify inventory for this line.";
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                var title = "Error in SetItemLevelMessaging";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "yellow";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Builds the AllLines class from items from the request and separates by Sourcing Guide. Creates
        ///     a dictionary entry where key is Sourcing Guide, and the value is a list of all order lines 
        ///     with items for that sourcing guide.
        /// </summary>
        /// <param name="orderItems">All items from the initial ATG order.</param>
        /// <returns>New list of items grouped by their sourcing guideline.</returns>
        public AllLines GroupBySourcingGuide(List<ItemRes> orderItems)
        {
            try
            {
                var allLines = new AllLines();

                for (var i = 0; i < orderItems.Count(); i++)
                {
                    var mpn = orderItems[i].masterProdId;
                    int.TryParse(orderItems[i].quantity, out int quantity);

                    if (string.IsNullOrEmpty(mpn) || quantity == 0 || orderItems[i].invalidMPN) continue;

                    var lineId = orderItems[i].lineId;

                    var sourcingGuide = itemController.items.ItemDict[mpn].SourcingGuideline;
                    // Set sourcing guide on the line
                    orderItems[i].sourcingGuide = sourcingGuide;

                    // Rounded items- if it's a broken bulk pack, need to ship from Branch instead of a DC
                    if (sourcingGuide == "Branch" || sourcingGuide == "FEI")
                    {
                        var isBulkPack = itemController.items.ItemDict[mpn].BulkPack;
                        var bulkPackQuantity = itemController.items.ItemDict[mpn].BulkPackQuantity;

                        if (isBulkPack && (quantity % bulkPackQuantity != 0))
                        {
                            sourcingGuide = "Branch";
                        }
                    }

                    var currLine = new SingleLine(mpn, quantity, lineId, sourcingGuide);

                    currLine.isMultiLineItem = orderItems
                        .Where(item => !string.IsNullOrEmpty(item.masterProdId) && !string.IsNullOrEmpty(orderItems[i].quantity))
                        .Count(item => item.masterProdId == mpn) > 1;

                    // Using TryAdd here because there will be duplicate sourcingGuides
                    allLines.lineDict.TryAdd(sourcingGuide, new List<SingleLine>());

                    allLines.lineDict[sourcingGuide].Add(currLine);
                }

                return allLines;
            }
            catch(KeyNotFoundException ex)
            {
                var title = "KeyNotFoundException in GroupBySourcingGuide";
                var text = $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(title);
                throw;
            }
        }


        /// <summary>
        ///     Sources each line individually and implies that the order does not need to be shipped together.
        /// </summary>
        /// <param name="lines">Line group with the same sourcing guideline.</param>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public void SourceByLine(List<SingleLine> lines, AtgOrderRes atgOrderRes)
        {
            try
            {
                foreach (var line in lines)
                {
                    var vendor = itemController.items.ItemDict[line.MasterProductNumber].Vendor;

                    var orderItem = orderController.GetOrderItemByLineId(line.LineId, atgOrderRes);
                    // Set Vendor name on the item line
                    orderItem.vendor = vendor;

                    var locations = line.Locations.Keys.ToList();

                    var sourced = SetShipFromOnLine(line, locations, orderItem);

                    if (sourced) continue;

                    // If not sourced, backorder to the closest stocking location.
                    BackOrderToClosestStockingLocation(locations, line, orderItem, atgOrderRes);
                }
            }
            catch(Exception ex)
            {
                var title = "Error in SourceByLine";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                throw;
            }
        }


        /// <summary>
        ///     Sets the Ship From value on a single line based on quantity and available locations. Used when sourcing by line.
        /// </summary>
        /// <param name="line">Line to be sourced.</param>
        /// <param name="locations">List of branch numbers that the item can potentially be sourced from.</param>
        /// <param name="orderItem">Item line from the AtgOrderRes.</param>
        /// <returns>True if ship from was set on the line.</returns>
        public bool SetShipFromOnLine(SingleLine line, List<string> locations, ItemRes orderItem)
        {
            try
            {
                var sourced = false;
                var mpn = line.MasterProductNumber;

                foreach (var branchNum in locations)
                {
                    var locationInventoryDict = new Dictionary<string, int>();

                    if (line.isMultiLineItem)
                    {
                        locationInventoryDict = itemController.inventory.InventoryDict[mpn].MultiLineAvailable;
                    }
                    else
                    {
                        locationInventoryDict = itemController.inventory.InventoryDict[mpn].Available;
                    }

                    locationInventoryDict.TryGetValue(branchNum, out int locationInventory);

                    // If current location has enough inventory to fulfill the line, mark current location as the ship from
                    if (locationInventory >= line.Quantity)
                    {
                        line.ShipFrom = branchNum;
                        locationController.locations.LocationDict.TryGetValue(branchNum, out Location location);

                        // Set Ship From on the order item line
                        orderItem.shipFrom = branchNum;
                        orderItem.shipFromLogon = location?.Logon;

                        // Reduce the available inventory by what is being sourced for multi line items
                        itemController.inventory.InventoryDict[mpn].MultiLineAvailable[branchNum] -= line.Quantity;

                        line.ShipFromInventory = locationInventory;
                        sourced = true;
                        break;
                    }
                }

                return sourced;
            }
            catch(Exception ex)
            {
                var title = "Exception in SetShipFromOnLine";
                var text = $"MPN: {orderItem.masterProdId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "yellow";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);

                return false;
            }
        }


        /// <summary>
        ///     Sets the Ship From to the closest stocking location (or closest location for non-stocks) for the item and flags the line as backordered.
        /// </summary>
        /// <param name="locations">List of branch numbers that the item can potentially be sourced from.</param>
        /// <param name="line">Order line to be sourced.</param>
        /// <param name="atgOrderRes">The ATG Order response that will be written to CosmosDB.</param>
        public void BackOrderToClosestStockingLocation(List<string> locations, SingleLine line, ItemRes orderItem, AtgOrderRes atgOrderRes)
        {
            try
            {
                var backorderLocation = "";

                // If no locations meet requirements, use the sell warehouse
                if (orderItem.noLocationsMeetRequirements)
                {
                    backorderLocation = atgOrderRes.sellWhse;
                }
                else
                {
                    backorderLocation = GetClosestStockingLocation(locations, line.MasterProductNumber);
                }
                _logger.LogInformation($"Backorder Location: {backorderLocation}");

                BackOrderToLocation(line, backorderLocation, atgOrderRes);
            }
            catch (Exception ex)
            {
                var teamsMessage = new TeamsMessage(
                    "Exception in BackOrderToClosestStockingLocation",
                    $"Order ID: {atgOrderRes.id}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", 
                    "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                throw;
            }
        }


        public void SourceComplete(List<SingleLine> lines, string guide, AtgOrderRes atgOrderRes)
        {
            var linesToBeSourced = new LinesToBeSourced();
            linesToBeSourced.CurrentLines = lines;

            SourceLinesComplete(linesToBeSourced, guide, atgOrderRes);

            return;
        }


        /// <summary>
        ///     Sources the order from a single location and implies that the order should ship complete. Attempts to find the closest location
        ///     that has all items in stock. If no single location has the entire order in stock, it will backorder to the closest location.
        /// </summary>
        /// <param name="linesToBeSourced">Group of lines that are currently unsourced.</param>
        /// <param name="guide">Sourcing guideline for the item group.</param>
        /// <returns>A list of sourced lines.</returns>
        public async Task<List<SingleLine>> SourceLinesComplete(LinesToBeSourced linesToBeSourced, string guide, AtgOrderRes atgOrderRes)
        {
            try
            {
                var currLines = linesToBeSourced.CurrentLines;

                var commonLocations = GetCommonLocations(currLines);

                // Loop through commonLocations and see if there is enough inventory to source each line
                var sourcedComplete = false;
                var currLocationId = "";

                // Time effective logic: ship from location with the lowest lead time that can fullfill all line items
                foreach (var branchNum in commonLocations)
                {
                    currLocationId = branchNum;

                    for(var i=0; i < currLines.Count(); i++)
                    {
                        var line = currLines[i];
                        var mpn = line.MasterProductNumber;

                        // Reset the multi line values on first iteration
                        if (i == 0)
                        {
                            itemController.inventory.InventoryDict[mpn].MultiLineAvailable = new Dictionary<string, int>(itemController.inventory.InventoryDict[mpn].Available);
                        }

                        var inventoryDict = GetInventoryDictionaryByItem(line.isMultiLineItem, mpn);

                        inventoryDict.TryGetValue(branchNum, out int currLocationInventory);

                        if (currLocationInventory >= line.Quantity)
                        {
                            sourcedComplete = true;
                            itemController.inventory.InventoryDict[mpn].MultiLineAvailable[branchNum] -= line.Quantity;
                        }
                        else
                        {
                            sourcedComplete = false;
                            break;
                        }
                    }

                    if (sourcedComplete) break;
                }

                var preferredLocation = locationController.locations.LocationDict.FirstOrDefault(l => l.Value.IsPreferred).Key;

                // If there is no preferred location found (such as for branch orders), use the closest location
                if (preferredLocation == null || guide == "Branch")
                {
                    preferredLocation = commonLocations[0];
                }
                _logger.LogInformation($"Preferred Location {preferredLocation}");

                // If the order sourced complete, set ship from to the current location
                if (sourcedComplete)
                {
                    ResetMultiLineInventoryQuantities(currLines);

                    SetShipFromLocationAndBackOrderQuantity(currLocationId, currLines, atgOrderRes);

                    if(currLocationId != preferredLocation)
                    {
                        // Then check if the proposed shipping cost exceeds the shipping cost charged to the customer by more than 10%, then move to Cost Effective logic 
                        var estimatedShippingCost = await GetEstimatedShippingCostOfOrder(currLines, atgOrderRes);

                        atgOrderRes.exceedsShippingCost = DoesShippingCostExceedThreshold(estimatedShippingCost, atgOrderRes);
                    }
                }

                // Cost effective logic: if not able to source complete or shipping cost is too high, use the preferred location
                if (!sourcedComplete || atgOrderRes.exceedsShippingCost)
                {
                    ResetMultiLineInventoryQuantities(currLines);

                    SetShipFromLocationAndBackOrderQuantity(preferredLocation, currLines, atgOrderRes);
                }

                // If we reach this point, all current lines have sourced
                linesToBeSourced.SourcedLines.AddRange(currLines);
                linesToBeSourced.CurrentLines = new List<SingleLine>();

                return linesToBeSourced.SourcedLines;
            }
            catch(Exception ex)
            {
                var title = "Error in SourceLinesComplete";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var teamsMessage = new TeamsMessage(title, text, "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                throw;
            }
        }


        /// <summary>
        ///     Sets the location to the sell warehouse for pickup orders. Also checks if the pickup location has inventory for each line and 
        ///     sets messaging at the item level if the pickup location does not have inventory.
        /// </summary>
        /// <param name="allLines">All lines from the initial ATG order.</param>
        public void SourcePickupOrders(Dictionary<string, List<SingleLine>> allLines, AtgOrderRes atgOrderRes)
        {
            try
            {
                _logger.LogInformation("SourcePickupOrders start");
                var pickupLocation = atgOrderRes.sellWhse;
                atgOrderRes.sourceFrom = pickupLocation;
                atgOrderRes.sourceComplete = true;

                // Write 'Ship From' and 'Vendor' to order line
                atgOrderRes.items.ForEach(item => 
                {
                    var mpn = item.masterProdId;
                    var itemDataExists = itemController.items.ItemDict.TryGetValue(mpn, out ItemData itemData);

                    if (itemDataExists)
                    {
                        item.vendor = itemData.Vendor;
                    }
                    
                    item.shipFrom = pickupLocation;
                    item.shipFromLogon = locationController.GetBranchLogonID(pickupLocation);
                });

                // Ensure that the pickup location has the required inventory on the order 
                var pickupLocationHasInventory = ValidatePickupInventory(pickupLocation, allLines, atgOrderRes);

                _logger.LogInformation("SourcePickupOrders finish");
            }
            catch(Exception ex)
            {
                var title = "Error in SourcePickupOrders";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(title);
            }
        }


        /// <summary>
        ///     Determines if the selected pickup location branch has enough inventory to fulfill pickup lines. If not,
        ///     it writes the sourcing message on the line(s) indicting the branch does not have enough inventory.
        /// </summary>
        /// <param name="pickupLocation">Branch number of the selected pickup location</param>
        /// <param name="pickupLines">List of lines where sourcing guide is Pickup.</param>
        /// <returns>Returns true unless any of the line(s) are not able to be fulfilled at the pickup branch.</returns>
        public bool ValidatePickupInventory(string pickupLocation, Dictionary<string, List<SingleLine>> allLines, AtgOrderRes atgOrderRes)
        {
            var pickupLocationHasInventory = true;

            try
            {
                foreach(var itemGroup in allLines)
                {
                    var lines = itemGroup.Value;
                    
                    // Check if the pickup location has enough inventory to fulfill each line
                    lines.ForEach(l =>
                    {
                        var mpn = l.MasterProductNumber;
                        
                        itemController.inventory.InventoryDict[mpn].Available.TryGetValue(pickupLocation, out int inventoryAtPickupLocation);

                        var orderItem = orderController.GetOrderItemByLineId(l.LineId, atgOrderRes);

                        if (l.Quantity > inventoryAtPickupLocation)
                        {
                            pickupLocationHasInventory = false;
                            orderItem.backordered = true;
                            orderItem.sourcingMessage = "This pickup location does not have the required quantity of inventory for the order line.";
                        }
                        else
                        {
                            orderItem.backordered = false;
                            orderItem.sourcingMessage = "Process payment and print order.";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var title = "Error in ValidatePickupInventory";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "yellow";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }

            return pickupLocationHasInventory;
        }


        /// <summary>
        ///     Sources lines where the item guideline is SOD, Vendor Direct or No Source.
        /// </summary>
        /// <param name="lines">Line group with the same sourcing guideline.</param>
        /// <param name="guide">Sourcing guideline for the item group.</param>
        public void SetShipFromOnNonFEILines(List<SingleLine> lines, string guide, AtgOrderRes atgOrderRes)
        {
            try
            {
                string shipFrom = null;
                string shipFromLogon = null;

                if(guide == "SOD")
                {
                    // SOD lines only go to branch number 39
                    shipFrom = "39";
                    shipFromLogon = "D98 DISTRIBUTION CENTERS";
                }
                
                lines.ForEach(l => {
                    var orderItem = orderController.GetOrderItemByLineId(l.LineId, atgOrderRes);

                    var mpn = l.MasterProductNumber;
                    var vendorName = itemController.items.ItemDict[mpn].Vendor;

                    // Set Ship From to the selling warehouse branch number for Vendor Direct lines
                    if (guide == "Vendor Direct")
                    {
                        shipFrom = atgOrderRes.sellWhse;
                    }

                    orderItem.vendor = vendorName;
                    orderItem.shipFrom = shipFrom;
                    orderItem.shipFromLogon = shipFromLogon;
                    orderItem.sourcingGuide = guide;
                });

            }
            catch (Exception ex)
            {
                var title = "Error in SetShipFromOnSODLines";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Used to keep track of inventory for orders that have the same item on different lines. Since we are decrementing the inventory
        ///     as each line is sourced, we have to reset it each time a different sourcing method is called.
        /// </summary>
        /// <param name="currLines">Line group with the same sourcing guideline.</param>
        public void ResetMultiLineInventoryQuantities(List<SingleLine> currLines)
        {
            try
            {
                foreach (var line in currLines)
                {
                    var mpn = line.MasterProductNumber;
                    itemController.inventory.InventoryDict[mpn].MultiLineAvailable = new Dictionary<string, int>(itemController.inventory.InventoryDict[mpn].Available);
                }
            }
            catch(Exception ex)
            {
                var title = "Error in ResetMultiLineInventoryQuantities";
                var text = $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "yellow";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Calls the UPS API to get the estimated cost of shipping the entire order with the current ship from locations.
        /// </summary>
        /// <param name="lines">Line group with the same sourcing guideline.</param>
        /// <returns>The total estimated shipping cost to ship the order through UPS.</returns>
        public async Task<double> GetEstimatedShippingCostOfOrder(List<SingleLine> lines, AtgOrderRes atgOrderRes)
        {
            _logger.LogInformation("GetEstimatedShippingCostOfOrder start");

            var totalEstShipCost = 0.00;
            var shippingTasks = new List<Task<double>>();

            try
            {
                var shipToAddress = new ShippingAddress()
                {
                    addressLine1 = atgOrderRes.shipping.shipTo.address1,
                    city = atgOrderRes.shipping.shipTo.city,
                    state = atgOrderRes.shipping.shipTo.state,
                    zip = atgOrderRes.shipping.shipTo.zip.Substring(0, 5)
                };

                var linesGroupedByShipFrom = lines.Where(x => !string.IsNullOrEmpty(x.ShipFrom)).GroupBy(l => l.ShipFrom);

                // Create a separate request for each Ship From location
                foreach (var groupedLine in linesGroupedByShipFrom)
                {
                    var branchNum = groupedLine.Key;
                    var shipFromAddress = locationController.locations.LocationDict[branchNum].ShippingAddress;

                    var cumulativeWeight = 0.00;

                    foreach (var line in groupedLine)
                    {
                        var mpn = line.MasterProductNumber;
                        var weight = itemController.items.ItemDict[mpn].Weight;

                        cumulativeWeight += weight;
                    }

                    // Add to list of UPS tasks that will be run async
                    shippingTasks.Add(shippingController.EstimateShippingCost(cumulativeWeight, shipToAddress, shipFromAddress, atgOrderRes));
                }

                // Send requests to UPS for shipping cost estimate
                var estShipCosts = await Task.WhenAll(shippingTasks);

                // Sum values of all shipping costs
                totalEstShipCost = estShipCosts.Sum();

                _logger.LogInformation($"GetEstimatedShippingCostOfOrder finish. Total est ship cost {totalEstShipCost}");
            }
            catch(Exception ex)
            {
                var title = "Error in GetEstimatedShippingCostOfOrder";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                _logger.LogError(title);
            }

            return totalEstShipCost;
        }


        /// <summary>
        ///     Gets the current inventory dictionary based on if the same item is on multiple lines.
        /// </summary>
        /// <param name="isMultiLineItem">Does the order contain the same item on different lines.</param>
        /// <param name="mpn">Master Product Number of the item.</param>
        /// <returns></returns>
        public Dictionary<string, int> GetInventoryDictionaryByItem(bool isMultiLineItem, string mpn)
        {
            var inventoryDict = new Dictionary<string, int>();

            if (isMultiLineItem)
                inventoryDict = itemController.inventory.InventoryDict[mpn].MultiLineAvailable;
            else
                inventoryDict = itemController.inventory.InventoryDict[mpn].Available;

            return inventoryDict;
        }


        /// <summary>
        ///     Determines locations where each item in the group can potentially be sourced from based on item restrictions.
        /// </summary>
        /// <param name="currentLines">Line group with the same sourcing guideline.</param>
        /// <returns>Location ID's that all items share.</returns>
        public static List<string> GetCommonLocations(List<SingleLine> currentLines)
        {
            var commonLocations = new List<string>();

            try
            {
                foreach (var line in currentLines)
                {
                    if (currentLines.First() == line)
                    {
                        commonLocations = new List<string>(line.Locations.Keys);
                        continue;
                    }

                    var branchNumbers = line.Locations.Keys.ToList();

                    commonLocations = commonLocations.Intersect(branchNumbers).ToList();
                }
            }
            catch (Exception ex)
            {
                var title = "Error in GetCommonLocations";
                var text = $"Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }

            return commonLocations;
        }


        /// <summary>
        ///     Determines the closest location to the customer that stocks the item. If no stocking locations are found, returns the first location in the list.
        /// </summary>
        /// <param name="branchNumbers">Location ID's to check the stocking status of.</param>
        /// <param name="mpn">Master Product Number of the item</param>
        /// <returns>Location ID of the closest location to the customer that stocks the item.</returns>
        public string GetClosestStockingLocation(List<string> branchNumbers, string mpn)
        {
            try
            {
                var closestStockingLocation = branchNumbers[0];

                foreach (var branchNum in branchNumbers)
                {
                    itemController.inventory.InventoryDict[mpn].StockStatus.TryGetValue(branchNum, out bool stockingStatus);

                    if (stockingStatus == true)
                    {
                        closestStockingLocation = branchNum;
                        break;
                    }
                }

                return closestStockingLocation;
            }
            catch (Exception ex)
            {
                var title = "Error in GetClosestStockingLocation";
                var text = $"Error message: {ex.Message}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
                throw;
            }
        }


        /// <summary>
        ///     If all items in the group do not have common locations, determines which line has the least number of locations in common with the group. 
        ///     Only used when recursion is necessary in SourceComplete. 
        /// </summary>
        /// <param name="currentLines">Line group with the same sourcing guideline.</param>
        /// <returns>Line has the least number of locations in common with the group.</returns>
        public static int GetMostLimitingLineIndex(List<SingleLine> currentLines)
        {
            var mostLimitingLineIndex = 0;
            var mostLimitingLineLocationCount = currentLines.First().Locations.Count;

            for(var i = 1; i < currentLines.Count; i++)
            {
                var currLineLocationCount = currentLines[i].Locations.Count;

                if (mostLimitingLineLocationCount > currLineLocationCount)
                {
                    mostLimitingLineIndex = i;
                    mostLimitingLineLocationCount = currLineLocationCount;
                }
            }

            return mostLimitingLineIndex;
        }


        public bool DoesShippingCostExceedThreshold(double estimatedShippingCost, AtgOrderRes atgOrderRes)
        {
            var shippingCostOnOrder = Convert.ToDouble(atgOrderRes.shipping.price);
            // Compensating for high UPS estimated ship costs
            var fourHundredPercentOfShippingCost = shippingCostOnOrder * 4;
            _logger.LogInformation($"fourHundredPercentOfShippingCost {fourHundredPercentOfShippingCost}");

            var exceedsShippingCostThreshold = estimatedShippingCost > fourHundredPercentOfShippingCost;

            return exceedsShippingCostThreshold;
        }


        /// <summary>
        ///     Sets the location id as the ship from location on each item line as well as the quantity backordered.
        /// </summary>
        /// <param name="locationId">Location where the items will be sourced from.</param>
        /// <param name="currentLines">Line group with the same sourcing guideline.</param>
        public void SetShipFromLocationAndBackOrderQuantity(string locationId, List<SingleLine> currentLines, AtgOrderRes atgOrderRes)
        {
            try
            {
                currentLines.ForEach((l) =>
                {
                    var mpn = l.MasterProductNumber;

                    l.ShipFrom = locationId;

                    var orderItem = orderController.GetOrderItemByLineId(l.LineId, atgOrderRes);
                    orderItem.shipFrom = locationId;
                    locationController.locations.LocationDict.TryGetValue(locationId, out Location location);
                    orderItem.shipFromLogon = location?.Logon;


                    var inventoryDict = GetInventoryDictionaryByItem(l.isMultiLineItem, mpn);

                    var locationInventory = 0;
                    inventoryDict.TryGetValue(locationId, out locationInventory);

                    l.ShipFromInventory = locationInventory;

                    if (locationInventory >= l.Quantity)
                    {
                        l.QuantityBackordered = 0;
                        itemController.inventory.InventoryDict[mpn].MultiLineAvailable[locationId] -= l.Quantity;
                    }
                    else
                    {
                        l.QuantityBackordered = l.Quantity - locationInventory;
                        orderItem.backordered = true;
                    }
                });
            }
            catch(Exception ex)
            {
                var title = "Error in SetShipFromLocationAndBackOrderQuantity";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Sets the ship from location on the item line and flags it as backordered.
        /// </summary>
        /// <param name="line">Backorded line.</param>
        /// <param name="locationId">Location where the item will be sourced from.</param>
        public void BackOrderToLocation(SingleLine line, string locationId, AtgOrderRes atgOrderRes)
        {
            try
            {
                var mpn = line.MasterProductNumber;
                itemController.inventory.InventoryDict[mpn].Available.TryGetValue(locationId, out int currItemInventory);

                // Set Ship From on the order item line
                var orderItem = orderController.GetOrderItemByLineId(line.LineId, atgOrderRes);
                orderItem.shipFrom = locationId;
                locationController.locations.LocationDict.TryGetValue(locationId, out Location location);
                orderItem.shipFromLogon = location?.Logon;
                orderItem.backordered = true;

                line.ShipFrom = locationId;
                line.ShipFromInventory = currItemInventory;
                line.QuantityBackordered = line.Quantity - currItemInventory;

                itemController.inventory.InventoryDict[mpn].Available[locationId] = 0;
            }
            catch(Exception ex)
            {
                var title = "Error in BackOrderToLocation";
                var text = $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}";
                var color = "red";
                var teamsMessage = new TeamsMessage(title, text, color, SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
            }
        }


        /// <summary>
        ///     Filters the ATG order lines by MPN. Useful when determining if the same item is on multiple lines.
        /// </summary>
        /// <param name="mpn">Master Product Number of the item.</param>
        /// <returns>ATG order lines that contain the MPN.</returns>
        public static List<ItemRes> GetOrderItemsByMPN(string mpn, AtgOrderRes atgOrderRes)
        {
            return atgOrderRes.items
                .Where(item => item.masterProdId == mpn.ToString())
                .Select(item => item)
                .ToList();
        }


        /// <summary>
        ///     Logs the location dict and invetory dict to Azure Monitor.
        /// </summary>
        public void LogLocationAndItemInventory()
        {
            try
            {
                foreach (var location in locationController.locations.LocationDict)
                {
                    var branchNum = location.Key;
                    var details = location.Value;
#if RELEASE
                    _logger.LogInformation($"Location: {branchNum}. Dist: {details.Distance}.");
#endif
#if DEBUG
                    _logger.LogInformation($"Location: {branchNum}. Dist: {details.Distance}. DaysInTrans: {details.BusinessDaysInTransit}. EstDelivery: {details.EstDeliveryDate}.");
#endif
                    itemController.mpns.ForEach(mpn =>
                    {
                        itemController.inventory.InventoryDict[mpn].Available.TryGetValue(branchNum, out int currLocationInventory);
                        _logger.LogInformation($"{mpn} inventory: {currLocationInventory}.");
                    });
                }
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Exception in LogLocationAndItemInventory");
            }
        }


        /// <summary>
        ///     Sets the item vendor on all lines with a valid MPN.
        /// </summary>
        public void SetVendorOnOrderLines(AtgOrderRes atgOrderRes)
        {
            try
            {
                _logger.LogInformation($"SetVendorOnOrderLines called");

                var validItems = atgOrderRes.items
                    .Where(item => !string.IsNullOrEmpty(item.masterProdId) && !item.invalidMPN)
                    .Select(item => item);

                foreach (var item in validItems)
                {
                    var mpn = item.masterProdId;

                    var itemDataExists = itemController.items.ItemDict.TryGetValue(mpn, out ItemData itemData);

                    if (itemDataExists)
                    {
                        var vendor = itemData.Vendor;

                        item.vendor = vendor ?? item.vendor;
                    }
                }
            }
            catch(Exception ex)
            {
                var title = "Error in SetVendorOnOrderLines";
                _logger.LogError(ex, title);
#if RELEASE
                var teamsMessage = new TeamsMessage(title, $"Order Id: {atgOrderRes.atgOrderId}. Error message: {ex.Message}. Stacktrace: {ex.StackTrace}", "yellow", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
            }
        }


        /// <summary>
        ///     Sets the "Process Sourcing" flag on the order response to true if all items are in stock and sourced complete. Otherwise, it will flag 
        ///     the order for manual intervention by a rep and create a manual order.
        /// </summary>
        /// <param name="orderRes">The ATG Order response object.</param>
        public void SetProcessSourcing(AtgOrderRes orderRes)
        {
            if (orderRes == null) throw new ArgumentNullException("orderRes", "AtgOrderRes is invalid.");

            // Set to the initial value equal to source complete
            orderRes.processSourcing = orderRes.sourceComplete;

            // If it's not sourced complete, no need to look at the items
            if (!orderRes.processSourcing) return;

            // Override if any of the items have a sourcing error message
            foreach(var item in orderRes.items)
            {
                var message = item.sourcingMessage.ToLower();
                var isErrorLine = !message.Contains("in stock") && !message.Contains("process payment") ? true : false;

                if (isErrorLine)
                {
                    orderRes.processSourcing = false;
                    break;
                }
            }
        }


        /// <summary>
        ///     Sets the potential locations to source from for each line item using the item's sourcing requirements, such as sourcing guide.
        /// </summary>
        /// <param name="allLines">Order items grounped by sourcing guide.</param>
        public void SetLineLocationsAndRequirements(AllLines allLines, AtgOrderRes atgOrderRes)
        {
            try
            {
                foreach (var linePair in allLines.lineDict)
                {
                    var guide = linePair.Key;

                    // Determine which locations are available for each line based on the sourcing guide
                    linePair.Value.ForEach(line =>
                    {
                        requirementController.SetLineRequirements(line, atgOrderRes);

                        // Determine which locations meet item requirements
                        foreach (var location in locationController.locations.LocationDict.Values)
                        {
                            // Skip locations that do not meet the item requirements
                            if (!requirementController.DoesLocationMeetRequirements(line, guide, location)) continue;

                            // If all requirements are met, add as an available location for the line
                            line.Locations.Add(location.BranchNumber, location);
                        }

                        // If line has no locations, flag it and use the locationsDict filtered by guide
                        if (line.Locations.Count() == 0)
                        {
                            _logger.LogInformation($"No locations meet requirements for item {line.MasterProductNumber}.");

                            var orderItem = atgOrderRes.items.FirstOrDefault(i => i.lineId == line.LineId);
                            orderItem.noLocationsMeetRequirements = true;

                            foreach (var location in locationController.locations.LocationDict)
                            {
                                if (requirementController.MeetsSourcingGuidelineRequirement(location.Value, guide))
                                {
                                    line.Locations.Add(location.Key, location.Value);
                                }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var title = "Error in SetLineLocationsAndRequirements";
                _logger.LogError(ex, title);
#if RELEASE
                var teamsMessage = new TeamsMessage(title, $"Error: {ex.Message}. Stacktrace: {ex.StackTrace}", "red", SourcingEngineFunctions.errorLogsUrl);
                teamsMessage.LogToTeams(teamsMessage);
#endif
            }
        }
    }
}
