using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;
using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public class DashboardService : IDashboardService
{
    private const int RecentCount = 5;

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardViewModel> GetAsync(CancellationToken cancellationToken = default)
    {
        return new DashboardViewModel
        {
            TotalClients = await _db.Clients.CountAsync(cancellationToken),
            TotalContacts = await _db.Contacts.CountAsync(cancellationToken),
            TotalLinks = await _db.ClientContacts.CountAsync(cancellationToken),
            ClientsWithoutContacts = await _db.Clients.CountAsync(c => !c.ClientContacts.Any(), cancellationToken),
            ContactsWithoutClients = await _db.Contacts.CountAsync(c => !c.ClientContacts.Any(), cancellationToken),
            RecentClients = await _db.Clients.AsNoTracking()
                .OrderByDescending(c => c.ClientId)
                .Take(RecentCount)
                .Select(c => new RecentItemViewModel(c.ClientId, c.Name, c.ClientCode, c.CreatedAt))
                .ToListAsync(cancellationToken),
            RecentContacts = await _db.Contacts.AsNoTracking()
                .OrderByDescending(c => c.ContactId)
                .Take(RecentCount)
                .Select(c => new RecentItemViewModel(c.ContactId, c.Surname + " " + c.Name, c.Email, c.CreatedAt))
                .ToListAsync(cancellationToken),
        };
    }
}
