namespace DoctorEngineApp.ViewModels.Warranties
{
    public class JobVM
    {
        public int JobId { get; set; }
        public string Name { get; set; }

        public int WarrantyMonths { get; set; }
        public bool IsSelected { get; set; }
    }
}
