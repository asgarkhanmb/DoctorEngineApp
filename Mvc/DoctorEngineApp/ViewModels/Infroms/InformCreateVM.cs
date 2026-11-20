using DoctorEngineApp.Helpers;
namespace DoctorEngineApp.ViewModels.Infroms
{
    public class InformCreateVM
    {
        [AllowedUpload(UploadKind.Image)]
        public IFormFile? Icon { get; set; }
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Başlığı daxil edin")]
        [System.ComponentModel.DataAnnotations.StringLength(80)]
        public string Title { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Təsviri daxil edin")]
        [System.ComponentModel.DataAnnotations.StringLength(300)]
        public string Description { get; set; } = string.Empty;
    }
}
