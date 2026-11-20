using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace NamClient.Web.Data.Paging;

public static class KeysetPagingExtensions
{
    public const int SearchCountCap = 10_000;

    public static async Task<PagedResult<TItem>> ToKeysetPageAsync<TEntity, TItem>(
        this IQueryable<TEntity> source,
        KeysetOrder<TEntity, TItem> order,
        Expression<Func<TEntity, TItem>> projection,
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageSize = request.PageSize;
        var backwards = false;
        var query = source;

        if (order.TryParseCursor(request.After, out var afterValues))
        {
            query = order.ApplySeek(query, afterValues);
        }
        else if (order.TryParseCursor(request.Before, out var beforeValues))
        {
            backwards = true;
            query = order.ApplySeek(query, beforeValues, reverse: true);
        }

        var rows = await order.ApplyOrder(query, reverse: backwards)
            .Select(projection)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(pageSize);
        }

        if (backwards)
        {
            rows.Reverse();
        }

        if (rows.Count == 0)
        {
            return new PagedResult<TItem>(rows, null, null);
        }

        var cameFromCursor = afterValues.Count > 0 || backwards;
        var hasPrevious = backwards ? hasMore : cameFromCursor;
        var hasNext = backwards || hasMore;

        return new PagedResult<TItem>(
            rows,
            hasPrevious ? order.CreateCursor(rows[0]) : null,
            hasNext ? order.CreateCursor(rows[^1]) : null);
    }

    // Searches stop counting at SearchCountCap and show it as "10,000+".
    public static async Task<(int Count, bool IsLowerBound)> CountForListAsync<T>(
        this IQueryable<T> query,
        bool isSearch,
        CancellationToken cancellationToken = default)
    {
        if (!isSearch)
        {
            return (await query.CountAsync(cancellationToken), false);
        }

        var count = await query.Take(SearchCountCap + 1).CountAsync(cancellationToken);
        return count > SearchCountCap ? (SearchCountCap, true) : (count, false);
    }
}
