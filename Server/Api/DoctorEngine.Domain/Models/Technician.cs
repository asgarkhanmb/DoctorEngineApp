namespace DoctorEngineApp.Models
{
    public class Technician :BaseEntity
    {
        public string FullName { get; set; }
        public string Position { get; set; }

        // Haqqımızda səhifəsində usta kartındakı qısa təqdimat mətni (admin paneldən)
        [System.ComponentModel.DataAnnotations.MaxLength(300)]
        public string? Bio { get; set; }

        public string Image { get; set; }

        public string FacebookUrl { get; set; }
        public string InstagramUrl { get; set; }
        public string TiktokUrl { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
