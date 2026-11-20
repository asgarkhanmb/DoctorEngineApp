using DoctorEngineApp.Localization;
using System.ComponentModel.DataAnnotations;
using DoctorEngine.Contracts;
using DoctorEngineApp.Data;
using DoctorEngineApp.Services;
using DoctorEngineApp.Services.Interfaces;
using DoctorEngineApp.ViewModels.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DoctorEngine.Api.Controllers.V1
{
    /// <summary>
    /// Tətbiqdən rezervasiya. Saytdakı ilə eyni servis (IReservationService): eyni saat qaydaları,
    /// eyni yoxlamalar, admin paneldə canlı bildiriş (SignalR) — sayt və tətbiq rezervasiyaları bir yerdə.
    /// </summary>
    [ApiController]
    [Route("api/v1/reservations")]
    [Produces("application/json")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]   // yalnız JSON — CSRF riski yoxdur
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservations;
        private readonly AppDbContext _db;
        private readonly ILogger<ReservationsController> _logger;

        public ReservationsController(IReservationService reservations, AppDbContext db, ILogger<ReservationsController> logger)
        {
            _reservations = reservations;
            _db = db;
            _logger = logger;
        }

        /// <summary>Formanı qurmaq üçün: xidmətlər + saat qaydaları.</summary>
        [HttpGet("options")]
        public async Task<ActionResult<ReservationOptionsDto>> Options()
        {
            var services = await _db.Services.AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            var dtos = services.Select(s => new BookableServiceDto(s.Id, L.T(s.Name))).ToList();   // istifadəçinin dilində

            return Ok(new ReservationOptionsDto(dtos, new ReservationRulesDto(
                ReservationService.OpeningHour,
                ReservationService.ClosingHour,
                ReservationService.SlotMinutes,
                ReservationService.MaxDaysAhead,
                "Asia/Baku")));
        }

        /// <summary>Gün üçün saatlar: GET api/v1/reservations/slots?serviceId=5&amp;date=2026-11-14</summary>
        [HttpGet("slots")]
        public async Task<ActionResult<IReadOnlyList<SlotDto>>> Slots([FromQuery] int serviceId, [FromQuery] DateTime date)
        {
            if (serviceId <= 0) return Ok(Array.Empty<SlotDto>());
            var slots = await _reservations.GetSlotsAsync(serviceId, date);
            return Ok(slots.Select(s => new SlotDto(s.Time, s.State)).ToList());
        }

        [HttpPost]
        [EnableRateLimiting("auth")]   // spam qoruması: IP üzrə dəqiqədə 10
        public async Task<ActionResult<CreateReservationResponse>> Create([FromBody] CreateReservationRequest request)
        {
            var vm = new ReservationCreateVM
            {
                FullName = request.FullName?.Trim() ?? "",
                Phone = request.Phone?.Trim() ?? "",
                WhatsApp = request.WhatsApp?.Trim() ?? "",
                ServiceId = request.ServiceId,
                DateTime = request.DateTime,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
            };

            // Saytdakı formanın DataAnnotations qaydaları
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(vm, new ValidationContext(vm), errors, validateAllProperties: true))
                return BadRequest(new CreateReservationResponse(false, errors[0].ErrorMessage ?? L.T("Məlumatları düzgün doldurun!"), null));

            try
            {
                var result = await _reservations.TryCreateAsync(vm);
                var response = new CreateReservationResponse(result.Success, result.Message, result.TrackingCode);
                return result.Success ? Ok(response) : Conflict(response);   // məs. saat artıq tutulub
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tətbiqdən rezervasiya yaradılarkən xəta");
                return StatusCode(500, new CreateReservationResponse(false, L.T("Xəta baş verdi. Yenidən cəhd edin."), null));
            }
        }

        /// <summary>Statuslar (tətbiqdə saxlanan izləmə kodları ilə): GET ...?codes=A1B2&amp;codes=C3D4</summary>
        [HttpGet("status")]
        public async Task<ActionResult<IReadOnlyList<ReservationStatusDto>>> Status([FromQuery] string[] codes)
        {
            var items = await _reservations.LookupAsync((codes ?? Array.Empty<string>()).Take(50));
            return Ok(items.Select(i => new ReservationStatusDto(i.Code, i.Service, i.Date, i.Time, i.Status, i.Reason)).ToList());
        }
    }
}
