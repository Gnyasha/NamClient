namespace NamClient.Web.ViewModels;

public sealed record RecentItemViewModel(int Id, string Title, string Subtitle, DateTime CreatedAt);

public sealed class DashboardViewModel
{
    public int TotalClients { get; init; }

    public int TotalContacts { get; init; }

    public int TotalLinks { get; init; }

    public int ClientsWithoutContacts { get; init; }

    public int ContactsWithoutClients { get; init; }

    public IReadOnlyList<RecentItemViewModel> RecentClients { get; init; } = [];

    public IReadOnlyList<RecentItemViewModel> RecentContacts { get; init; } = [];

    public bool IsEmpty => TotalClients == 0 && TotalContacts == 0;
}
