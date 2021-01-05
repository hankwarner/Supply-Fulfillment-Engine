using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class ShippingAddress
    {
        public string branchNumber { get; set; }

        public string addressLine1 { get; set; }

        public string addressLine2 { get; set; }

        public string city { get; set; }

        public string state { get; set; }

        public string zip { get; set; }
    }
}
