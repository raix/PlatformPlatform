using Aspire.Hosting.Scaleway.Generator;

var outputDirectory = GetOutputDirectory(args);
Console.WriteLine($"Output directory: {outputDirectory}");

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Aspire.Hosting.Scaleway.Generator/1.0");

var services = ServiceCatalog.AllServices;
var processedCount = 0;
var skippedCount = 0;

foreach (var service in services)
{
    if (ServiceCatalog.IsSkipped(service))
    {
        continue;
    }

    Console.Write($"Processing {service}...");

    try
    {
        var typesContent = await FetchTypesFile(httpClient, service);
        if (typesContent is null)
        {
            Console.WriteLine(" no types.gen.ts found, skipping.");
            skippedCount++;
            continue;
        }

        var parsed = TypeScriptParser.Parse(service, typesContent);

        if (parsed.CreateRequests.Count == 0)
        {
            Console.WriteLine(" no Create*Request types found, skipping.");
            skippedCount++;
            continue;
        }

        CSharpEmitter.Emit(parsed, outputDirectory);
        Console.WriteLine($" {parsed.CreateRequests.Count} resource(s), {parsed.Enums.Count} enum(s).");
        processedCount++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($" error: {ex.Message}");
        skippedCount++;
    }
}

Console.WriteLine($"Done. Processed {processedCount} services, skipped {skippedCount}.");
return;

static string GetOutputDirectory(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--output")
        {
            return Path.GetFullPath(args[i + 1]);
        }
    }

    // Default: sibling project's Generated folder
    var generatorDir = AppContext.BaseDirectory;
    // Navigate up to find the project root (handles bin/Debug/net10.0 structure)
    var projectDir = FindProjectDirectory(generatorDir)
                     ?? throw new InvalidOperationException("Could not find project directory.");
    return Path.GetFullPath(Path.Combine(projectDir, "..", "Aspire.Hosting.Scaleway", "Generated"));
}

static string? FindProjectDirectory(string startDir)
{
    var dir = startDir;
    while (dir is not null)
    {
        if (Directory.GetFiles(dir, "*.csproj").Length > 0)
        {
            return dir;
        }

        dir = Path.GetDirectoryName(dir);
    }

    return null;
}

static async Task<string?> FetchTypesFile(HttpClient httpClient, string serviceName)
{
    // Try versions in order of preference: stable first, then beta/alpha
    string[] versionCandidates = ["v1", "v2", "v3", "v1beta1", "v1alpha1", "v1alpha2", "v2beta1", "v2alpha1"];

    foreach (var version in versionCandidates)
    {
        var url = $"https://raw.githubusercontent.com/scaleway/scaleway-sdk-js/main/packages_generated/{serviceName}/src/{version}/types.gen.ts";
        var response = await httpClient.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsStringAsync();
        }
    }

    return null;
}
