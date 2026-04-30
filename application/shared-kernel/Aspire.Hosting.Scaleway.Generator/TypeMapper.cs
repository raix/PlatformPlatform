namespace Aspire.Hosting.Scaleway.Generator;

/// <summary>
///     Maps TypeScript types from Scaleway SDK to C# types.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    ///     Maps a TypeScript type to its C# equivalent.
    ///     Returns null if the type should be skipped (complex interface type).
    /// </summary>
    public static string? MapType(string typeScriptType, bool isOptional, HashSet<string> knownEnums)
    {
        var mapped = typeScriptType switch
        {
            "string" => isOptional ? "string?" : "string",
            "number" => "long",
            "boolean" => "bool",
            "string[]" => "string[]?",
            "ScwRegion" => "ScalewayRegion",
            "ScwZone" => "ScalewayZone",
            _ => ResolveCustomType(typeScriptType, isOptional, knownEnums)
        };

        return mapped;
    }

    /// <summary>
    ///     Returns the default value expression for a C# type, or null if none is needed.
    /// </summary>
    public static string? GetDefaultValue(string csharpType)
    {
        return csharpType switch
        {
            "string" => "string.Empty",
            "ScalewayRegion" => "ScalewayRegion.FrPar",
            "ScalewayZone" => "ScalewayZone.FrPar1",
            _ => null
        };
    }

    private static string? ResolveCustomType(string typeScriptType, bool isOptional, HashSet<string> knownEnums)
    {
        // If it ends with [] it's an array of a custom type - skip for now
        if (typeScriptType.EndsWith("[]"))
        {
            return null;
        }

        // If it's a known enum type, use it
        if (knownEnums.Contains(typeScriptType))
        {
            return isOptional ? $"Scaleway{{ServicePrefix}}{typeScriptType}?" : $"Scaleway{{ServicePrefix}}{typeScriptType}";
        }

        // Unknown complex type - skip
        return null;
    }

    /// <summary>
    ///     Resolves a custom type with the actual service prefix applied.
    /// </summary>
    public static string ResolveServicePrefix(string type, string servicePrefix)
    {
        return type.Replace("{ServicePrefix}", servicePrefix);
    }
}
