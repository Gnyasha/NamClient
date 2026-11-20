namespace NamClient.Web.Data.Paging;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? PreviousCursor,
    string? NextCursor,
    int TotalCount = 0,
    bool TotalIsLowerBound = false)
{
    public bool HasPrevious => PreviousCursor is not null;

    public bool HasNext => NextCursor is not null;
}
