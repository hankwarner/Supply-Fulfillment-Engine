using System.Collections.Generic;
using System.Linq;

namespace FergusonSourcingCore.Models
{
    public partial class ManualOrder
    {
        public ManualOrder(AtgOrderRes order, TrilogieRequest trilogieReq = null)
        {
            id = order.id;
            atgOrderId = order.atgOrderId;
            custAccountId = order.custAccountId;
            customerId = order.customerId;
            customerName = order.customerName;
            orderSubmitDate = order.orderSubmitDate;
            sellWhse = order.sellWhse ?? "D98 DISTRIBUTION CENTERS";
            sourcingMessage = order.sourcingMessage;
            sourceSystem = order.sourceSystem;
            orderRequiredDate = order.orderRequiredDate;
            trilogieErrorMessage = trilogieReq?.TrilogieErrorMessage ?? "";
            trilogieOrderId = trilogieReq?.TrilogieOrderId ?? "";
            trilogieStatus = trilogieReq?.TrilogieStatus.ToString() ?? "Waiting on Trilogie response.";

            paymentOnAccount = new ManualPaymentOnAccount()
            {
                payment = new ManualPayment()
                {
                    address1 = order.paymentOnAccount?.First().address1,
                    address2 = order.paymentOnAccount?.First().address2,
                    cardType = order.paymentOnAccount?.First().cardType,
                    city = order.paymentOnAccount?.First().city,
                    state = order.paymentOnAccount?.First().state,
                    zip = order.paymentOnAccount?.First().zip,
                    phone = order.paymentOnAccount?.First().phone
                }
            };

            shipping = new ManualShipping()
            {
                price = order.shipping?.price,
                shipViaCode = order.shipping?.shipViaCode,
                shipVia = order.shipping?.shipVia,
                shipTo = new ManualShipTo()
                {
                    address1 = order.shipping.shipTo?.address1,
                    address2 = order.shipping.shipTo?.address2,
                    city = order.shipping.shipTo?.city,
                    country = order.shipping.shipTo?.country,
                    name = order.shipping.shipTo?.name,
                    shipInstructionsPhoneNumberAreaDialing = order.shipping.shipTo?.shipInstructionsPhoneNumberAreaDialing,
                    shipInstructionsPhoneNumberDialNumber = order.shipping.shipTo?.shipInstructionsPhoneNumberDialNumber,
                    state = order.shipping.shipTo?.state,
                    zip = order.shipping.shipTo?.zip
                }
            };

            sourcing = order.items.GroupBy(item =>
                item.shipFrom,
                item => item,
                (key, group) => new Sourcing()
                {
                    shipFrom = key,
                    sourceComplete = order.sourceComplete,
                    items = group.Select(item => new ManualItem()
                    {
                        lineItemId = item.lineId,
                        unitPrice = item.unitPrice,
                        unitPriceCode = item.unitPriceCode,
                        extendedPrice = item.extendedPrice,
                        description = item.itemDescription,
                        quantity = item.quantity,
                        sourcingMessage = item.sourcingMessage,
                        masterProdId = item.masterProdId,
                        sourcingGuide = item.sourcingGuide,
                        vendor = item.vendor,
                        preferredShipVia = item.preferredShipVia,
                        alt1Code = item.alt1Code
                    }).ToList()
                }).ToList();
        }

        public ManualOrder(){ }

        public string atgOrderId { get; set; }
        public string custAccountId { get; set; }
        public string customerId { get; set; }
        public string customerName { get; set; }
        public string orderSubmitDate { get; set; }
        public string orderRequiredDate { get; set; }
        public string sellWhse { get; set; }
        public string sellLogon { get; set; }
        public string sourcingMessage { get; set; }
        public string processOrderType { get; set; } = "manual";
        public bool claimed { get; set; } = false;
        public string timeClaimed { get; set; } = null;
        public bool orderComplete { get; set; } = false;
        public string timeCompleted { get; set; } = null;
        public ManualPaymentOnAccount paymentOnAccount { get; set; }
        public ManualShipping shipping { get; set; }
        public List<Sourcing> sourcing { get; set; }
        public string sourceSystem { get; set; }
        public string id { get; set; }
        public string notes { get; set; }
        public string trilogieErrorMessage { get; set; } = "";
        public string trilogieStatus { get; set; } = "Waiting on Trilogie response.";
        public string trilogieOrderId { get; set; } = "";
    }

    public partial class ManualPaymentOnAccount
    {
        public ManualPayment payment { get; set; }
    }

    public partial class ManualPayment
    {
        public string cardType { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string zip { get; set; }
        public string phone { get; set; }
    }

    public partial class ManualShipping
    {
        public string shipViaCode { get; set; }
        public string shipVia { get; set; }
        public string price { get; set; }
        public ManualShipTo shipTo { get; set; }
    }

    public partial class ManualShipTo
    {
        public string name { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string country { get; set; }
        public string state { get; set; }
        public string zip { get; set; }
        public string shipInstructionsPhoneNumberAreaDialing { get; set; }
        public string shipInstructionsPhoneNumberDialNumber { get; set; }
    }

    public partial class Sourcing
    {
        public string shipFrom { get; set; }
        public string shipFromLogon { get; set; }
        public bool sourceComplete { get; set; }
        public List<ManualItem> items { get; set; }
    }

    public partial class ManualItem
    {
        public int lineItemId { get; set; }
        public string unitPrice { get; set; }
        public string unitPriceCode { get; set; }
        public string extendedPrice { get; set; }
        public string description { get; set; }
        public string quantity { get; set; }
        public string sourcingMessage { get; set; }
        public string masterProdId { get; set; }
        public bool itemComplete { get; set; } = false;
        public string sourcingGuide { get; set; }
        public string vendor { get; set; }
        public string preferredShipVia { get; set; }
        public string alt1Code { get; set; }
    }

}
