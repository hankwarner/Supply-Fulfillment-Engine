using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace FergusonSourcingCore.Models
{
    public class BillToCustomer
    {
        public long SiteUseId { get; set; }
        public string PartyName { get; set; }
        public object ContactId { get; set; }
        public string AccountNumber { get; set; }
        public string Address1 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

    }

    public class ShipToCustomer
    {
        public long PartyId { get; set; }
        public object ContactId { get; set; }
        public string PartyName { get; set; }
        public long SiteId { get; set; }
        public string Address1 { get; set; }
        public object Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

    }

    public class HeaderEffBCustomerTypeprivateVO
    {
        public string ContextCode { get; set; }
        public string customerType { get; set; }

    }

    public class HeaderEffBFEAdditionalInformationprivateVO
    {
        public string jobName { get; set; }
        public string orderedBy { get; set; }
        public string masterProjectNo { get; set; }
        public string customerNotes { get; set; }
        public long sellOrganization { get; set; }
        public string pickUpType { get; set; }
        public string backorder { get; set; }

    }

    public class Payments
    {
        public string cardType { get; set; }
        public string cardExpireDate { get; set; }
        public string cardToken { get; set; }
        public string cardMaskedNumber { get; set; }
        public string cardHolderName { get; set; }
        public string Address { get; set; }
        public string city { get; set; }
        public string state { get; set; }
        public string zip { get; set; }

    }

    public class HeaderEffBFETrilogieInformationprivateVO
    {
        public string shipInstructionsPhoneNumberAreaDialing { get; set; }
        public string shipInstructionsPhoneNumberDialNumber { get; set; }
        public string specialShippingInstructions { get; set; }
        public string userEmail { get; set; }
        public string attentionOfName { get; set; }
        public string shippingPrice { get; set; }
        public string freightCharge { get; set; }
        public string inboundFreightCharge { get; set; }
        public string shippingTax { get; set; }
        public string autoReplenish { get; set; }
        public string sourceSystem { get; set; }
        public string writerInitials { get; set; }
        public string showroomFlag { get; set; }
        public string showroomSalesPersonCode { get; set; }
        public string salesPersonCode { get; set; }
        public string alternateSalesPersonCode { get; set; }
        public string contractNumber { get; set; }
        public DateTime bidExpireDate { get; set; }
        public Payments payments { get; set; }

    }

    public class AdditionalInformation
    {
        public string Category { get; set; }
        public List<HeaderEffBCustomerTypeprivateVO> HeaderEffBCustomer__TypeprivateVO { get; set; }
        public List<HeaderEffBFEAdditionalInformationprivateVO> HeaderEffBFE__Additional__InformationprivateVO { get; set; }
        public List<HeaderEffBFETrilogieInformationprivateVO> HeaderEffBFE__Trilogie__InformationprivateVO { get; set; }

    }

    public class Header
    {
        public string TransactionalCurrCode { get; set; }
        public string TransactionalCurrName { get; set; }
        public string TransactionTypeCode { get; set; }
        public string TransactionType { get; set; }
        public object BuyingPartyContactId { get; set; }
        public string BusinessUnitName { get; set; }
        public string BuyingPartyName { get; set; }
        public long BuyingPartyId { get; set; }
        public string Comments { get; set; }
        public bool PartialShipAllowedFlag { get; set; }
        public bool FreezePriceFlag { get; set; }
        public bool FreezeShippingChargeFlag { get; set; }
        public bool FreezeTaxFlag { get; set; }
        public bool SubmittedFlag { get; set; }
        public object CustomerPONumber { get; set; }
        public DateTime RequestedShipDate { get; set; }
        public object ShippingMode { get; set; }
        public object ShippingInstructions { get; set; }
        public object PackingInstructions { get; set; }
        public long RequestingBusinessUnitId { get; set; }
        public long RequestingLegalEntityId { get; set; }
        public long RequestedFulfillmentOrganizationId { get; set; }
        public string RequestedFulfillmentOrganizationName { get; set; }
        public string SalesChannel { get; set; }
        public string SalesChannelCode { get; set; }
        public bool SourceComplete { get; set; }
        public string SourcingMessage { get; set; }
        public string PaymentTerms { get; set; }
        public DateTime TransactionOn { get; set; }
        public List<BillToCustomer> billToCustomer { get; set; }
        public List<ShipToCustomer> shipToCustomer { get; set; }
        public List<AdditionalInformation> additionalInformation { get; set; }

    }

    public class BillToCustomer2
    {
        public long SiteUseId { get; set; }
        public string PartyName { get; set; }
        public object ContactId { get; set; }
        public string AccountNumber { get; set; }
        public string Address1 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

    }

    public class ShipToCustomer2
    {
        public long PartyId { get; set; }
        public object ContactId { get; set; }
        public string PartyName { get; set; }
        public long SiteId { get; set; }
        public string Address1 { get; set; }
        public object Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

    }

    public class AssociatedComment
    {
        public string cmtTypeCode { get; set; }
        public string lineAssocComment { get; set; }

    }

    public class FulfillLineEffBECOMMprivateVO
    {
        public string ContextCode { get; set; }
        public int promotionId { get; set; }
        public int multiplier { get; set; }
        public string amount { get; set; }
        public string leadLawFlag { get; set; }
        public string leadLawCode { get; set; }
        public string adaFlag { get; set; }
        public string greenFlag { get; set; }
        public string priceColumn { get; set; }
        public string priceFormula { get; set; }
        public string description { get; set; }
        public string commentDetailSequence { get; set; }
        public List<AssociatedComment> associatedComments { get; set; }

    }

    public class AdditionalInformation2
    {
        public string Category { get; set; }
        public List<FulfillLineEffBECOMMprivateVO> FulfillLineEffBECOMMprivateVO { get; set; }

    }

    public class Line
    {
        public string SourceTransactionLineId { get; set; }
        public string SourceTransactionScheduleId { get; set; }
        public string SourceTransactionLineNumber { get; set; }
        public string SourceScheduleNumber { get; set; }
        public string OrderedUOMCode { get; set; }
        public int OrderedQuantity { get; set; }
        public string ProductId { get; set; }
        public string TransactionCategoryCode { get; set; }
        public string ShipSetName { get; set; }
        public bool PartialShipAllowedFlag { get; set; }
        public string Comments { get; set; }
        public DateTime RequestedShipDate { get; set; }
        public string ShipFrom { get; set; }
        public object ShippingInstructions { get; set; }
        public string SourcingMessage { get; set; }
        public object PackingInstructions { get; set; }
        public List<BillToCustomer2> billToCustomer { get; set; }
        public List<ShipToCustomer2> shipToCustomer { get; set; }
        public List<AdditionalInformation2> additionalInformation { get; set; }

    }

    public class FergusonOrder
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string SourceTransactionNum { get; set; }
        public string SourceTransactionSys { get; set; }
        public string SourceTransactionId { get; set; }
        public DateTime SourceOrderEntryDate { get; set; }
        public Header header { get; set; }
        public List<Line> lines { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}