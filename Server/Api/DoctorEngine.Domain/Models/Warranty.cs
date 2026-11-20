namespace DoctorEngineApp.Models
{
    public class Warranty:BaseEntity
    {
        public string CustomerFullName { get; set; } 
        public string VehicleModel { get; set; }     
        public int VehicleYear { get; set; }         
        public string Terms { get; set; }          
        public List<WarrantyItem> Items { get; set; }
    }
}
