using NamClient.Web.Data.Paging;
using NamClient.Web.Models;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public interface IContactService
{
    string NormaliseSort(string? sort);

    Task<PagedResult<ContactListItemViewModel>> SearchAsync(PageRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ContactListItemViewModel> ExportAsync(string? search, string? sort, IReadOnlyCollection<int>? ids);

    Task<Contact?> FindAsync(int contactId, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, int? excludeContactId = null, CancellationToken cancellationToken = default);

    Task<Contact?> CreateAsync(string name, string surname, string email, CancellationToken cancellationToken = default);

    Task<SaveResult> UpdateAsync(int contactId, string name, string surname, string email, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(IReadOnlyCollection<int> contactIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LinkedClientViewModel>> GetLinkedClientsAsync(int contactId, CancellationToken cancellationToken = default);

    Task<LinkOptionsViewModel> SearchLinkableClientsAsync(int contactId, string? search, CancellationToken cancellationToken = default);
}

public enum SaveResult
{
    Saved,
    NotFound,
    DuplicateEmail,
}
