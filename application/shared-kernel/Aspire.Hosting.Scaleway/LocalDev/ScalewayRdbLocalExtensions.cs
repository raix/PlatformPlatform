using Aspire.Hosting.Scaleway;

namespace Aspire.Hosting;

public static class ScalewayRdbLocalExtensions
{
    /// <summary>
    /// Configures the Scaleway RDB instance to run as a local PostgreSQL container during development.
    /// In publish mode, the resource remains a Scaleway cloud resource.
    /// </summary>
    public static IResourceBuilder<ScalewayRdbInstanceResource> RunAsPostgresContainer(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        Action<IResourceBuilder<PostgresServerResource>>? configureContainer = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        var containerBuilder = builder.ApplicationBuilder
            .AddPostgres(
                $"{builder.Resource.Name}-container",
                password: builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.PasswordParameter)
            );

        configureContainer?.Invoke(containerBuilder);

        foreach (var (resourceName, databaseName) in builder.Resource.Databases)
        {
            containerBuilder.AddDatabase(resourceName, databaseName);
        }

        builder.WithAnnotation(new ConnectionStringRedirectAnnotation(containerBuilder.Resource));

        return builder;
    }
}
