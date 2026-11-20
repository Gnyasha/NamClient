using System.Globalization;

namespace NamClient.Web.Infrastructure;

public static class DateDisplay
{
    public static string Local(DateTime? utc) =>
        utc is null ? string.Empty : AsUtc(utc.Value).ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static string Relative(DateTime utc)
    {
        var age = DateTime.UtcNow - AsUtc(utc);

        return age.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)age.TotalMinutes} min ago",
            < 60 * 24 => $"{(int)age.TotalHours} h ago",
            < 60 * 24 * 7 => (int)age.TotalDays == 1 ? "yesterday" : $"{(int)age.TotalDays} days ago",
            _ => AsUtc(utc).ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture),
        };
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
