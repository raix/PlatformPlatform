using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSwag.Annotations;

namespace SharedKernel.Endpoints;

public sealed class TrackEndpoints : IEndpoints
{
    private static readonly ActivitySource FrontendActivitySource = new("PlatformPlatform.Frontend");
    private static readonly Meter FrontendMeter = new("PlatformPlatform.Frontend");

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/track", Track).AllowAnonymous().DisableAntiforgery();
    }

    [OpenApiIgnore]
    private static TrackResponse Track(HttpContext context, List<TrackRequest> trackRequests, ILogger<string> logger)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        foreach (var trackRequest in trackRequests)
        {
            switch (trackRequest.Data.BaseType)
            {
                case "PageviewData":
                {
                    using var activity = FrontendActivitySource.StartActivity("PageView", ActivityKind.Internal);
                    if (activity is not null)
                    {
                        activity.SetTag("page.name", trackRequest.Data.BaseData.Name);
                        activity.SetTag("page.url", trackRequest.Data.BaseData.Url);
                        activity.SetTag("page.duration", trackRequest.Data.BaseData.Duration.ToString());
                        activity.SetTag("page.id", trackRequest.Data.BaseData.Id);
                        activity.SetTag("client.address", ip);
                        CopyTags(activity, trackRequest.Tags);
                        CopyProperties(activity, trackRequest.Data.BaseData.Properties);
                    }

                    break;
                }
                case "PageviewPerformanceData":
                {
                    using var activity = FrontendActivitySource.StartActivity("PageViewPerformance", ActivityKind.Internal);
                    if (activity is not null)
                    {
                        activity.SetTag("page.name", trackRequest.Data.BaseData.Name);
                        activity.SetTag("page.url", trackRequest.Data.BaseData.Url);
                        activity.SetTag("page.duration", trackRequest.Data.BaseData.Duration.ToString());
                        activity.SetTag("page.id", trackRequest.Data.BaseData.Id);
                        activity.SetTag("page.perf_total", trackRequest.Data.BaseData.PerfTotal.ToString());
                        activity.SetTag("page.network_connect", trackRequest.Data.BaseData.NetworkConnect.ToString());
                        activity.SetTag("page.sent_request", trackRequest.Data.BaseData.SentRequest.ToString());
                        activity.SetTag("page.received_response", trackRequest.Data.BaseData.ReceivedResponse.ToString());
                        activity.SetTag("page.dom_processing", trackRequest.Data.BaseData.DomProcessing.ToString());
                        activity.SetTag("client.address", ip);
                        CopyTags(activity, trackRequest.Tags);
                        CopyProperties(activity, trackRequest.Data.BaseData.Properties);
                    }

                    break;
                }
                case "ExceptionData":
                {
                    using var activity = FrontendActivitySource.StartActivity("FrontendException", ActivityKind.Internal);
                    if (activity is not null)
                    {
                        activity.SetStatus(ActivityStatusCode.Error);
                        activity.SetTag("exception.severity", trackRequest.Data.BaseData.SeverityLevel);
                        activity.SetTag("client.address", ip);
                        CopyTags(activity, trackRequest.Tags);
                        CopyProperties(activity, trackRequest.Data.BaseData.Properties);

                        foreach (var exception in trackRequest.Data.BaseData.Exceptions)
                        {
                            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
                                {
                                    { "exception.type", exception.TypeName },
                                    { "exception.message", exception.Message },
                                    { "exception.stacktrace", exception.Stack }
                                }
                            ));
                        }
                    }

                    break;
                }
                case "MetricData":
                {
                    foreach (var metric in trackRequest.Data.BaseData.Metrics)
                    {
                        var histogram = FrontendMeter.CreateHistogram<double>(metric.Name);
                        histogram.Record(metric.Value);
                    }

                    break;
                }
                case "RemoteDependencyData":
                {
                    // Ignore remote dependency data
                    break;
                }
                default:
                {
                    logger.LogWarning("Unsupported telemetry type: {BaseType}", trackRequest.Data.BaseType);
                    break;
                }
            }
        }

        return new TrackResponse(true, "Telemetry sent.");
    }

    private static void CopyTags(Activity activity, Dictionary<string, string> tags)
    {
        foreach (var tag in tags)
        {
            if (!string.IsNullOrEmpty(tag.Value))
            {
                activity.SetTag($"ai.{tag.Key.Replace("ai.", "")}", tag.Value);
            }
        }
    }

    private static void CopyProperties(Activity activity, IDictionary<string, string>? properties)
    {
        if (properties is null) return;

        foreach (var property in properties)
        {
            if (!string.IsNullOrEmpty(property.Value))
            {
                activity.SetTag($"custom.{property.Key}", property.Value);
            }
        }
    }
}

[PublicAPI]
public sealed record TrackResponse(bool Success, string Message);

[PublicAPI]
public sealed record TrackRequest(
    DateTimeOffset Time,
    // ReSharper disable once InconsistentNaming
    string IKey,
    string Name,
    Dictionary<string, string> Tags,
    TrackData Data
);

[PublicAPI]
public sealed record TrackData(string BaseType, TrackBaseData BaseData);

[PublicAPI]
public sealed record TrackBaseData(
    string Name,
    string Url,
    TimeSpan Duration,
    TimeSpan PerfTotal,
    TimeSpan NetworkConnect,
    TimeSpan SentRequest,
    TimeSpan ReceivedResponse,
    TimeSpan DomProcessing,
    Dictionary<string, string> Properties,
    Dictionary<string, double> Measurements,
    List<TrackMetric> Metrics,
    List<TrackException> Exceptions,
    string SeverityLevel,
    string Id
);

[PublicAPI]
public sealed record TrackMetric(string Name, int Kind, double Value, int Count);

[PublicAPI]
public sealed record TrackException(string TypeName, string Message, bool HasFullStack, string Stack, List<TrackExceptionParsedStack> ParsedStack);

[PublicAPI]
public sealed record TrackExceptionParsedStack(string Assembly, string FileName, string Method, int Line, int Level);
