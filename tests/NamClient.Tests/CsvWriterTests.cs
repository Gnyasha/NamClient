using NamClient.Web.Infrastructure;

namespace NamClient.Tests;

public class CsvWriterTests
{
    [Theory]
    [InlineData("Protea", "Protea")]
    [InlineData("Smith, John", "\"Smith, John\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+27 11", "'+27 11")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Escape_quotes_and_neutralises_formulas(string? value, string expected)
    {
        Assert.Equal(expected, CsvWriter.Escape(value));
    }
}
