using DoctorEngineApp.ViewModels.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace DoctorEngineApp.ViewModels.ContactInfos
{
    public class ContactInfoVm
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Ünvanı daxil edin")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefonu daxil edin")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email daxil edin")]
        [EmailAddress(ErrorMessage = "Email düzgün deyil")]
        public string Email { get; set; } = string.Empty;

        // Sosial şəbəkələr ixtiyaridir — boş qalarsa saytda ikon göstərilmir
        [Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")]
        public string? YouTube { get; set; }

        [Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")]
        public string? Facebook { get; set; }

        [Url(ErrorMessage = "Link düzgün deyil (https:// ilə başlamalıdır)")]
        public string? TikTok { get; set; }

        public string? WhatsApp { get; set; }

        [Required(ErrorMessage = "İş saatlarını daxil edin")]
        public string Weekdays { get; set; } = string.Empty;

        [Required(ErrorMessage = "İş saatlarını daxil edin")]
        public string Weekend { get; set; } = string.Empty;

        [ValidateNever]
        public List<ServiceVM> Services { get; set; } = new();
    }
}
