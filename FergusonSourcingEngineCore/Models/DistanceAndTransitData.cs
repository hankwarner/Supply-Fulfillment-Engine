
namespace FergusonSourcingCore.Models
{
    public class DistanceAndTransitData
    {
        public string BranchNumber { get; set; }

        public string ZipCode { get; set; }

        public decimal? DistanceFromZip { get; set; }

        public int? BusinessTransitDays { get; set; }

        public bool? SaturdayDelivery { get; set; }
    }
}
