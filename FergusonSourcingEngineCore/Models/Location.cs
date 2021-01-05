using System;
using System.Collections.Generic;

namespace FergusonSourcingCore.Models
{
    public class Locations
    {
        // Key = branch number
        public Dictionary<string, Location> LocationDict = new Dictionary<string, Location>();
    }
    

    public class Location
    {
        public string BranchNumber { get; set; }

        public decimal? Distance { get; set; }

        public int? BusinessDaysInTransit { get; set; }

        public bool IsPreferred { get; set; } = false;

        public string Address1 { get; set; }

        public string Address2 { get; set; }

        public string City { get; set; }

        public string State { get; set; }

        public string Zip { get; set; }

        public bool WarehouseManagementSoftware { get; set; }

        public bool BranchLocation { get; set; }

        public bool DCLocation { get; set; }

        public bool SODLocation { get; set; }
        
        public bool ShipHub { get; set; }

        public bool OverpackCapable { get; set; }

        public string Logon { get; set; }

        public int ProcessingTime { get; set; }

        public ShippingAddress ShippingAddress
        {
            get 
            {
                return new ShippingAddress()
                {
                    addressLine1 = Address1.Replace("\n", "").Replace("\r", ""),
                    city = City.Replace("\n", "").Replace("\r", ""),
                    state = State.Replace("\n", "").Replace("\r", ""),
                    zip = Zip.Substring(0, 5),
                    branchNumber = BranchNumber
                };
            }
        }

        public bool SaturdayDelivery { get; set; }
        
        public DateTime EstShipDate { get; set; }

        public DateTime EstDeliveryDate { get; set; }

        public string FedExESTCutoffTimes { get; set; }
    }
}
