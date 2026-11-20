using DoctorEngineApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReservationController : Controller
{
    private readonly IReservationService _reservationService;

    public ReservationController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _reservationService.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        await _reservationService.ConfirmAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Reject(int id)
    {
        ViewBag.Id = id;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            ModelState.AddModelError("reason", "İmtina səbəbini yazın.");
            ViewBag.Id = id;
            return View();
        }

        await _reservationService.RejectAsync(id, reason.Trim());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _reservationService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
