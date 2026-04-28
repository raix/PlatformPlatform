using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Scaleway;
using FluentAssertions;

namespace Aspire.Hosting.Scaleway.Tests;

public sealed class ScalewayRdbTests
{
    [Fact]
    public void AddScalewayRdbInstance_CreatesResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");

        rdb.Resource.Name.Should().Be("my-db");
        rdb.Resource.Engine.Should().Be(string.Empty);
        rdb.Resource.NodeType.Should().Be(string.Empty);
        rdb.Resource.UserName.Should().Be(string.Empty);
        rdb.Resource.Region.Should().Be(ScalewayRegion.FrPar);
        rdb.Resource.IsHaCluster.Should().BeFalse();
        rdb.Resource.DisableBackup.Should().BeFalse();
    }

    [Fact]
    public void AddScalewayRdbInstance_PropertiesCanBeSet()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");
        rdb.Resource.Engine = "PostgreSQL-16";
        rdb.Resource.NodeType = "DB-DEV-S";
        rdb.Resource.UserName = "admin";
        rdb.Resource.IsHaCluster = true;
        rdb.Resource.Region = ScalewayRegion.NlAms;

        rdb.Resource.Engine.Should().Be("PostgreSQL-16");
        rdb.Resource.NodeType.Should().Be("DB-DEV-S");
        rdb.Resource.UserName.Should().Be("admin");
        rdb.Resource.IsHaCluster.Should().BeTrue();
        rdb.Resource.Region.Should().Be(ScalewayRegion.NlAms);
    }

    [Fact]
    public void AddScalewayRdbInstance_ImplementsIScalewayResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("my-db");

        rdb.Resource.Should().BeAssignableTo<IScalewayResource>();
    }

    [Fact]
    public void RunAsPostgresContainer_ThenAddDatabase_CreatesPostgresDatabase()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("postgres")
            .RunAsPostgresContainer();

        var database = rdb.AddDatabase("account-database", "account");

        database.Resource.Should().BeAssignableTo<IResourceWithConnectionString>();
        database.Resource.Name.Should().Be("account-database");
        database.Resource.Should().BeOfType<PostgresDatabaseResource>();
        ((PostgresDatabaseResource)database.Resource).DatabaseName.Should().Be("account");
    }

    [Fact]
    public void RunAsPostgresContainer_ThenAddMultipleDatabases_AllCreated()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rdb = builder.AddScalewayRdbInstance("postgres")
            .RunAsPostgresContainer();

        var accountDb = rdb.AddDatabase("account-database", "account");
        var backOfficeDb = rdb.AddDatabase("back-office-database", "back_office");
        var mainDb = rdb.AddDatabase("main-database", "main");

        accountDb.Resource.Name.Should().Be("account-database");
        backOfficeDb.Resource.Name.Should().Be("back-office-database");
        mainDb.Resource.Name.Should().Be("main-database");

        // All should share the same parent PostgreSQL server
        var accountParent = ((PostgresDatabaseResource)accountDb.Resource).Parent;
        var backOfficeParent = ((PostgresDatabaseResource)backOfficeDb.Resource).Parent;
        var mainParent = ((PostgresDatabaseResource)mainDb.Resource).Parent;

        accountParent.Should().BeSameAs(backOfficeParent);
        backOfficeParent.Should().BeSameAs(mainParent);
    }

    [Fact]
    public void RunAsPostgresContainer_ConfiguresInnerContainer()
    {
        var builder = DistributedApplication.CreateBuilder();
        var configured = false;

        builder.AddScalewayRdbInstance("postgres")
            .RunAsPostgresContainer(c =>
            {
                configured = true;
                c.WithLifetime(ContainerLifetime.Persistent);
            });

        configured.Should().BeTrue();
    }
}
