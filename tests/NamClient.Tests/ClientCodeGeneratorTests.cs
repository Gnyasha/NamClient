using NamClient.Web.Services;

namespace NamClient.Tests;

public class ClientCodeGeneratorTests
{
    [Theory]
    [InlineData("First National Bank", "FNB")]
    [InlineData("Protea", "PRO")]
    [InlineData("IT", "ITA")]
    [InlineData("X", "XAB")]
    [InlineData("Absa Bank", "ABS")]
    [InlineData("apple", "APP")]
    [InlineData("  Bank   of  China  Ltd ", "BOC")]
    [InlineData("3M", "MAB")]
    [InlineData("123", "ABC")]
    [InlineData("", "ABC")]
    public void BuildPrefix_follows_the_spec_rules(string name, string expected)
    {
        Assert.Equal(expected, ClientCodeGenerator.BuildPrefix(name));
    }

    [Fact]
    public void NextAvailableCode_starts_at_001()
    {
        Assert.Equal("FNB001", ClientCodeGenerator.NextAvailableCode("FNB", []));
    }

    [Fact]
    public void NextAvailableCode_increments_past_used_numbers()
    {
        Assert.Equal("PRO003", ClientCodeGenerator.NextAvailableCode("PRO", ["PRO001", "PRO002"]));
    }

    [Fact]
    public void NextAvailableCode_fills_the_lowest_gap()
    {
        Assert.Equal("PRO002", ClientCodeGenerator.NextAvailableCode("PRO", ["PRO001", "PRO003"]));
    }

    [Fact]
    public void NextAvailableCode_throws_when_all_numbers_are_used()
    {
        var all = Enumerable.Range(1, 999).Select(n => $"ITA{n:D3}");
        Assert.Throws<InvalidOperationException>(() => ClientCodeGenerator.NextAvailableCode("ITA", all));
    }

    [Fact]
    public void Generated_code_is_six_upper_case_alphanumerics()
    {
        var code = ClientCodeGenerator.NextAvailableCode(ClientCodeGenerator.BuildPrefix("protea"), []);
        Assert.Matches("^[A-Z]{3}[0-9]{3}$", code);
    }
}
