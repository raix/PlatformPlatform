namespace Aspire.Hosting.Scaleway;

/// <summary>
///     Scaleway availability zones for zonal resources.
/// </summary>
public enum ScalewayZone
{
    FrPar1,
    FrPar2,
    FrPar3,
    NlAms1,
    NlAms2,
    NlAms3,
    PlWaw1,
    PlWaw2,
    PlWaw3
}

public static class ScalewayZoneExtensions
{
    public static string ToApiString(this ScalewayZone zone)
    {
        return zone switch
        {
            ScalewayZone.FrPar1 => "fr-par-1",
            ScalewayZone.FrPar2 => "fr-par-2",
            ScalewayZone.FrPar3 => "fr-par-3",
            ScalewayZone.NlAms1 => "nl-ams-1",
            ScalewayZone.NlAms2 => "nl-ams-2",
            ScalewayZone.NlAms3 => "nl-ams-3",
            ScalewayZone.PlWaw1 => "pl-waw-1",
            ScalewayZone.PlWaw2 => "pl-waw-2",
            ScalewayZone.PlWaw3 => "pl-waw-3",
            _ => throw new UnreachableException($"Unknown Scaleway zone: {zone}.")
        };
    }

    public static ScalewayZone ParseZone(string value)
    {
        return value switch
        {
            "fr-par-1" => ScalewayZone.FrPar1,
            "fr-par-2" => ScalewayZone.FrPar2,
            "fr-par-3" => ScalewayZone.FrPar3,
            "nl-ams-1" => ScalewayZone.NlAms1,
            "nl-ams-2" => ScalewayZone.NlAms2,
            "nl-ams-3" => ScalewayZone.NlAms3,
            "pl-waw-1" => ScalewayZone.PlWaw1,
            "pl-waw-2" => ScalewayZone.PlWaw2,
            "pl-waw-3" => ScalewayZone.PlWaw3,
            _ => throw new ArgumentException($"Unknown Scaleway zone: '{value}'.")
        };
    }

    public static ScalewayRegion ToRegion(this ScalewayZone zone)
    {
        return zone switch
        {
            ScalewayZone.FrPar1 or ScalewayZone.FrPar2 or ScalewayZone.FrPar3 => ScalewayRegion.FrPar,
            ScalewayZone.NlAms1 or ScalewayZone.NlAms2 or ScalewayZone.NlAms3 => ScalewayRegion.NlAms,
            ScalewayZone.PlWaw1 or ScalewayZone.PlWaw2 or ScalewayZone.PlWaw3 => ScalewayRegion.PlWaw,
            _ => throw new UnreachableException($"Unknown Scaleway zone: {zone}.")
        };
    }
}
