using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayStorageLocalExtensions
{
    /// <summary>
    /// Configures Scaleway Object Storage to run as a local SeaweedFS container during development.
    /// SeaweedFS provides an S3-compatible API for local blob storage.
    /// In publish mode, the resource remains a Scaleway cloud resource.
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
            .WithHttpEndpoint(port: s3Port, targetPort: 8333, name: "s3")
            .WithVolume($"{builder.Resource.Name}-seaweedfs-data", "/data")
            .WithLifetime(ContainerLifetime.Persistent);

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new InnerS3ContainerAnnotation(containerBuilder, "s3"));

        return builder;
    }

    /// <summary>
    /// Configures Scaleway Object Storage to run as a local MinIO container during development.
    /// MinIO provides an S3-compatible API for local blob storage.
    /// In publish mode, the resource remains a Scaleway cloud resource.
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
            .WithHttpEndpoint(port: apiPort, targetPort: 9000, name: "s3")
            .WithHttpEndpoint(port: consolePort, targetPort: 9001, name: "console")
            .WithVolume($"{builder.Resource.Name}-minio-data", "/data")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithUrlForEndpoint("console", u => u.DisplayText = "MinIO Console");

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new InnerS3ContainerAnnotation(containerBuilder, "s3"));

        return builder;
    }

    /// <summary>
    /// Wires the S3 endpoint from the local storage container to a project as the S3_ENDPOINT environment variable.
    /// </summary>
    public static IResourceBuilder<T> WithS3Storage<T>(
        this IResourceBuilder<T> projectBuilder,
        IResourceBuilder<ScalewayObjectStorageResource> storageBuilder) where T : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        var s3Annotation = storageBuilder.Resource.Annotations.OfType<InnerS3ContainerAnnotation>().FirstOrDefault();

        if (s3Annotation is not null)
        {
            projectBuilder.WithEnvironment("S3_ENDPOINT", s3Annotation.InnerBuilder.GetEndpoint(s3Annotation.EndpointName));
            projectBuilder.WaitFor(s3Annotation.InnerBuilder);
        }

        return projectBuilder;
    }
}
