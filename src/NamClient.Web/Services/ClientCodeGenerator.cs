using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;

namespace NamClient.Web.Services;

public class ClientCodeGenerator : IClientCodeGenerator
{
    public const int PrefixLength = 3;
    public const int MaxNumber = 999;

    private readonly AppDbContext _db;

    public ClientCodeGenerator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateAsync(string clientName, CancellationToken cancellationToken = default)
    {
        var prefix = BuildPrefix(clientName);

        var existingCodes = await _db.Clients
            .AsNoTracking()
            .Where(c => c.ClientCode.StartsWith(prefix))
            .Select(c => c.ClientCode)
            .ToListAsync(cancellationToken);

        return NextAvailableCode(prefix, existingCodes);
    }

    // Initials for 3+ words (First National Bank: FNB), otherwise the first 3 letters (Protea: PRO), padded with A, B, C (IT: ITA).
    public static string BuildPrefix(string clientName)
    {
        var words = (clientName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(LettersOnly)
            .Where(w => w.Length > 0)
            .ToList();

        var letters = words.Count >= PrefixLength
            ? string.Concat(words.Take(PrefixLength).Select(w => w[0]))
            : string.Concat(words);

        if (letters.Length > PrefixLength)
        {
            letters = letters[..PrefixLength];
        }

        var filler = 'A';
        while (letters.Length < PrefixLength)
        {
            letters += filler++;
        }

        return letters;
    }

    public static string NextAvailableCode(string prefix, IEnumerable<string> existingCodes)
    {
        var taken = new HashSet<string>(existingCodes.Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase);

        for (var number = 1; number <= MaxNumber; number++)
        {
            var candidate = $"{prefix}{number:D3}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"All client codes for the prefix '{prefix}' are in use.");
    }

    private static string LettersOnly(string word) =>
        new(word.ToUpperInvariant().Where(ch => ch is >= 'A' and <= 'Z').ToArray());
}
