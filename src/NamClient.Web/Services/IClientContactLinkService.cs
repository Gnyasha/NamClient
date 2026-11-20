namespace NamClient.Web.Services;

public interface IClientContactLinkService
{
    Task<LinkOutcome> LinkContactsToClientAsync(int clientId, IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default);

    Task<LinkOutcome> LinkClientsToContactAsync(int contactId, IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default);

    Task<int> UnlinkContactsFromClientAsync(int clientId, IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default);

    Task<int> UnlinkClientsFromContactAsync(int contactId, IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default);
}

public sealed record LinkOutcome(bool OwnerFound, int Linked = 0, int AlreadyLinked = 0, int Missing = 0)
{
    public static LinkOutcome OwnerNotFound { get; } = new(false);
}
