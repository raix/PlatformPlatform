using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayCredentialTests
{
    [Fact]
    public void ScalewayCredentialConfig_HasCorrectDefaults()
    {
        var config = new ScalewayCredentialConfig();

        config.AccessKey.Should().BeNull();
        config.SecretKey.Should().BeNull();
        config.DefaultProjectId.Should().BeNull();
        config.DefaultOrganizationId.Should().BeNull();
        config.DefaultRegion.Should().Be(ScalewayRegion.FrPar);
        config.DefaultZone.Should().Be(ScalewayZone.FrPar1);
        config.ApiUrl.Should().Be("https://api.scaleway.com");
    }

    [Fact]
    public void AddScalewayCredentialConfig_RegistersResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var credentials = builder.AddScalewayCredentialConfig(
            accessKey: "SCWXXXXXXXXXXXXXXXXXXX",
            secretKey: "00000000-0000-0000-0000-000000000000",
            defaultProjectId: "project-123"
        );

        credentials.Resource.Name.Should().Be("scaleway-credentials");
        credentials.Resource.Config.AccessKey.Should().Be("SCWXXXXXXXXXXXXXXXXXXX");
        credentials.Resource.Config.SecretKey.Should().Be("00000000-0000-0000-0000-000000000000");
        credentials.Resource.Config.DefaultProjectId.Should().Be("project-123");
    }
}
