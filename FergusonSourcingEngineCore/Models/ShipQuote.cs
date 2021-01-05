using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class ShipQuoteRequest
    {
        public string RateType { get; set; }

        public ShippingAddress DestinationAddress { get; set; }

        public ShippingAddress OriginAddress { get; set; }

        public Package Package { get; set; }
    }


    public class Package
    {
        public double Weight { get; set; }
    }
}
