using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApiClient _api;

    public AccountController(ApiClient api) => _api = api;

    // ---------- Login ----------

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.IsLoggedIn()) return GoHome();
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.PostAsync<AuthResponse>("api/auth/login", new { vm.Email, vm.Password });
        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(vm);
        }

        // Note: no Session.Clear() here. ApiClient has just stored the API's session cookie
        // in this same session, and clearing would throw it away.
        HttpContext.Session.SetUser(result.Data);
        TempData["Success"] = $"Welcome back, {result.Data.FullName}!";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return GoHome();
    }

    // ---------- Register as a participant ----------

    [HttpGet]
    public IActionResult RegisterParticipant()
    {
        if (HttpContext.Session.IsLoggedIn()) return GoHome();
        return View(new RegisterParticipantViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterParticipant(RegisterParticipantViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.PostAsync<AuthResponse>("api/auth/register/participant", new
        {
            vm.FullName, vm.Email, vm.Password, vm.DateOfBirth,
            vm.Gender, vm.EmergencyContactName, vm.EmergencyContactPhone
        });

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(vm);
        }

        HttpContext.Session.SetUser(result.Data);
        TempData["Success"] = $"Welcome to RaceDay, {result.Data.FullName}! Your account is ready.";
        return GoHome();
    }

    // ---------- Register as an organiser ----------

    [HttpGet]
    public IActionResult RegisterOrganiser()
    {
        if (HttpContext.Session.IsLoggedIn()) return GoHome();
        return View(new RegisterOrganiserViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterOrganiser(RegisterOrganiserViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _api.PostAsync<AuthResponse>("api/auth/register/organiser", new
        {
            vm.FullName, vm.Email, vm.Password, vm.OrganisationName, vm.PhoneNumber
        });

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View(vm);
        }

        HttpContext.Session.SetUser(result.Data);
        TempData["Success"] = $"Welcome to RaceDay, {result.Data.FullName}! Your organiser account is ready.";
        return GoHome();
    }

    // ---------- Logout ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (HttpContext.Session.IsLoggedIn())
            await _api.PostAsync<MessageDto>("api/auth/logout");   // end the API session too

        HttpContext.Session.Clear();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // Organisers land on their dashboard, everyone else on the events page.
    private IActionResult GoHome() =>
        HttpContext.Session.GetRole() == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
