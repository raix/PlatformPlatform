namespace Aspire.Hosting.Scaleway.Provisioning;

/// <summary>
///     Marker interface for Scaleway publish target annotations.
///     Resources annotated with this will be provisioned via the Scaleway API during publish/deploy.
/// </summary>
public interface IScalewayPublishTargetAnnotation : IResourceAnnotation;
