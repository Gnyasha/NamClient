using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.Models;

namespace NamClient.Web.Services;

public class ClientContactLinkService : IClientContactLinkService
{
    private const int MaxAttempts = 2;

    private readonly AppDbContext _db;

    public ClientContactLinkService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LinkOutcome> LinkContactsToClientAsync(int clientId, IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default)
    {
        if (!await _db.Clients.AnyAsync(c => c.ClientId == clientId, cancellationToken))
        {
            return LinkOutcome.OwnerNotFound;
        }

        var requested = Batch(contactIds);

        return await LinkAsync(
            requested,
            existing: () => _db.Contacts.Where(c => requested.Contains(c.ContactId)).Select(c => c.ContactId),
            linked: () => _db.ClientContacts.Where(cc => cc.ClientId == clientId && requested.Contains(cc.ContactId)).Select(cc => cc.ContactId),
            createLink: contactId => new ClientContact { ClientId = clientId, ContactId = contactId },
            cancellationToken);
    }

    public async Task<LinkOutcome> LinkClientsToContactAsync(int contactId, IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default)
    {
        if (!await _db.Contacts.AnyAsync(c => c.ContactId == contactId, cancellationToken))
        {
            return LinkOutcome.OwnerNotFound;
        }

        var requested = Batch(clientIds);

        return await LinkAsync(
            requested,
            existing: () => _db.Clients.Where(c => requested.Contains(c.ClientId)).Select(c => c.ClientId),
            linked: () => _db.ClientContacts.Where(cc => cc.ContactId == contactId && requested.Contains(cc.ClientId)).Select(cc => cc.ClientId),
            createLink: clientId => new ClientContact { ClientId = clientId, ContactId = contactId },
            cancellationToken);
    }

    public Task<int> UnlinkContactsFromClientAsync(int clientId, IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default)
    {
        var ids = Batch(contactIds);
        return _db.ClientContacts
            .Where(cc => cc.ClientId == clientId && ids.Contains(cc.ContactId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> UnlinkClientsFromContactAsync(int contactId, IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default)
    {
        var ids = Batch(clientIds);
        return _db.ClientContacts
            .Where(cc => cc.ContactId == contactId && ids.Contains(cc.ClientId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static List<int> Batch(IReadOnlyCollection<int> ids) =>
        ids.Distinct().Take(ClientService.MaxBatchSize).ToList();

    private async Task<LinkOutcome> LinkAsync(
        List<int> requested,
        Func<IQueryable<int>> existing,
        Func<IQueryable<int>> linked,
        Func<int, ClientContact> createLink,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var existingIds = await existing().ToListAsync(cancellationToken);
            var alreadyLinked = await linked().ToListAsync(cancellationToken);
            var toLink = existingIds.Except(alreadyLinked).ToList();

            _db.ClientContacts.AddRange(toLink.Select(createLink));

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return new LinkOutcome(true, toLink.Count, alreadyLinked.Count, requested.Count - existingIds.Count);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation() && attempt < MaxAttempts)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }
}
