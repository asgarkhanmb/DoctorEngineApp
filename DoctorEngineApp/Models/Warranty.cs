using DoctorEngineApp.Models;

public class Warranty :BaseEntity
{
    public string CustomerName { get; set; }
    public string Phone { get; set; }

    public string Brand { get; set; }
    public string Model { get; set; }
    public int Year { get; set; }
    public string Fuel { get; set; }

    public string Master { get; set; }
    public string Terms { get; set; }

    public int? WarrantyLogoId { get; set; }
    public WarrantyLogo WarrantyLogo { get; set; }
    public ICollection<WarrantyJob> Jobs { get; set; }
}
