using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Who is logged in, kept in the MVC session (so it survives across every page).</summary>
public static class UserSessionExtensions
{
    public static void SetUser(this ISession session, AuthResponse user)
    {
        session.SetInt32("UserId", user.UserId);
        session.SetString("Role", user.Role);
        session.SetString("FullName", user.FullName);
        session.SetString("Email", user.Email);
    }

    public static int? GetUserId(this ISession session) => session.GetInt32("UserId");
    public static string? GetRole(this ISession session) => session.GetString("Role");
    public static string? GetFullName(this ISession session) => session.GetString("FullName");
    public static bool IsLoggedIn(this ISession session) => session.GetRole() != null;
}
