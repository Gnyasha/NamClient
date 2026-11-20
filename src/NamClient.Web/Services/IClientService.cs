using NamClient.Web.Data.Paging;
using NamClient.Web.Models;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public interface IClientService
{
    string NormaliseSort(string? sort);

    Task<PagedResult<ClientListItemViewModel>> SearchAsync(PageRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ClientListItemViewModel> ExportAsync(string? search, string? sort, IReadOnlyCollection<int>? ids);

    Task<Client?> FindAsync(int clientId, CancellationToken cancellationToken = default);

    Task<Client> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(int clientId, string name, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(IReadOnlyCollection<int> clientIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LinkedContactViewModel>> GetLinkedContactsAsync(int clientId, CancellationToken cancellationToken = default);

    Task<LinkOptionsViewModel> SearchLinkableContactsAsync(int clientId, string? search, CancellationToken cancellationToken = default);
}
