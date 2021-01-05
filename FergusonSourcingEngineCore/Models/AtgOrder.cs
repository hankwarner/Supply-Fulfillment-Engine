using AzureFunctions.Extensions.Swashbuckle.Attribute;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FergusonSourcingCore.Models
{
    public class Payment
    {
        public string cardType { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string address3 { get; set; }
        public string cardholderName { get; set; }
        public string cardExpirationDate { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string zip { get; set; }
        public string token { get; set; }
        public string cardMaskedNumber { get; set; }
        public string phone { get; set; }

    }

    public class ShipTo
    {
        public string name { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string address3 { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string state { get; set; }
        public string id { get; set; }
        public string shipInstructionsAttention { get; set; }
        public string zip { get; set; }
        public string shipInstructionsPhoneNumberAreaDialing { get; set; }
        public string shipInstructionsPhoneNumberDialNumber { get; set; }
        public string specialShippingInstructions { get; set; }
    }

    public class Shipping
    {
        public string tax { get; set; }
        public string shipViaCode { get; set; }
        public string shipVia { get; set; } // Trilogie Ship Via code mapped directly from ATG order's shipViaCode
        public string price { get; set; }
        public ShipTo shipTo { get; set; }

    }

    public class Item
    {
        public string priceFormula { get; set; }
        public string priceColumn { get; set; }
        public string extendedPrice { get; set; }
        public string unitPrice { get; set; }
        public string unitPriceCode { get; set; }
        public string leadLawFlag { get; set; }
        public string promotionMultiplier { get; set; }
        public string distributedTax { get; set; }
        public string description { get; set; }
        public string netPrice { get; set; }
        public string quantity { get; set; }
        public string masterProdId { get; set; }
    }


    public class ItemRes : Item
    {
        public ItemRes(Item item)
        {
            priceFormula = item.priceFormula;
            priceColumn = item.priceColumn;
            extendedPrice = item.extendedPrice;
            unitPrice = item.unitPrice;
            unitPriceCode = item.unitPriceCode;
            leadLawFlag = item.leadLawFlag;
            promotionMultiplier = item.promotionMultiplier;
            distributedTax = item.distributedTax;
            description = item.description;
            netPrice = item.netPrice;
            quantity = item.quantity;
            masterProdId = item.masterProdId;
        }

        public ItemRes() { }

        public int lineId { get; set; }
        public string sourcingMessage { get; set; } = "";
        public string shipFrom { get; set; }
        public string shipFromLogon { get; set; }
        public string sourcingGuide { get; set; }
        public string itemDescription { get; set; }
        public string alt1Code { get; set; }
        [JsonIgnore]
        public string preferredShippingMethod { get; set; }
        public string preferredShipVia { get; set; } // Trilogie Ship Via code
        
        public string vendor { get; set; }

        [JsonIgnore]
        public bool backordered { get; set; } = false;

        [JsonIgnore]
        public Dictionary<string, bool> requirements { get; set; } = new Dictionary<string, bool>();

        [JsonIgnore]
        public bool invalidMPN { get; set; } = false;

        [JsonIgnore]
        public bool noLocationsMeetRequirements { get; set; } = false;
    }


    public abstract class AtgOrder
    {
        public string taxExempt { get; set; }
        public string atgOrderId { get; set; }
        public string custAccountId { get; set; }
        public string customerId { get; set; }
        public string taxAmount { get; set; }
        public string customerName { get; set; }
        public string orderedBy { get; set; }
        public string userEmail { get; set; }
        public string orderSubmitDate { get; set; }
        public string orderEntryDate { get; set; }
        public string orderRequiredDate { get; set; }
        public string shipFromWhse { get; set; }
        public string sellWhse { get; set; }
        public string notes { get; set; }
        public List<Payment> paymentOnAccount { get; set; }
        public Shipping shipping { get; set; }
        public string sourceSystem { get; set; }
        public string taxCode { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class AtgOrderReq : AtgOrder
    {
        public List<Item> items { get; set; }
    }

    public class AtgOrderRes : AtgOrder
    {
        public AtgOrderRes(AtgOrderReq req)
        {
            taxExempt = req.taxExempt;
            atgOrderId = req.atgOrderId;
            id = req.atgOrderId;
            custAccountId = req.custAccountId;
            customerId = req.customerId;
            customerName = req.customerName;
            orderSubmitDate = req.orderSubmitDate;
            orderEntryDate = req.orderEntryDate;
            orderRequiredDate = req.orderRequiredDate;
            shipFromWhse = req.shipFromWhse;
            sellWhse = req.sellWhse;
            notes = req.notes;
            paymentOnAccount = req.paymentOnAccount;
            shipping = req.shipping;
            sourceSystem = req.sourceSystem;
            taxCode = req.taxCode;
            paymentOnAccount = req.paymentOnAccount;

            items = new List<ItemRes>();

            if (req.items.Count == 0) throw new ArgumentException("No items found on order.", "items");

            for (var i = 0; i < req.items.Count; i++)
            {
                var item = new ItemRes(req.items[i]);
                item.lineId = i;
                items.Add(item);
            }
        }

        public AtgOrderRes() { }

        public string id { get; set; }

        // Default to false. If the order is sourced complete and all in stock, it will set to true
        public bool processSourcing { get; set; } = false;

        public bool sourceComplete { get; set; }

        public string sourceFrom { get; set; }

        public string sourcingMessage { get; set; } = "";

        public DateTime lastModifiedDate { get; set; } = DateTime.Now;

        public List<ItemRes> items { get; set; }

        public string trilogieErrorMessage { get; set; } = "";

        public string trilogieStatus { get; set; } = "Waiting on Trilogie response.";

        public string trilogieOrderId { get; set; } = "";

        public string totalRuntime { get; set; }

        [JsonIgnore]
        public DateTime startTime { get; set; }

        [JsonIgnore]
        public bool validSellWarehouse { get; set; } = true;

        [JsonIgnore]
        public bool exceedsShippingCost { get; set; } = false;

        [JsonIgnore]
        public bool inStockAtALocation { get; set; }

        public void SetTotalRuntime()
        {
            var span = DateTime.Now - startTime;

            totalRuntime = $"{span.Seconds} seconds";
        }
    }
}