using Microsoft.AspNetCore.Mvc.Rendering;

namespace DoctorEngineApp.ViewModels.Warranties
{
    public class WarrantyCreateVM
    {
        public string CustomerName { get; set; }
        public string Phone { get; set; }

        public string Brand { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string Fuel { get; set; }

        public string Master { get; set; }
        public string Terms { get; set; }

        // SEÇİLMİŞ LOGO (DB üçün)
        public int? WarrantyLogoId { get; set; }

        // YENİ LOGO YÜKLƏMƏ (FORM üçün)
        public IFormFile LogoFile { get; set; }

        public List<JobVM> Jobs { get; set; }
        public List<SelectListItem> Logos { get; set; }

    }
}
