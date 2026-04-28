using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayTemTests
{
    [Fact]
    public void AddScalewayTransactionalEmail_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tem = builder.AddScalewayTransactionalEmail("my-email", "example.com");

        tem.Resource.Name.Should().Be("my-email");
        tem.Resource.DomainName.Should().Be("example.com");
        tem.Resource.AcceptTos.Should().BeTrue();
        tem.Resource.SmtpHost.Should().Be("smtp.tem.scw.cloud");
        tem.Resource.SmtpPort.Should().Be(465);
        tem.Resource.Region.Should().Be(ScalewayRegion.FrPar);
    }

    [Fact]
    public void WithSmtpPort_SetsPort()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tem = builder.AddScalewayTransactionalEmail("my-email", "example.com")
            .WithSmtpPort(587);

        tem.Resource.SmtpPort.Should().Be(587);
    }
}
