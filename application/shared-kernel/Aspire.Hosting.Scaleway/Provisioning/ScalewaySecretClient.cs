using System.Text;
using System.Text.Json;

namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Get-or-create wrapper over Scaleway Secret Manager. Used by the deploy step to persist
///     credentials (RDB password, host, etc.) before the resource that needs them is created,
///     so a partial failure can never leave a credential orphaned with no recovery path.
/// </summary>
internal sealed class ScalewaySecretClient(ScalewayApiClient apiClient)
{
    /// <summary>
    ///     Returns the value of the secret with <paramref name="secretName" />, creating it
    ///     with <paramref name="valueFactory" />'s output if absent. Idempotent — repeat calls
    ///     return the original value.
    /// </summary>
    public async Task<string> GetOrCreateAsync(
        string projectId,
        string region,
        string secretName,
        Func<string> valueFactory,
        string[] tags,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByNameAsync(projectId, region, secretName, cancellationToken);
        if (existing is not null)
        {
            var existingValue = await AccessLatestAsync(region, existing.Value.GetProperty("id").GetString()!, cancellationToken);
            if (existingValue is not null)
            {
                return existingValue;
            }
        }

        var value = valueFactory();
        var secretId = existing?.GetProperty("id").GetString()
                       ?? await CreateSecretAsync(projectId, region, secretName, tags, cancellationToken);
        await CreateVersionAsync(region, secretId, value, cancellationToken);
        return value;
    }

    /// <summary>
    ///     Writes <paramref name="value" /> as the latest version of <paramref name="secretName" />,
    ///     creating the secret if absent. Used for values the deploy step computes after a resource
    ///     is created (e.g., the RDB endpoint hostname).
    /// </summary>
    public async Task SetAsync(
        string projectId,
        string region,
        string secretName,
        string value,
        string[] tags,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByNameAsync(projectId, region, secretName, cancellationToken);
        var secretId = existing?.GetProperty("id").GetString()
                       ?? await CreateSecretAsync(projectId, region, secretName, tags, cancellationToken);
        await CreateVersionAsync(region, secretId, value, cancellationToken);
    }

    private async Task<JsonElement?> FindByNameAsync(string projectId, string region, string name, CancellationToken cancellationToken)
    {
        var secrets = await apiClient.ListResourcesAsync(
            $"secret-manager/v1beta1/regions/{region}/secrets",
            region,
            new Dictionary<string, string> { ["project_id"] = projectId, ["name"] = name },
            cancellationToken
        );

        var match = secrets.FirstOrDefault(s => s.TryGetProperty("name", out var n) && n.GetString() == name);
        return match.ValueKind == JsonValueKind.Undefined ? null : match;
    }

    private async Task<string> CreateSecretAsync(string projectId, string region, string name, string[] tags, CancellationToken cancellationToken)
    {
        var result = await apiClient.CreateResourceAsync(
            $"secret-manager/v1beta1/regions/{region}/secrets",
            region,
            new { project_id = projectId, name, tags },
            cancellationToken
        );
        return result.GetProperty("id").GetString()!;
    }

    private async Task CreateVersionAsync(string region, string secretId, string value, CancellationToken cancellationToken)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        await apiClient.CreateResourceAsync(
            $"secret-manager/v1beta1/regions/{region}/secrets/{secretId}/versions",
            region,
            new { data = base64 },
            cancellationToken
        );
    }

    private async Task<string?> AccessLatestAsync(string region, string secretId, CancellationToken cancellationToken)
    {
        var result = await apiClient.GetAsync(
            $"secret-manager/v1beta1/regions/{region}/secrets/{secretId}/versions/latest_enabled/access",
            region,
            cancellationToken
        );

        if (result is null || !result.Value.TryGetProperty("data", out var data))
        {
            return null;
        }

        var base64 = data.GetString();
        return base64 is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
