using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a controller or action. [RequireRole] = any logged-in user,
/// [RequireRole("Organiser")] = organisers only.
/// Not logged in -> sent to the login page. Wrong role -> sent to "access denied".
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireRoleAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var role = http.Session.GetRole();

        if (role == null)
        {
            var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
            tempData["Warning"] = "Please log in to continue.";

            var returnUrl = http.Request.Method == HttpMethods.Get
                ? http.Request.Path + http.Request.QueryString
                : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
    }
}
