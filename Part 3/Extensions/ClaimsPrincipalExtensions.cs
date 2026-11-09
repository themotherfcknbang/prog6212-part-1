using System.Security.Claims;

namespace RaceDay.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetRole(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role)!;
}