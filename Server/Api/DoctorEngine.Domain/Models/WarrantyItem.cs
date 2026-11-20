namespace DoctorEngineApp.Models
{
    public class WarrantyItem :BaseEntity
    {
        public string PartName { get; set; }    
        public int WarrantyMonth { get; set; }  

        public int WarrantyId { get; set; }
        public Warranty Warranty { get; set; }
    }
}
