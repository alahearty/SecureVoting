using SecureVoting.Application.Exporting;
using Xunit;

namespace SecureVoting.UnitTests;

public sealed class CsvEscaperTests
{
    [Theory]
    [InlineData("=SUM(A1:A2)")]
    [InlineData("+2348030000000")]
    [InlineData("-1+1")]
    [InlineData("@cmd")]
    [InlineData("   =cmd")]
    [InlineData("\t=cmd")]
    public void FormulaLeadingValuesAreNeutralized(string value)
    {
        var escaped = CsvEscaper.Escape(value);
        Assert.StartsWith("\"'", escaped);
    }

    [Fact]
    public void QuotesAreDoubledAndOrdinaryValuesRemainReadable()
    {
        Assert.Equal("\"Ada, \"\"A\"\" Okafor\"", CsvEscaper.Escape("Ada, \"A\" Okafor"));
        Assert.Equal("\"Candidate\"", CsvEscaper.Escape("Candidate"));
        Assert.Equal("\"\"", CsvEscaper.Escape(null));
    }
}
