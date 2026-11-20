using NamClient.Web.Services.Search;

namespace NamClient.Tests;

public class SearchTextTests
{
    [Fact]
    public void Words_splits_on_punctuation_lowercases_and_removes_duplicates()
    {
        Assert.Equal(["first", "national", "bank", "pty"], SearchText.Words("First National Bank (Pty) bank"));
    }

    [Fact]
    public void Words_keeps_digits()
    {
        Assert.Equal(["3m", "south", "africa"], SearchText.Words("3M South-Africa"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("&&&")]
    public void Words_of_blank_or_symbol_only_text_is_empty(string? text)
    {
        Assert.Empty(SearchText.Words(text));
    }

    [Fact]
    public void Terms_keep_email_punctuation()
    {
        Assert.Equal(["thandi@example.com", "nkosi"], SearchText.Terms("  Thandi@Example.com   NKOSI "));
    }

    [Fact]
    public void Terms_are_limited_to_five()
    {
        Assert.Equal(SearchText.MaxTerms, SearchText.Terms("a b c d e f g").Count);
    }
}
