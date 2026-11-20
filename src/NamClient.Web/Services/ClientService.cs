using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.Data.Paging;
using NamClient.Web.Models;
using NamClient.Web.Services.Search;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public class ClientService : IClientService
{
    public const int MaxBatchSize = 500;
    public const int PickerResultLimit = 20;

    private const int MaxCodeAttempts = 5;

    private static readonly Expression<Func<Client, ClientListItemViewModel>> ListProjection = c => new ClientListItemViewModel
    {
        ClientId = c.ClientId,
        Name = c.Name,
        ClientCode = c.ClientCode,
        LinkedContactCount = c.ClientContacts.Count(),
    };

    private static readonly IReadOnlyDictionary<string, Func<KeysetOrder<Client, ClientListItemViewModel>>> Orders =
        new Dictionary<string, Func<KeysetOrder<Client, ClientListItemViewModel>>>
        {
            ["name"] = () => new KeysetOrder<Client, ClientListItemViewModel>().By(c => c.Name, i => i.Name).By(c => c.ClientId, i => i.ClientId),
            ["-name"] = () => new KeysetOrder<Client, ClientListItemViewModel>(descending: true).By(c => c.Name, i => i.Name).By(c => c.ClientId, i => i.ClientId),
            ["code"] = () => new KeysetOrder<Client, ClientListItemViewModel>().By(c => c.ClientCode, i => i.ClientCode).By(c => c.ClientId, i => i.ClientId),
            ["-code"] = () => new KeysetOrder<Client, ClientListItemViewModel>(descending: true).By(c => c.ClientCode, i => i.ClientCode).By(c => c.ClientId, i => i.ClientId),
        };

    private readonly AppDbContext _db;
    private readonly IClientCodeGenerator _codeGenerator;

    public ClientService(AppDbContext db, IClientCodeGenerator codeGenerator)
    {
        _db = db;
        _codeGenerator = codeGenerator;
    }

    public string NormaliseSort(string? sort) =>
        sort is not null && Orders.ContainsKey(sort) ? sort : "name";

    public async Task<PagedResult<ClientListItemViewModel>> SearchAsync(PageRequest request, CancellationToken cancellationToken = default)
    {
        var filtered = ClientSearch.Apply(_db.Clients.AsNoTracking(), _db, request.Search);
        var order = Orders[NormaliseSort(request.Sort)]();

        var page = await filtered.ToKeysetPageAsync(order, ListProjection, request, cancellationToken);
        var (total, isLowerBound) = await filtered.CountForListAsync(request.HasSearch, cancellationToken);

        return page with { TotalCount = total, TotalIsLowerBound = isLowerBound };
    }

    public IAsyncEnumerable<ClientListItemViewModel> ExportAsync(string? search, string? sort, IReadOnlyCollection<int>? ids)
    {
        var query = ids is { Count: > 0 }
            ? _db.Clients.AsNoTracking().Where(c => ids.Contains(c.ClientId))
            : ClientSearch.Apply(_db.Clients.AsNoTracking(), _db, search);

        return Orders[NormaliseSort(sort)]()
            .ApplyOrder(query)
            .Select(ListProjection)
            .AsAsyncEnumerable();
    }

    public Task<Client?> FindAsync(int clientId, CancellationToken cancellationToken = default) =>
        _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

    public async Task<Client> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        var client = new Client { Name = trimmed, NameWords = ClientSearch.WordsFor(trimmed) };
        _db.Clients.Add(client);

        for (var attempt = 1; ; attempt++)
        {
            client.ClientCode = await _codeGenerator.GenerateAsync(client.Name, cancellationToken);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return client;
            }
            // Code taken by a concurrent save: generate the next one and retry.
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation() && attempt < MaxCodeAttempts)
            {
            }
        }
    }

    public async Task<bool> UpdateAsync(int clientId, string name, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients
            .Include(c => c.NameWords)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client is null)
        {
            return false;
        }

        client.Name = name.Trim();

        var newWords = SearchText.Words(client.Name).ToHashSet();
        foreach (var stale in client.NameWords.Where(w => !newWords.Contains(w.Word)).ToList())
        {
            client.NameWords.Remove(stale);
        }

        foreach (var word in newWords.Except(client.NameWords.Select(w => w.Word)))
        {
            client.NameWords.Add(new ClientNameWord { Word = word });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> DeleteAsync(IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default)
    {
        var ids = clientIds.Distinct().Take(MaxBatchSize).ToList();

        return _db.Clients
            .Where(c => ids.Contains(c.ClientId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedContactViewModel>> GetLinkedContactsAsync(int clientId, CancellationToken cancellationToken = default) =>
        await _db.ClientContacts
            .AsNoTracking()
            .Where(cc => cc.ClientId == clientId)
            .Select(cc => cc.Contact)
            .OrderBy(c => c.Surname).ThenBy(c => c.Name).ThenBy(c => c.ContactId)
            .Select(c => new LinkedContactViewModel(c.ContactId, c.Surname + " " + c.Name, c.Email))
            .ToListAsync(cancellationToken);

    public async Task<LinkOptionsViewModel> SearchLinkableContactsAsync(int clientId, string? search, CancellationToken cancellationToken = default)
    {
        var options = await ContactSearch.Apply(_db.Contacts.AsNoTracking(), search)
            .Where(c => !c.ClientContacts.Any(cc => cc.ClientId == clientId))
            .OrderBy(c => c.Surname).ThenBy(c => c.Name).ThenBy(c => c.ContactId)
            .Select(c => new LinkOptionViewModel(c.ContactId, c.Surname + " " + c.Name, c.Email))
            .Take(PickerResultLimit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = options.Count > PickerResultLimit;
        return new LinkOptionsViewModel(options.Take(PickerResultLimit).ToList(), hasMore, search);
    }
}
