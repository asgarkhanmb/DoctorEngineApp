using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.Maps
{
    public class MapVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ünvan tələb olunur")]
        public string Address { get; set; } = string.Empty;

        // iframe src-ə yazılır: yalnız https linklərə icazə (javascript:/data: qadağandır)
        [Required(ErrorMessage = "Xəritə linki tələb olunur")]
        [RegularExpression(@"^https://[^\s""'<>]+$", ErrorMessage = "Xəritə linki https:// ilə başlamalıdır")]
        public string MapUrl { get; set; } = string.Empty;
    }
}
