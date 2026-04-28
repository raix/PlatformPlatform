namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Marker interface for Scaleway publish target annotations.
/// Resources annotated with this will be provisioned via the Scaleway API during publish/deploy.
/// </summary>
public interface IScalewayPublishTargetAnnotation : IResourceAnnotation
{
}
