using Aspire.Hosting.Scaleway;

namespace AppHost;

/// <summary>
///     Per-environment configuration that drives every value in <c>Program.cs</c> that varies
///     between <c>local</c>, <c>staging</c>, and <c>production</c>. Resolved once via
///     <see cref="Resolve" /> at AppHost startup; the three <see cref="Local" /> / <see cref="Staging" /> /
///     <see cref="Production" /> instances are the literal source of truth a reviewer can compare
///     at a glance.
/// </summary>
public sealed record EnvironmentProfile(
    string Name,
    bool IsLocal,
    ScalewayRegion Region,
    string? CustomDomain,
    decimal? MonthlyBudgetEur,
    RdbProfile Rdb,
    ContainerProfile ApiContainer,
    ContainerProfile WorkerContainer,
    bool AllowOAuthMock,
    bool AllowStripeMock
)
{
    /// <summary>
    ///     <c>pp run</c> on a developer laptop. Cloud values aren't reached here — local containers
    ///     handle Postgres, SeaweedFS, and Mailpit instead.
    /// </summary>
    public static readonly EnvironmentProfile Local = new(
        "local",
        true,
        ScalewayRegion.FrPar,
        null,
        null,
        new RdbProfile("PostgreSQL-16", "DB-DEV-S", 10, false),
        new ContainerProfile(0, 0, 0),
        new ContainerProfile(0, 0, 0),
        true,
        true
    );

    public static readonly EnvironmentProfile Staging = new(
        "staging",
        false,
        ScalewayRegion.FrPar,
        "staging.platformplatform.example",
        50m,
        new RdbProfile("PostgreSQL-16", "DB-DEV-S", 10, false),
        new ContainerProfile(0, 5, 512),
        new ContainerProfile(0, 5, 512),
        true,
        true
    );

    public static readonly EnvironmentProfile Production = new(
        "production",
        false,
        ScalewayRegion.FrPar,
        "platformplatform.example",
        500m,
        new RdbProfile("PostgreSQL-16", "DB-GP-S", 50, true),
        new ContainerProfile(1, 10, 1024),
        new ContainerProfile(1, 10, 1024),
        false,
        false
    );

    /// <summary>
    ///     Static-init guard that catches "Production has mocks enabled" or similar regressions
    ///     before the AppHost ever starts — runs on every <c>pp run</c> too, so a developer who
    ///     accidentally weakens Production gets a fail-fast error at any AppHost load.
    /// </summary>
    static EnvironmentProfile()
    {
        AssertProductionInvariants(Production);
    }

    private static void AssertProductionInvariants(EnvironmentProfile production)
    {
        if (production.AllowOAuthMock) throw new InvalidOperationException("Production must not allow the OAuth mock provider.");
        if (production.AllowStripeMock) throw new InvalidOperationException("Production must not allow the Stripe mock provider.");
        if (!production.Rdb.IsHaCluster) throw new InvalidOperationException("Production RDB must be configured as an HA cluster.");
        if (production.MonthlyBudgetEur is null or <= 0) throw new InvalidOperationException("Production must declare a positive MonthlyBudgetEur.");
        if (string.IsNullOrWhiteSpace(production.CustomDomain)) throw new InvalidOperationException("Production must declare a CustomDomain.");
        if (production.ApiContainer.MinScale < 1) throw new InvalidOperationException("Production ApiContainer.MinScale must be at least 1 (no scale-to-zero).");
    }

    /// <summary>
    ///     Reads <c>APPHOST_ENVIRONMENT</c> and returns the matching profile. Defaults to
    ///     <see cref="Local" /> when unset, preserving zero-config behaviour for <c>pp run</c>.
    ///     An unrecognised value fails fast before any provisioning starts.
    /// </summary>
    public static EnvironmentProfile Resolve()
    {
        return Environment.GetEnvironmentVariable("APPHOST_ENVIRONMENT")?.ToLowerInvariant() switch
        {
            null or "" or "local" => Local,
            "staging" => Staging,
            "production" => Production,
            var unknown => throw new DistributedApplicationException(
                $"Unknown APPHOST_ENVIRONMENT '{unknown}'. Expected: local, staging, production."
            )
        };
    }
}

public sealed record RdbProfile(string Engine, string NodeType, int VolumeSizeInGb, bool IsHaCluster);

public sealed record ContainerProfile(int MinScale, int MaxScale, int MemoryLimitMb);
