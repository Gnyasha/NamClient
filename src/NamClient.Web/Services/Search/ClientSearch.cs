using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.Models;

namespace NamClient.Web.Services.Search;

/// <summary>Every term must match the start of a name word or the client code; both are index seeks.</summary>
public static class ClientSearch
{
    public static IQueryable<Client> Apply(IQueryable<Client> clients, AppDbContext db, string? search)
    {
        var terms = SearchText.NameTerms(search);
        if (terms.Count > 0)
        {
            clients = clients.TagWith(SearchQueryHintInterceptor.Tag);
        }

        foreach (var term in terms)
        {
            var matchingIds = db.ClientNameWords
                .Where(w => w.Word.StartsWith(term))
                .Select(w => w.ClientId);

            if (term.Length <= AppDbContext.ClientCodeLength)
            {
                matchingIds = matchingIds.Union(db.Clients
                    .Where(c => c.ClientCode.StartsWith(term))
                    .Select(c => c.ClientId));
            }

            clients = clients.Where(c => matchingIds.Contains(c.ClientId));
        }

        return clients;
    }

    public static List<ClientNameWord> WordsFor(string name) =>
        SearchText.Words(name).Select(w => new ClientNameWord { Word = w }).ToList();
}
