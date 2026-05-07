namespace Aspire.Hosting.Scaleway.LocalDev;

public static class ScalewayStorageLocalExtensions
{
    /// <summary>
    ///     Configures Scaleway Object Storage to run as a local SeaweedFS container during development.
    ///     SeaweedFS provides an S3-compatible API for local blob storage.
    ///     In publish mode, the resource remains a Scaleway cloud resource.
    /// </summary>
    public static IResourceBuilder<ScalewayObjectStorageResource> RunAsSeaweedFsContainer(
        this IResourceBuilder<ScalewayObjectStorageResource> builder,
        int? s3Port = null,
        Action<IResourceBuilder<ContainerResource>>? configureContainer = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        var containerBuilder = builder.ApplicationBuilder
            .AddContainer($"{builder.Resource.Name}-seaweedfs", "chrislusf/seaweedfs")
            .WithArgs("server", "-s3", "-dir=/data")
            .WithHttpEndpoint(s3Port, 8333, "s3")
            .WithVolume($"{builder.Resource.Name}-seaweedfs-data", "/data")
            .WithLifetime(ContainerLifetime.Persistent);

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new InnerS3ContainerAnnotation(containerBuilder, "s3"));

        return builder;
    }

    /// <summary>
    ///     Configures Scaleway Object Storage to run as a local MinIO container during development.
    ///     MinIO provides an S3-compatible API for local blob storage.
    ///     In publish mode, the resource remains a Scaleway cloud resource.
    /// </summary>
    public static IResourceBuilder<ScalewayObjectStorageResource> RunAsMinioContainer(
        this IResourceBuilder<ScalewayObjectStorageResource> builder,
        int? apiPort = null,
        int? consolePort = null,
        Action<IResourceBuilder<ContainerResource>>? configureContainer = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        var containerBuilder = builder.ApplicationBuilder
            .AddContainer($"{builder.Resource.Name}-minio", "minio/minio")
            .WithArgs("server", "/data", "--console-address", ":9001")
            .WithHttpEndpoint(apiPort, 9000, "s3")
            .WithHttpEndpoint(consolePort, 9001, "console")
            .WithVolume($"{builder.Resource.Name}-minio-data", "/data")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithUrlForEndpoint("console", u => u.DisplayText = "MinIO Console");

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new InnerS3ContainerAnnotation(containerBuilder, "s3"));

        return builder;
    }

    /// <summary>
    ///     Wires the local storage container's S3 endpoint into a project as the <c>BLOB_STORAGE_URL</c>
    ///     environment variable. All SCSs share the same endpoint; per-SCS isolation is via bucket name
    ///     (the <c>AddNamedBlobStorages</c> connection name).
    /// </summary>
    public static IResourceBuilder<T> WithS3Storage<T>(
        this IResourceBuilder<T> projectBuilder,
        IResourceBuilder<ScalewayObjectStorageResource> storageBuilder) where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        var s3Annotation = storageBuilder.Resource.Annotations.OfType<InnerS3ContainerAnnotation>().FirstOrDefault();

        if (s3Annotation is not null)
        {
            projectBuilder.WithEnvironment("BLOB_STORAGE_URL", s3Annotation.InnerBuilder.GetEndpoint(s3Annotation.EndpointName));
            projectBuilder.WaitFor(s3Annotation.InnerBuilder);
        }

        return projectBuilder;
    }
}
