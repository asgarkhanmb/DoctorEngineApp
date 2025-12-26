using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Categories
{
    public class CategoryCreateVM
    {
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        public IFormFile IconFile { get; set; } = null!;
    }
}
