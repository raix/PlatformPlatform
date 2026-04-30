using System.Text.Json;
using Aspire.Hosting.Scaleway.MockServer;

var port = ParseInt("--port", args) ?? 0;
var seedPath = ParseString("--seed", args);

using var server = new ScalewayMockServer(port);
server.Start();

if (seedPath is not null)
{
    var json = await File.ReadAllTextAsync(seedPath);
    using var doc = JsonDocument.Parse(json);
    var seeded = 0;
    foreach (var bucket in doc.RootElement.EnumerateObject())
    {
        foreach (var resource in bucket.Value.EnumerateArray())
        {
            server.Seed(bucket.Name, resource);
            seeded++;
        }
    }

    Console.WriteLine($"Seeded {seeded} resource(s) from {seedPath}");
}

Console.WriteLine();
Console.WriteLine($"Mock Scaleway API listening at: {server.Url}");
Console.WriteLine();
Console.WriteLine("Point aspire deploy at it from another terminal:");
Console.WriteLine($"  export SCW_API_URL={server.Url}");
Console.WriteLine("  export SCW_ACCESS_KEY=test SCW_SECRET_KEY=test SCW_DEFAULT_PROJECT_ID=test");
Console.WriteLine("  export SCALEWAY_DEPLOY_INTERACTIVE=1");
Console.WriteLine("  aspire deploy --apphost application/AppHost/AppHost.csproj");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to stop.");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // expected on Ctrl+C
}

return;

static int? ParseInt(string flag, string[] arguments)
{
    var value = ParseString(flag, arguments);
    return value is not null && int.TryParse(value, out var parsed) ? parsed : null;
}

static string? ParseString(string flag, string[] arguments)
{
    for (var i = 0; i < arguments.Length - 1; i++)
    {
        if (arguments[i] == flag)
        {
            return arguments[i + 1];
        }
    }

    return null;
}
