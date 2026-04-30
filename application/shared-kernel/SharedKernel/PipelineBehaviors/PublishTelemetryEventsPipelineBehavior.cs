using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace SharedKernel.PipelineBehaviors;

internal static class TelemetryActivitySource
{
    public static readonly ActivitySource Instance = new("PlatformPlatform.TelemetryEvents");
}

public sealed class PublishTelemetryEventsPipelineBehavior<TRequest, TResponse>(
    ITelemetryEventsCollector telemetryEventsCollector,
    ConcurrentCommandCounter concurrentCommandCounter,
    ILogger<PublishTelemetryEventsPipelineBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse> where TRequest : ICommand where TResponse : ResultBase
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (concurrentCommandCounter.IsZero())
        {
            while (telemetryEventsCollector.HasEvents)
            {
                var telemetryEvent = telemetryEventsCollector.Dequeue();
                var eventName = telemetryEvent.GetType().Name;

                using var activity = TelemetryActivitySource.Instance.StartActivity(eventName);
                if (activity is not null)
                {
                    foreach (var property in telemetryEvent.Properties)
                    {
                        activity.SetTag(property.Key, property.Value);
                    }
                }

                logger.LogInformation("Telemetry: {EventName} {EventProperties}", eventName, string.Join(", ", telemetryEvent.Properties.Select(p => $"{p.Key}={p.Value}")));
            }
        }

        return response;
    }
}
