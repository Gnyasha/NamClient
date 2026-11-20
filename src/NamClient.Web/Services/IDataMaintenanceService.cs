namespace NamClient.Web.Services;

public interface IDataMaintenanceService
{
    Task<string> DeleteAllAsync(CancellationToken cancellationToken = default);
}
