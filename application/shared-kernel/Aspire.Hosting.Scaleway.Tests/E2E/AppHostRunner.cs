using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aspire.Hosting.Scaleway.Tests.E2E;

/// <summary>
///     Spawns <c>aspire deploy</c> as a subprocess against the platform AppHost,
///     with environment variables pointing at a per-test mock Scaleway server.
/// </summary>
internal static class AppHostRunner
{
    public static async Task<AppHostRunResult> RunDeployAsync(
        string mockServerUrl,
        IReadOnlyDictionary<string, string>? extraEnvironment = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var appHostProject = ResolveAppHostProjectPath();
        var processTimeout = timeout ?? TimeSpan.FromMinutes(5);

        var psi = new ProcessStartInfo
        {
            FileName = "aspire",
            ArgumentList =
            {
                "deploy",
                "--apphost", appHostProject,
                "--non-interactive",
                "--include-exception-details",
                "--no-build",
                "--nologo"
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["SCW_API_URL"] = mockServerUrl,
                ["SCW_ACCESS_KEY"] = "e2e-access-key",
                ["SCW_SECRET_KEY"] = "e2e-secret-key",
                ["SCW_DEFAULT_PROJECT_ID"] = "e2e-project",
                ["SCW_PRICING_CACHE_DISABLED"] = "1"
            }
        };

        if (extraEnvironment is not null)
        {
            foreach (var (key, value) in extraEnvironment)
            {
                psi.Environment[key] = value;
            }
        }

        using var process = new Process();
        process.StartInfo = psi;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        var started = Stopwatch.StartNew();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(processTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited between WaitForExit and Kill — nothing to do.
            }
            catch (Win32Exception)
            {
                // OS refused the kill (process gone, permission, etc.) — best-effort, surface the timeout instead.
            }

            throw new TimeoutException($"aspire deploy did not complete within {processTimeout}.\nStdout:\n{stdout}\nStderr:\n{stderr}");
        }

        return new AppHostRunResult(process.ExitCode, stdout.ToString(), stderr.ToString(), started.Elapsed);
    }

    private static string ResolveAppHostProjectPath()
    {
        // Walk up from the test assembly until we find application/AppHost/AppHost.csproj.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "application", "AppHost", "AppHost.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate application/AppHost/AppHost.csproj walking up from the test assembly directory.");
    }
}

internal sealed record AppHostRunResult(int ExitCode, string Stdout, string Stderr, TimeSpan Duration)
{
    public string CombinedOutput => $"--- stdout ---\n{Stdout}\n--- stderr ---\n{Stderr}";
}
