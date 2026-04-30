using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRegionTests
{
    [Theory]
    [InlineData(ScalewayRegion.FrPar, "fr-par")]
    [InlineData(ScalewayRegion.NlAms, "nl-ams")]
    [InlineData(ScalewayRegion.PlWaw, "pl-waw")]
    public void ToApiString_ReturnsCorrectFormat(ScalewayRegion region, string expected)
    {
        region.ToApiString().Should().Be(expected);
    }

    [Theory]
    [InlineData("fr-par", ScalewayRegion.FrPar)]
    [InlineData("nl-ams", ScalewayRegion.NlAms)]
    [InlineData("pl-waw", ScalewayRegion.PlWaw)]
    public void ParseRegion_ReturnsCorrectEnum(string value, ScalewayRegion expected)
    {
        ScalewayRegionExtensions.ParseRegion(value).Should().Be(expected);
    }

    [Fact]
    public void ParseRegion_ThrowsForUnknownValue()
    {
        var act = () => ScalewayRegionExtensions.ParseRegion("us-east-1");
        act.Should().Throw<ArgumentException>();
    }
}
