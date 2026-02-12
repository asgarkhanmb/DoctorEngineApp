namespace DoctorEngineApp.Models
{
    public class WarrantyJob:BaseEntity
    {
        public int WarrantyId { get; set; }
        public Warranty Warranty { get; set; }

        public int JobCatalogId { get; set; }
        public JobCatalog JobCatalog { get; set; }

        public int WarrantyMonths { get; set; }
    }
}
