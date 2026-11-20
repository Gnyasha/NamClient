using System.Text;
using NamClient.Web.Models;

namespace NamClient.Web.Services.Search;

public static class SearchText
{
    public const int MaxInputLength = 100;
    public const int MaxTerms = 5;

    public static IReadOnlyList<string> Words(string? text)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                Flush(current, words);
            }
        }

        Flush(current, words);
        return words;
    }

    public static IReadOnlyList<string> Terms(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Clip(text)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .Take(MaxTerms)
            .ToList();
    }

    public static IReadOnlyList<string> NameTerms(string? text) =>
        Words(text is null ? null : Clip(text)).Take(MaxTerms).ToList();

    private static string Clip(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length > MaxInputLength ? trimmed[..MaxInputLength] : trimmed;
    }

    private static void Flush(StringBuilder current, List<string> words)
    {
        if (current.Length == 0)
        {
            return;
        }

        var word = current.Length > ClientNameWord.MaxLength
            ? current.ToString(0, ClientNameWord.MaxLength)
            : current.ToString();

        if (!words.Contains(word))
        {
            words.Add(word);
        }

        current.Clear();
    }
}
