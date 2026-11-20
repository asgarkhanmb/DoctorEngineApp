using DoctorEngineApp.Models;

namespace DoctorEngineApp.ViewModels.Services
{
    public class SubServiceVM
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? Video { get; set; }
        public string? CategoryName { get; set; }
        public int CategoryId { get; set; }

        /// <summary>Bu kateqoriyada başqa (əsas) video var — bu saytda göstərilmir.</summary>
        public bool IsExtra { get; set; }
        public List<Inform> Informs { get; set; } = new List<Inform>();
    }
}
