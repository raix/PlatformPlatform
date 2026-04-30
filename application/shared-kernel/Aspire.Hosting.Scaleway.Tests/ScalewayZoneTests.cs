using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayZoneTests
{
    [Theory]
    [InlineData(ScalewayZone.FrPar1, "fr-par-1")]
    [InlineData(ScalewayZone.NlAms2, "nl-ams-2")]
    [InlineData(ScalewayZone.PlWaw3, "pl-waw-3")]
    public void ToApiString_ReturnsCorrectFormat(ScalewayZone zone, string expected)
    {
        zone.ToApiString().Should().Be(expected);
    }

    [Theory]
    [InlineData("fr-par-1", ScalewayZone.FrPar1)]
    [InlineData("nl-ams-2", ScalewayZone.NlAms2)]
    [InlineData("pl-waw-3", ScalewayZone.PlWaw3)]
    public void ParseZone_ReturnsCorrectEnum(string value, ScalewayZone expected)
    {
        ScalewayZoneExtensions.ParseZone(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(ScalewayZone.FrPar1, ScalewayRegion.FrPar)]
    [InlineData(ScalewayZone.FrPar3, ScalewayRegion.FrPar)]
    [InlineData(ScalewayZone.NlAms1, ScalewayRegion.NlAms)]
    [InlineData(ScalewayZone.PlWaw2, ScalewayRegion.PlWaw)]
    public void ToRegion_ReturnsCorrectRegion(ScalewayZone zone, ScalewayRegion expected)
    {
        zone.ToRegion().Should().Be(expected);
    }

    [Fact]
    public void ParseZone_ThrowsForUnknownValue()
    {
        var act = () => ScalewayZoneExtensions.ParseZone("us-east-1a");
        act.Should().Throw<ArgumentException>();
    }
}
