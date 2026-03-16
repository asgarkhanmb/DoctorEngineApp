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
    public async Task<IActionResult> Reject(int id, string reason)
    {
        await _reservationService.RejectAsync(id, reason);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        await _reservationService.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
