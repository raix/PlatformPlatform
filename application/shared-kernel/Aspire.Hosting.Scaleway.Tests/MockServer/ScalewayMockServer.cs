using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Scaleway.Tests.MockServer;

/// <summary>
///     Lightweight mock HTTP server that simulates the Scaleway REST API.
///     Tracks all received requests and maintains an in-memory resource store.
/// </summary>
public sealed class ScalewayMockServer : IDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private readonly ConcurrentDictionary<string, List<JsonElement>> _resources = new();
    private int _idCounter;

    public ScalewayMockServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();

        // Use middleware to catch all requests regardless of path
        _app.Run(async context =>
            {
                var method = context.Request.Method;
                var path = context.Request.Path.Value ?? "/";
                var query = context.Request.QueryString.Value ?? "";
                var body = method is "POST" or "PATCH" or "PUT"
                    ? await new StreamReader(context.Request.Body).ReadToEndAsync()
                    : null;

                _requests.Enqueue(new RecordedRequest(method, path + query, body, DateTimeOffset.UtcNow));

                var resourceType = ExtractResourceType(path);
                var arrayName = GetArrayName(resourceType);

                switch (method)
                {
                    case "GET":
                    {
                        var nameFilter = context.Request.Query["name"].FirstOrDefault();
                        var items = _resources.GetValueOrDefault(resourceType, []);
                        if (nameFilter is not null)
                        {
                            items = items.Where(r => r.TryGetProperty("name", out var n) && n.GetString() == nameFilter).ToList();
                        }

                        var json = JsonSerializer.Serialize(new Dictionary<string, object>
                            {
                                [arrayName] = items,
                                ["total_count"] = items.Count
                            }
                        );
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(json);
                        break;
                    }
                    case "POST":
                    {
                        var id = $"{resourceType}-{Interlocked.Increment(ref _idCounter)}";
                        var properties = new Dictionary<string, object> { ["id"] = id, ["status"] = "provisioning" };

                        // Inject region/zone from the URL path (Scaleway API includes these in responses)
                        var regionOrZone = ExtractRegionOrZone(path);
                        if (regionOrZone is not null)
                        {
                            if (path.Contains("/zones/"))
                            {
                                properties["zone"] = regionOrZone;
                            }
                            else
                            {
                                properties["region"] = regionOrZone;
                            }
                        }

                        if (body is not null)
                        {
                            var doc = JsonDocument.Parse(body);
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                properties[prop.Name] = prop.Value;
                            }
                        }

                        var resourceJson = JsonDocument.Parse(JsonSerializer.Serialize(properties)).RootElement;
                        _resources.AddOrUpdate(resourceType, _ => [resourceJson], (_, list) =>
                            {
                                list.Add(resourceJson);
                                return list;
                            }
                        );

                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(properties));
                        break;
                    }
                    case "PATCH":
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("""{"id": "patched", "status": "ready"}""");
                        break;
                    }
                    case "DELETE":
                    {
                        context.Response.StatusCode = 204;
                        break;
                    }
                }
            }
        );
    }

    public string Url => _app.Urls.First();

    public IReadOnlyList<RecordedRequest> ReceivedRequests => [.. _requests];

    public IReadOnlyDictionary<string, List<JsonElement>> Resources => _resources;

    public void Dispose()
    {
        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Pre-populates the mock with a resource so subsequent GETs return it.
    ///     Use to simulate already-provisioned Scaleway state for E2E scenarios.
    /// </summary>
    public void Seed(string resourceType, object body)
    {
        var json = JsonDocument.Parse(JsonSerializer.Serialize(body)).RootElement;
        _resources.AddOrUpdate(resourceType, _ => [json], (_, list) =>
            {
                list.Add(json);
                return list;
            }
        );
    }

    public void Start()
    {
        _app.StartAsync().GetAwaiter().GetResult();
    }

    private static string ExtractResourceType(string path)
    {
        // Resource type is always the last path segment for the Scaleway list endpoints we handle
        // (e.g. /rdb/v1/regions/{region}/instances). For GET-by-id (/.../instances/{id}) callers
        // would need a different lookup, but our pipeline only uses list+create endpoints.
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : "unknown";
    }

    private static string? ExtractRegionOrZone(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] is "regions" or "zones")
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    private static string GetArrayName(string resourceType)
    {
        return resourceType switch
        {
            "private-networks" => "private_networks",
            _ => resourceType.Replace("-", "_")
        };
    }
}

public sealed record RecordedRequest(string Method, string Path, string? Body, DateTimeOffset Timestamp);
