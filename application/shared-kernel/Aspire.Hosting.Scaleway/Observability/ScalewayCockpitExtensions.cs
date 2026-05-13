namespace Aspire.Hosting.Scaleway.Observability;

public static class ScalewayCockpitExtensions
{
    /// <summary>
    ///     Configures OpenTelemetry environment variables to push traces, metrics, and logs to Scaleway Cockpit.
    ///     Scaleway Cockpit uses separate OTLP endpoints per signal type with bearer token authentication.
    /// </summary>
    /// <param name="builder">The project resource builder.</param>
    /// <param name="region">Scaleway region (e.g., fr-par, nl-ams, pl-waw).</param>
    /// <param name="dataSourceId">The Cockpit data source ID from the Scaleway console.</param>
    /// <param name="tokenParameterName">Environment variable name containing the Cockpit push token.</param>
    public static IResourceBuilder<T> WithScalewayCockpit<T>(
        this IResourceBuilder<T> builder,
        ScalewayRegion region,
        string dataSourceId,
        string tokenParameterName = "COCKPIT_TOKEN") where T : IResourceWithEnvironment
    {
        var regionString = region.ToApiString();

        builder.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
        builder.WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", $"https://{dataSourceId}.traces.cockpit.{regionString}.scw.cloud/otlp/v1/traces");
        builder.WithEnvironment("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", $"https://{dataSourceId}.metrics.cockpit.{regionString}.scw.cloud/otlp/v1/metrics");
        builder.WithEnvironment("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT", $"https://{dataSourceId}.logs.cockpit.{regionString}.scw.cloud/otlp/v1/logs");
        builder.WithEnvironment(context =>
            {
                var token = Environment.GetEnvironmentVariable(tokenParameterName);
                if (token is not null)
                {
                    context.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] = $"Authorization=Bearer {token}";
                }
            }
        );

        return builder;
    }
}
