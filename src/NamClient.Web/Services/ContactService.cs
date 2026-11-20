using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.Data.Paging;
using NamClient.Web.Models;
using NamClient.Web.Services.Search;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public class ContactService : IContactService
{
    private static readonly Expression<Func<Contact, ContactListItemViewModel>> ListProjection = c => new ContactListItemViewModel
    {
        ContactId = c.ContactId,
        Name = c.Name,
        Surname = c.Surname,
        Email = c.Email,
        LinkedClientCount = c.ClientContacts.Count(),
    };

    private static readonly IReadOnlyDictionary<string, Func<KeysetOrder<Contact, ContactListItemViewModel>>> Orders =
        new Dictionary<string, Func<KeysetOrder<Contact, ContactListItemViewModel>>>
        {
            ["name"] = () => new KeysetOrder<Contact, ContactListItemViewModel>()
                .By(c => c.Surname, i => i.Surname).By(c => c.Name, i => i.Name).By(c => c.ContactId, i => i.ContactId),
            ["-name"] = () => new KeysetOrder<Contact, ContactListItemViewModel>(descending: true)
                .By(c => c.Surname, i => i.Surname).By(c => c.Name, i => i.Name).By(c => c.ContactId, i => i.ContactId),
            ["email"] = () => new KeysetOrder<Contact, ContactListItemViewModel>()
                .By(c => c.Email, i => i.Email).By(c => c.ContactId, i => i.ContactId),
            ["-email"] = () => new KeysetOrder<Contact, ContactListItemViewModel>(descending: true)
                .By(c => c.Email, i => i.Email).By(c => c.ContactId, i => i.ContactId),
        };

    private readonly AppDbContext _db;

    public ContactService(AppDbContext db)
    {
        _db = db;
    }

    public string NormaliseSort(string? sort) =>
        sort is not null && Orders.ContainsKey(sort) ? sort : "name";

    public async Task<PagedResult<ContactListItemViewModel>> SearchAsync(PageRequest request, CancellationToken cancellationToken = default)
    {
        var filtered = ContactSearch.Apply(_db.Contacts.AsNoTracking(), request.Search);
        var order = Orders[NormaliseSort(request.Sort)]();

        var page = await filtered.ToKeysetPageAsync(order, ListProjection, request, cancellationToken);
        var (total, isLowerBound) = await filtered.CountForListAsync(request.HasSearch, cancellationToken);

        return page with { TotalCount = total, TotalIsLowerBound = isLowerBound };
    }

    public IAsyncEnumerable<ContactListItemViewModel> ExportAsync(string? search, string? sort, IReadOnlyCollection<int>? ids)
    {
        var query = ids is { Count: > 0 }
            ? _db.Contacts.AsNoTracking().Where(c => ids.Contains(c.ContactId))
            : ContactSearch.Apply(_db.Contacts.AsNoTracking(), search);

        return Orders[NormaliseSort(sort)]()
            .ApplyOrder(query)
            .Select(ListProjection)
            .AsAsyncEnumerable();
    }

    public Task<Contact?> FindAsync(int contactId, CancellationToken cancellationToken = default) =>
        _db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.ContactId == contactId, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, int? excludeContactId = null, CancellationToken cancellationToken = default)
    {
        var normalised = email.Trim();

        return _db.Contacts.AnyAsync(
            c => c.Email == normalised && (excludeContactId == null || c.ContactId != excludeContactId),
            cancellationToken);
    }

    public async Task<Contact?> CreateAsync(string name, string surname, string email, CancellationToken cancellationToken = default)
    {
        if (await EmailExistsAsync(email, null, cancellationToken))
        {
            return null;
        }

        var contact = new Contact
        {
            Name = name.Trim(),
            Surname = surname.Trim(),
            Email = email.Trim(),
        };
        _db.Contacts.Add(contact);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return contact;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            return null;
        }
    }

    public async Task<SaveResult> UpdateAsync(int contactId, string name, string surname, string email, CancellationToken cancellationToken = default)
    {
        var contact = await _db.Contacts.FirstOrDefaultAsync(c => c.ContactId == contactId, cancellationToken);
        if (contact is null)
        {
            return SaveResult.NotFound;
        }

        if (await EmailExistsAsync(email, contactId, cancellationToken))
        {
            return SaveResult.DuplicateEmail;
        }

        contact.Name = name.Trim();
        contact.Surname = surname.Trim();
        contact.Email = email.Trim();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return SaveResult.Saved;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            return SaveResult.DuplicateEmail;
        }
    }

    public Task<int> DeleteAsync(IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default)
    {
        var ids = contactIds.Distinct().Take(ClientService.MaxBatchSize).ToList();

        return _db.Contacts
            .Where(c => ids.Contains(c.ContactId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedClientViewModel>> GetLinkedClientsAsync(int contactId, CancellationToken cancellationToken = default) =>
        await _db.ClientContacts
            .AsNoTracking()
            .Where(cc => cc.ContactId == contactId)
            .Select(cc => cc.Client)
            .OrderBy(c => c.Name).ThenBy(c => c.ClientId)
            .Select(c => new LinkedClientViewModel(c.ClientId, c.Name, c.ClientCode))
            .ToListAsync(cancellationToken);

    public async Task<LinkOptionsViewModel> SearchLinkableClientsAsync(int contactId, string? search, CancellationToken cancellationToken = default)
    {
        var options = await ClientSearch.Apply(_db.Clients.AsNoTracking(), _db, search)
            .Where(c => !c.ClientContacts.Any(cc => cc.ContactId == contactId))
            .OrderBy(c => c.Name).ThenBy(c => c.ClientId)
            .Select(c => new LinkOptionViewModel(c.ClientId, c.Name, c.ClientCode))
            .Take(ClientService.PickerResultLimit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = options.Count > ClientService.PickerResultLimit;
        return new LinkOptionsViewModel(options.Take(ClientService.PickerResultLimit).ToList(), hasMore, search);
    }
}
