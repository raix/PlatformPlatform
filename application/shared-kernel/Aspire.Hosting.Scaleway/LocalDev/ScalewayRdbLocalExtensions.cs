namespace Aspire.Hosting.Scaleway.LocalDev;

public static class ScalewayRdbLocalExtensions
{
    /// <summary>
    ///     Configures the Scaleway RDB instance to run as a local PostgreSQL container during development.
    ///     In publish mode, the resource remains a Scaleway cloud resource.
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
            .AddPostgres($"{builder.Resource.Name}-container");

        configureContainer?.Invoke(containerBuilder);

        builder.WithAnnotation(new InnerPostgresAnnotation(containerBuilder));
        builder.WithAnnotation(new ConnectionStringRedirectAnnotation(containerBuilder.Resource));

        return builder;
    }

    /// <summary>
    ///     Adds a named database to the Scaleway RDB instance.
    ///     In local dev (after RunAsPostgresContainer), creates the database on the inner PostgreSQL container.
    /// </summary>
    public static IResourceBuilder<PostgresDatabaseResource> AddDatabase(
        this IResourceBuilder<ScalewayRdbInstanceResource> builder,
        string name,
        string? databaseName = null)
    {
        var innerAnnotation = builder.Resource.Annotations.OfType<InnerPostgresAnnotation>().FirstOrDefault();

        if (innerAnnotation is null)
        {
            // Publish mode without RunAsPostgresContainer: create the placeholder Postgres resource on first
            // call and cache it via InnerPostgresAnnotation so subsequent AddDatabase calls reuse the same parent.
            var placeholder = builder.ApplicationBuilder.AddPostgres($"{builder.Resource.Name}-placeholder");
            innerAnnotation = new InnerPostgresAnnotation(placeholder);
            builder.WithAnnotation(innerAnnotation);
        }

        return innerAnnotation.InnerBuilder.AddDatabase(name, databaseName);
    }
}
