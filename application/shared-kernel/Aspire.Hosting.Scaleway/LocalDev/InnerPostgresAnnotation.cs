namespace Aspire.Hosting.Scaleway.LocalDev;

/// <summary>
///     Annotation that stores a reference to the inner PostgreSQL container
///     created by RunAsPostgresContainer, so AddDatabase can delegate to it.
/// </summary>
internal sealed class InnerPostgresAnnotation(IResourceBuilder<PostgresServerResource> innerBuilder) : IResourceAnnotation
{
    public IResourceBuilder<PostgresServerResource> InnerBuilder { get; } = innerBuilder;
}
