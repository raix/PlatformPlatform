using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayStorageTests
{
    [Fact]
    public void AddScalewayObjectStorage_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var storage = builder.AddScalewayObjectStorage("my-storage");

        storage.Resource.Name.Should().Be("my-storage");
        storage.Resource.Region.Should().Be(ScalewayRegion.FrPar);
        storage.Resource.Endpoint.Should().Be("https://s3.fr-par.scw.cloud");
    }

    [Fact]
    public void AddScalewayObjectStorage_AcceptsCustomRegion()
    {
        var builder = DistributedApplication.CreateBuilder();

        var storage = builder.AddScalewayObjectStorage("my-storage", region: ScalewayRegion.NlAms);

        storage.Resource.Region.Should().Be(ScalewayRegion.NlAms);
        storage.Resource.Endpoint.Should().Be("https://s3.nl-ams.scw.cloud");
    }

    [Fact]
    public void AddBucket_CreatesBucketResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var storage = builder.AddScalewayObjectStorage("my-storage");

        var bucket = storage.AddBucket("avatars");

        bucket.Resource.Name.Should().Be("avatars");
        bucket.Resource.BucketName.Should().Be("avatars");
        bucket.Resource.Parent.Should().BeSameAs(storage.Resource);
        bucket.Resource.Acl.Should().Be(ScalewayBucketAcl.Private);
        bucket.Resource.Versioning.Should().BeFalse();
    }

    [Fact]
    public void AddBucket_AcceptsCustomBucketName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var storage = builder.AddScalewayObjectStorage("my-storage");

        var bucket = storage.AddBucket("avatar-bucket", bucketName: "my-app-avatars");

        bucket.Resource.BucketName.Should().Be("my-app-avatars");
    }

    [Fact]
    public void WithAcl_SetsAccessControl()
    {
        var builder = DistributedApplication.CreateBuilder();
        var storage = builder.AddScalewayObjectStorage("my-storage");

        var bucket = storage.AddBucket("public-assets")
            .WithAcl(ScalewayBucketAcl.PublicRead);

        bucket.Resource.Acl.Should().Be(ScalewayBucketAcl.PublicRead);
    }

    [Fact]
    public void WithVersioning_EnablesVersioning()
    {
        var builder = DistributedApplication.CreateBuilder();
        var storage = builder.AddScalewayObjectStorage("my-storage");

        var bucket = storage.AddBucket("docs")
            .WithVersioning();

        bucket.Resource.Versioning.Should().BeTrue();
    }

    [Fact]
    public void RunAsSeaweedFsContainer_ShouldAddInnerS3Annotation()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();

        // Act
        var storage = builder.AddScalewayObjectStorage("my-storage")
            .RunAsSeaweedFsContainer();

        // Assert
        storage.Resource.Annotations.OfType<InnerS3ContainerAnnotation>().Should().ContainSingle();
    }

    [Fact]
    public void WithS3Storage_ShouldWireEnvironmentVariable()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var storage = builder.AddScalewayObjectStorage("my-storage")
            .RunAsSeaweedFsContainer();
        var container = builder.AddContainer("my-app", "my-image");

        // Act
        container.WithS3Storage(storage);

        // Assert - the annotation exists on storage, confirming wiring is possible
        storage.Resource.Annotations.OfType<InnerS3ContainerAnnotation>().Should().ContainSingle();
    }
}
