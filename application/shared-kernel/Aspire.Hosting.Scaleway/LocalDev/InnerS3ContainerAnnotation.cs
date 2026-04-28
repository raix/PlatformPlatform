namespace Aspire.Hosting.Scaleway;

/// <summary>
/// Annotation that stores a reference to the inner S3-compatible container
/// created by RunAsSeaweedFsContainer/RunAsMinioContainer, so WithReference can wire the S3 endpoint.
/// </summary>
internal sealed class InnerS3ContainerAnnotation(IResourceBuilder<ContainerResource> innerBuilder, string endpointName) : IResourceAnnotation
{
    public IResourceBuilder<ContainerResource> InnerBuilder { get; } = innerBuilder;

    public string EndpointName { get; } = endpointName;
}
