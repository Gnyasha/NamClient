using NamClient.Web.ViewModels;

namespace NamClient.Web.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync(CancellationToken cancellationToken = default);
}
