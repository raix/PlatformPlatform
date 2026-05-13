using Aspire.Hosting.Pipelines;

namespace Aspire.Hosting.Scaleway;

public static class ScalewayEnvironmentExtensions
{
    /// <summary>
    ///     Adds a Scaleway deployment environment to the distributed application.
    ///     This is the root resource that owns shared infrastructure: private network, container registry,
    ///     and container namespace. Mirrors the AWSCDKEnvironmentResource pattern.
    /// </summary>
    public static IResourceBuilder<ScalewayEnvironmentResource> AddScalewayEnvironment(
        this IDistributedApplicationBuilder builder,
        string name,
        ScalewayRegion? region = null,
        string? projectId = null)
    {
        var config = ScalewayCredentialConfig.FromEnvironment(defaultProjectId: projectId, defaultRegion: region);

        var isPublishMode = builder.ExecutionContext.IsPublishMode;
        var resource = new ScalewayEnvironmentResource(name, config, isPublishMode);
        var resourceBuilder = builder.AddResource(resource);

        resourceBuilder.WithPipelineStepFactory(
            ScalewayPipelineStep.StepNameFor(resource),
            context => ScalewayPipelineStep.ExecuteAsync(resource, context),
            requiredBy: [WellKnownPipelineSteps.Deploy]
        );

        return resourceBuilder;
    }

    /// <summary>
    ///     Sets a maximum monthly budget in EUR. Deployments that exceed this budget are blocked during dry-run.
    /// </summary>
    public static IResourceBuilder<ScalewayEnvironmentResource> WithMonthlyBudget(
        this IResourceBuilder<ScalewayEnvironmentResource> builder,
        decimal maxMonthlyEur)
    {
        builder.Resource.MonthlyBudget = maxMonthlyEur;
        return builder;
    }
}
