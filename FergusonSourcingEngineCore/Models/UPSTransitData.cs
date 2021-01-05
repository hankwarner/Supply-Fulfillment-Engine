
namespace FergusonSourcingCore.Models
{
    public class UPSTransitData
    {
        public UPSTransitData() { }

        public string BranchNumber { get; set; }

        public int? BusinessTransitDays { get; set; }

        public bool? SaturdayDelivery { get; set; }
    }
}
