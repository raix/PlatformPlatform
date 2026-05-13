namespace Aspire.Hosting.Scaleway;

/// <summary>
///     Marker interface for all Scaleway cloud resources.
/// </summary>
public interface IScalewayResource : IResource
{
    ScalewayCredentialConfig? CredentialConfig { get; set; }

    TaskCompletionSource? ProvisioningTaskCompletionSource { get; set; }
}
