using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class DistanceData
    {
        public DistanceData(string branchNum, double? distance, int? transitDays, bool? saturdayDelivery)
        {
            BranchNumber = branchNum;
            DistanceFromZip = distance;
            BusinessTransitDays = transitDays;
            SaturdayDelivery = saturdayDelivery;
        }
        
        public string BranchNumber { get; set; }

        public double? DistanceFromZip { get; set; }

        public int? BusinessTransitDays { get; set; }

        public bool? SaturdayDelivery { get; set; } = false;

        public string Error { get; set; } = null;
    }
}
