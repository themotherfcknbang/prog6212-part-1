namespace RaceDay.Web.Services;

public static class Formatting
{
    /// <summary>47 -> "47th", 1 -> "1st", 12 -> "12th"</summary>
    public static string Ordinal(int n)
    {
        var teen = n % 100 >= 11 && n % 100 <= 13;
        var suffix = teen ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{n}{suffix}";
    }

    public static string StatusLabel(string status) => status switch
    {
        "DNF" => "Did not finish",
        "DNS" => "Did not start",
        "DSQ" => "Disqualified",
        _ => status
    };
}
