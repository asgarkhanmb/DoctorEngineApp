using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Categories
{
    public class CategoryEditVM
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; } = null!;
        public string? ExistingIcon { get; set; }
        public IFormFile? IconFile { get; set; }
    }
}
