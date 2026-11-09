using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireRole("Participant")]
public class ProfileController : BaseController
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private readonly ApiClient _api;

    public ProfileController(ApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<ParticipantProfile>("api/profile");

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success || result.Data == null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", "Home");
        }

        var p = result.Data;
        return View(new ProfileViewModel
        {
            FullName = p.FullName,
            Email = p.Email,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            EmergencyContactName = p.EmergencyContactName,
            EmergencyContactPhone = p.EmergencyContactPhone,
            ProfilePictureUrl = p.ProfilePictureUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(ProfileViewModel vm)
    {
        if (!ModelState.IsValid) return View("Index", vm);

        var result = await _api.PutAsync<ParticipantProfile>("api/profile", new
        {
            vm.FullName, vm.DateOfBirth, vm.Gender, vm.EmergencyContactName, vm.EmergencyContactPhone
        });

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (!result.Success || result.Data == null)
        {
            ModelState.AddModelError(string.Empty, result.Error);
            return View("Index", vm);
        }

        HttpContext.Session.SetString("FullName", result.Data.FullName);   // keeps the top bar up to date
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPicture(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Index));
        }
        if (file.Length > MaxImageBytes)
        {
            TempData["Error"] = "That image is too big. Please choose one that is 5 MB or smaller.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _api.UploadAsync<PictureResponse>("api/profile/picture", file);

        var redirect = SessionExpired(result);
        if (redirect != null) return redirect;

        if (result.Success) TempData["Success"] = "Your profile picture has been updated.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Index));
    }
}
