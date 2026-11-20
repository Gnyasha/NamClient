namespace NamClient.Web.Data.Paging;

public sealed class PageRequest
{
    public const int DefaultPageSize = 25;

    public static readonly IReadOnlyList<int> PageSizes = [25, 50, 100];

    public string? Q { get; set; }

    public string? Sort { get; set; }

    public string? After { get; set; }

    public string? Before { get; set; }

    public int Size { get; set; } = DefaultPageSize;

    public int PageSize => PageSizes.Contains(Size) ? Size : DefaultPageSize;

    public string? Search => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();

    public bool HasSearch => Search is not null;
}
