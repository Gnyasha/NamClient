using Microsoft.EntityFrameworkCore;
using NamClient.Web.Data.Paging;

namespace NamClient.Tests;

public class KeysetPagingTests
{
    private sealed class Row
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
    }

    private static TestDb CreateDb(int rowCount)
    {
        var options = new DbContextOptionsBuilder<TestDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new TestDb(options);

        // Duplicate names on purpose: the Id tie-breaker must keep the order total.
        db.Rows.AddRange(Enumerable.Range(1, rowCount).Select(i => new Row { Id = i, Name = $"Name {i % 7:D2}" }));
        db.SaveChanges();
        return db;
    }

    private static KeysetOrder<Row, Row> NameOrder(bool descending = false) =>
        new KeysetOrder<Row, Row>(descending).By(r => r.Name, r => r.Name).By(r => r.Id, r => r.Id);

    private static List<int> Expected(TestDb db, bool descending = false)
    {
        var ordered = db.Rows.AsEnumerable()
            .OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .ToList();

        if (descending)
        {
            ordered.Reverse();
        }

        return ordered;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Walking_forward_visits_every_row_once_in_order(bool descending)
    {
        using var db = CreateDb(53);
        var order = NameOrder(descending);
        var seen = new List<int>();
        var request = new PageRequest { Size = 25 };

        while (true)
        {
            var page = await db.Rows.ToKeysetPageAsync(order, r => r, request);
            seen.AddRange(page.Items.Select(r => r.Id));
            if (!page.HasNext)
            {
                break;
            }

            request = new PageRequest { Size = 25, After = page.NextCursor };
        }

        Assert.Equal(Expected(db, descending), seen);
    }

    [Fact]
    public async Task Previous_cursor_returns_the_page_before()
    {
        using var db = CreateDb(60);
        var order = NameOrder();

        var first = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25 });
        var second = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25, After = first.NextCursor });
        var back = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25, Before = second.PreviousCursor });

        Assert.False(first.HasPrevious);
        Assert.True(second.HasPrevious);
        Assert.Equal(first.Items.Select(r => r.Id), back.Items.Select(r => r.Id));
        Assert.False(back.HasPrevious);
        Assert.True(back.HasNext);
    }

    [Fact]
    public async Task Last_page_has_no_next()
    {
        using var db = CreateDb(30);
        var order = NameOrder();

        var first = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25 });
        var last = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25, After = first.NextCursor });

        Assert.Equal(5, last.Items.Count);
        Assert.False(last.HasNext);
        Assert.True(last.HasPrevious);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("WyJvbmx5LW9uZSJd")] // ["only-one"]: wrong number of parts
    [InlineData("WyJhIiwiYiJd")] // ["a","b"]: second part is not an int
    public async Task Bad_cursors_fall_back_to_the_first_page(string cursor)
    {
        using var db = CreateDb(30);
        var order = NameOrder();

        var page = await db.Rows.ToKeysetPageAsync(order, r => r, new PageRequest { Size = 25, After = cursor });

        Assert.Equal(Expected(db).Take(25), page.Items.Select(r => r.Id));
        Assert.False(page.HasPrevious);
    }

    [Fact]
    public void Page_size_must_be_one_of_the_allowed_sizes()
    {
        Assert.Equal(PageRequest.DefaultPageSize, new PageRequest { Size = 5000 }.PageSize);
        Assert.Equal(100, new PageRequest { Size = 100 }.PageSize);
    }
}
