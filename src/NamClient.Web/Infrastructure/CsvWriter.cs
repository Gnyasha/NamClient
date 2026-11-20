namespace NamClient.Web.Infrastructure;

public static class CsvWriter
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] CharactersNeedingQuotes = [',', '"', '\n', '\r'];

    public static Task WriteRowAsync(TextWriter writer, params string[] values) =>
        writer.WriteAsync(string.Join(',', values.Select(Escape)) + "\r\n");

    public static string Escape(string? value)
    {
        value ??= string.Empty;

        // Prefix ' so spreadsheets don't run the cell as a formula.
        if (value.Length > 0 && FormulaPrefixes.Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny(CharactersNeedingQuotes) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
