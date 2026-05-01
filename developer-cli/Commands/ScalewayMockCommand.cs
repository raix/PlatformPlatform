using System.CommandLine;
using System.Diagnostics;
using DeveloperCli.Installation;
using DeveloperCli.Utilities;
using Spectre.Console;

namespace DeveloperCli.Commands;

/// <summary>
///     Command that runs the in-memory Scaleway mock server for local interactive QA.
///     Spawns <c>dotnet run</c> on the mock server project; the user points
///     <c>aspire deploy</c> at the printed URL from another terminal.
/// </summary>
public sealed class ScalewayMockCommand : Command
{
    public ScalewayMockCommand() : base("scaleway-mock", "Run an in-memory mock Scaleway API server for local interactive QA of aspire deploy")
    {
        var portOption = new Option<int>("--port") { Description = "Port to bind to (0 = OS-assigned, default)" };
        var seedOption = new Option<string?>("--seed") { Description = "Path to a JSON seed file (e.g. {\"instances\": [...]})" };

        Options.Add(portOption);
        Options.Add(seedOption);

        SetAction(parseResult => Execute(
                parseResult.GetValue(portOption),
                parseResult.GetValue(seedOption)
            )
        );
    }

    private static void Execute(int port, string? seedPath)
    {
        Prerequisite.Ensure(Prerequisite.Dotnet);

        var projectPath = Path.Combine(
            Configuration.ApplicationFolder,
            "shared-kernel",
            "Aspire.Hosting.Scaleway.MockServer",
            "Aspire.Hosting.Scaleway.MockServer.csproj"
        );

        if (!File.Exists(projectPath))
        {
            AnsiConsole.MarkupLine($"[red]Mock server project not found at {projectPath}[/]");
            Environment.Exit(1);
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Configuration.ApplicationFolder,
            ArgumentList = { "run", "--project", projectPath, "--" }
        };

        if (port > 0)
        {
            processStartInfo.ArgumentList.Add("--port");
            processStartInfo.ArgumentList.Add(port.ToString());
        }

        if (seedPath is not null)
        {
            var resolvedSeed = Path.GetFullPath(seedPath);
            if (!File.Exists(resolvedSeed))
            {
                AnsiConsole.MarkupLine($"[red]Seed file not found: {resolvedSeed}[/]");
                Environment.Exit(1);
            }

            processStartInfo.ArgumentList.Add("--seed");
            processStartInfo.ArgumentList.Add(resolvedSeed);
        }

        AnsiConsole.MarkupLine("[blue]Starting Scaleway mock server (Ctrl+C to stop)...[/]");
        ProcessHelper.StartProcess(processStartInfo);
    }
}
