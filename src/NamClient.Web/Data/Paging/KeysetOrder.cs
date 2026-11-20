using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace NamClient.Web.Data.Paging;

/// <summary>Keyset (seek) paging: each page starts after the previous page's last key, so deep pages are as fast as the first - in vice versaz.</summary>
public sealed class KeysetOrder<TEntity, TItem>
{
    private static readonly MethodInfo StringCompare =
        typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;

    private readonly List<IKeyPart> _parts = [];

    public KeysetOrder(bool descending = false)
    {
        Descending = descending;
    }

    public bool Descending { get; }

    public KeysetOrder<TEntity, TItem> By(Expression<Func<TEntity, string>> selector, Func<TItem, string> reader)
    {
        _parts.Add(new KeyPart<string>(selector, reader));
        return this;
    }

    public KeysetOrder<TEntity, TItem> By(Expression<Func<TEntity, int>> selector, Func<TItem, int> reader)
    {
        _parts.Add(new KeyPart<int>(selector, reader));
        return this;
    }

    public IOrderedQueryable<TEntity> ApplyOrder(IQueryable<TEntity> query, bool reverse = false)
    {
        EnsureParts();
        var descending = Descending != reverse;

        var ordered = _parts[0].OrderBy(query, descending);
        foreach (var part in _parts.Skip(1))
        {
            ordered = part.ThenBy(ordered, descending);
        }

        return ordered;
    }

    public IQueryable<TEntity> ApplySeek(IQueryable<TEntity> query, IReadOnlyList<string> cursorValues, bool reverse = false)
    {
        EnsureParts();
        var descending = Descending != reverse;
        var entity = Expression.Parameter(typeof(TEntity), "e");

        Expression? predicate = null;
        for (var i = 0; i < _parts.Count; i++)
        {
            Expression clause = _parts[i].Compare(entity, cursorValues[i], after: !descending);
            for (var j = i - 1; j >= 0; j--)
            {
                clause = Expression.AndAlso(_parts[j].Equal(entity, cursorValues[j]), clause);
            }

            predicate = predicate is null ? clause : Expression.OrElse(predicate, clause);
        }

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(predicate!, entity));
    }

    public string CreateCursor(TItem item)
    {
        var values = _parts.Select(p => p.Read(item)).ToArray();
        return WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(values));
    }

    public bool TryParseCursor(string? cursor, out IReadOnlyList<string> values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<string[]>(WebEncoders.Base64UrlDecode(cursor));
            if (decoded is null || decoded.Length != _parts.Count || !_parts.Select((p, i) => p.IsValid(decoded[i])).All(ok => ok))
            {
                return false;
            }

            values = decoded;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    private void EnsureParts()
    {
        if (_parts.Count == 0)
        {
            throw new InvalidOperationException("A keyset order needs at least one key.");
        }
    }

    private interface IKeyPart
    {
        IOrderedQueryable<TEntity> OrderBy(IQueryable<TEntity> query, bool descending);

        IOrderedQueryable<TEntity> ThenBy(IOrderedQueryable<TEntity> query, bool descending);

        Expression Equal(ParameterExpression entity, string value);

        Expression Compare(ParameterExpression entity, string value, bool after);

        string Read(TItem item);

        bool IsValid(string? value);
    }

    private sealed class KeyPart<TKey> : IKeyPart
    {
        private readonly Expression<Func<TEntity, TKey>> _selector;
        private readonly Func<TItem, TKey> _reader;

        public KeyPart(Expression<Func<TEntity, TKey>> selector, Func<TItem, TKey> reader)
        {
            _selector = selector;
            _reader = reader;
        }

        public IOrderedQueryable<TEntity> OrderBy(IQueryable<TEntity> query, bool descending) =>
            descending ? query.OrderByDescending(_selector) : query.OrderBy(_selector);

        public IOrderedQueryable<TEntity> ThenBy(IOrderedQueryable<TEntity> query, bool descending) =>
            descending ? query.ThenByDescending(_selector) : query.ThenBy(_selector);

        public Expression Equal(ParameterExpression entity, string value) =>
            Expression.Equal(Body(entity), Value(value));

        public Expression Compare(ParameterExpression entity, string value, bool after)
        {
            if (typeof(TKey) == typeof(string))
            {
                var compare = Expression.Call(StringCompare, Body(entity), Value(value));
                var zero = Expression.Constant(0);
                return after ? Expression.GreaterThan(compare, zero) : Expression.LessThan(compare, zero);
            }

            return after
                ? Expression.GreaterThan(Body(entity), Value(value))
                : Expression.LessThan(Body(entity), Value(value));
        }

        public string Read(TItem item) =>
            Convert.ToString(_reader(item), CultureInfo.InvariantCulture) ?? string.Empty;

        public bool IsValid(string? value) =>
            value is not null && (typeof(TKey) != typeof(int) || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

        private Expression Body(ParameterExpression entity) =>
            new ParameterReplacer(_selector.Parameters[0], entity).Visit(_selector.Body);

        // Wrapped in an object so EF sends a SQL parameter and reuses the query plan.
        private static Expression Value(string raw)
        {
            object parsed = typeof(TKey) == typeof(int)
                ? int.Parse(raw, CultureInfo.InvariantCulture)
                : raw;

            var holder = new ValueHolder((TKey)parsed);
            return Expression.Property(Expression.Constant(holder), nameof(ValueHolder.Value));
        }

        private sealed class ValueHolder(TKey value)
        {
            public TKey Value { get; } = value;
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
