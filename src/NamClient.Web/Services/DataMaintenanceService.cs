using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data;

namespace NamClient.Web.Services;

public class DataMaintenanceService : IDataMaintenanceService
{
    private readonly AppDbContext _db;

    public DataMaintenanceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        var clients = await _db.Clients.CountAsync(cancellationToken);
        var contacts = await _db.Contacts.CountAsync(cancellationToken);

        // Link tables are truncated; parent tables are deleted in batches to keep transactions small.
        const string sql = """
            TRUNCATE TABLE dbo.ClientContacts;
            TRUNCATE TABLE dbo.ClientNameWords;
            WHILE 1 = 1 BEGIN DELETE TOP (100000) FROM dbo.Clients;  IF @@ROWCOUNT = 0 BREAK; END;
            WHILE 1 = 1 BEGIN DELETE TOP (100000) FROM dbo.Contacts; IF @@ROWCOUNT = 0 BREAK; END;
            DBCC CHECKIDENT ('dbo.Clients', RESEED, 0) WITH NO_INFOMSGS;
            DBCC CHECKIDENT ('dbo.Contacts', RESEED, 0) WITH NO_INFOMSGS;
            """;

        var previousTimeout = _db.Database.GetCommandTimeout();
        _db.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        finally
        {
            _db.Database.SetCommandTimeout(previousTimeout);
        }

        return $"Deleted {clients:N0} clients, {contacts:N0} contacts and all their links.";
    }
}
