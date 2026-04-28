namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Scaleway regions where resources can be deployed.
/// </summary>
public enum ScalewayRegion
{
    FrPar,
    NlAms,
    PlWaw
}

public static class ScalewayRegionExtensions
{
    public static string ToApiString(this ScalewayRegion region) => region switch
    {
        ScalewayRegion.FrPar => "fr-par",
        ScalewayRegion.NlAms => "nl-ams",
        ScalewayRegion.PlWaw => "pl-waw",
        _ => throw new UnreachableException($"Unknown Scaleway region: {region}.")
    };

    public static ScalewayRegion ParseRegion(string value) => value switch
    {
        "fr-par" => ScalewayRegion.FrPar,
        "nl-ams" => ScalewayRegion.NlAms,
        "pl-waw" => ScalewayRegion.PlWaw,
        _ => throw new ArgumentException($"Unknown Scaleway region: '{value}'.")
    };
}
