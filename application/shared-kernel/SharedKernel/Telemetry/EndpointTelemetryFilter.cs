using System.Collections.Immutable;

namespace SharedKernel.Telemetry;

/// <summary>
///     Defines excluded paths and file extensions for telemetry filtering
/// </summary>
public static class EndpointTelemetryFilter
{
    public static readonly ImmutableHashSet<string> ExcludedPaths = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "/swagger", "/internal-api/live", "/internal-api/ready", "/api/track"
    );

    public static readonly ImmutableHashSet<string> ExcludedFileExtensions = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        ".js", ".css", ".png", ".jpg", ".ico", ".map", ".svg", ".woff", ".woff2", "webp"
    );
}
