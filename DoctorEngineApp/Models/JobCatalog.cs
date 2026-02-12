namespace DoctorEngineApp.Models
{
    public class JobCatalog :BaseEntity
    {
        public string Name { get; set; }
        public int DefaultWarrantyMonths { get; set; }
    }
}
