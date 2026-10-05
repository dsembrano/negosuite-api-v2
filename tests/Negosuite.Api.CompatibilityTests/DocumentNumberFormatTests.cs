using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class DocumentNumberFormatTests
{
    [Theory]
    [InlineData("######", "000001")]
    [InlineData("########", "00000001")]
    [InlineData("YYYY-######", "2026-000001")]
    [InlineData("YYYY-########", "2026-00000001")]
    [InlineData("YYYY-MM-######", "2026-10-000001")]
    [InlineData("YYYYMM-########", "202610-00000001")]
    [InlineData("YYMM-######", "2610-000001")]
    [InlineData("YYYYMMDD-########", "20261003-00000001")]
    [InlineData(null, "00000001")]
    [InlineData("", "00000001")]
    [InlineData("unknown", "00000001")]
    public void Formats_supported_patterns_and_preserves_legacy_fallback(string format, string expected)
        => Assert.Equal(expected, AutoReferenceNoConfig.getFormattedSequenceNo(1, format, new DateTime(2026, 10, 3)));

    [Theory]
    [InlineData("######", 1000000, "1000000")]
    [InlineData("YYYY-######", 1000000, "2026-1000000")]
    [InlineData("########", 100000000, "100000000")]
    [InlineData("YYMM-######", int.MaxValue, "2610-2147483647")]
    public void Padding_never_truncates_or_wraps_the_sequence(string format, int sequence, string expected)
        => Assert.Equal(expected, AutoReferenceNoConfig.getFormattedSequenceNo(sequence, format, new DateTime(2026, 10, 3)));

    [Fact]
    public void A_date_change_does_not_reset_the_supplied_sequence()
    {
        Assert.Equal("2026-12-000123", AutoReferenceNoConfig.getFormattedSequenceNo(123, "YYYY-MM-######", new DateTime(2026, 12, 31)));
        Assert.Equal("2027-01-000124", AutoReferenceNoConfig.getFormattedSequenceNo(124, "YYYY-MM-######", new DateTime(2027, 1, 1)));
    }

    [Fact]
    public void Date_and_padding_are_independent_of_process_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("th-TH");
            Assert.Equal("2026-10-000042", AutoReferenceNoConfig.getFormattedSequenceNo(42, "YYYY-MM-######", new DateTime(2026, 10, 3)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = original; }
    }
}
