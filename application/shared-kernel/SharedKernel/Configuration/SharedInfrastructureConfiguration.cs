using Amazon.S3;
using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SharedKernel.Integrations.BlobStorage;
using SharedKernel.Telemetry;

namespace SharedKernel.Configuration;

public static class SharedInfrastructureConfiguration
{
    public static readonly bool IsRunningInAzure = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") is not null;

    public static readonly bool IsRunningInScaleway = Environment.GetEnvironmentVariable("SCW_SECRET_KEY") is not null;

    public static readonly bool IsRunningInCloud = IsRunningInAzure || IsRunningInScaleway;

    public static DefaultAzureCredential DefaultAzureCredential => GetDefaultAzureCredential();

    private static DefaultAzureCredential GetDefaultAzureCredential()
    {
        // Hack: Remove trailing whitespace from the environment variable, added in Bicep to workaround issue #157.
        var managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")!.Trim();
        var credentialOptions = new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentityClientId };
        return new DefaultAzureCredential(credentialOptions);
    }

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddSharedInfrastructure<T>(string connectionName)
            where T : DbContext
        {
            builder
                .AddAzureKeyVaultConfiguration()
                .ConfigureDatabaseContext<T>(connectionName)
                .AddDefaultBlobStorage()
                .AddConfigureOpenTelemetry()
                .AddOpenTelemetryExporters();

            builder.Services
                .AddScoped<OpenTelemetryEnricher>()
                .ConfigureHttpClientDefaults(http =>
                    {
                        http.AddStandardResilienceHandler(); // Turn on resilience by default
                        http.AddServiceDiscovery(); // Turn on service discovery by default
                    }
                );

            return builder;
        }
    }

    extension(IHostApplicationBuilder builder)
    {
        private IHostApplicationBuilder AddAzureKeyVaultConfiguration()
        {
            if (IsRunningInAzure)
            {
                var keyVaultUri = new Uri(Environment.GetEnvironmentVariable("KEYVAULT_URL")!);
                var secretClient = new SecretClient(keyVaultUri, DefaultAzureCredential);

                builder.Configuration.AddAzureKeyVault(secretClient, new AzureKeyVaultConfigurationOptions
                    {
                        Manager = new KeyVaultSecretManager(),
                        ReloadInterval = TimeSpan.FromMinutes(1)
                    }
                );
            }

            // Scaleway: secrets are injected as environment variables by the container runtime,
            // no additional configuration source needed.

            return builder;
        }

        private IHostApplicationBuilder ConfigureDatabaseContext<T>(string connectionName)
            where T : DbContext
        {
            if (IsRunningInAzure)
            {
                var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
                dataSourceBuilder.UsePeriodicPasswordProvider(async (_, cancellationToken) =>
                    {
                        var token = await DefaultAzureCredential.GetTokenAsync(new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), cancellationToken);
                        return token.Token;
                    }, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5)
                );
                var dataSource = dataSourceBuilder.Build();
                builder.Services.AddSingleton(dataSource);
                builder.Services.AddDbContext<T>(options =>
                    options.UseNpgsql(dataSource, o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                );
            }
            else
            {
                // Scaleway RDB and local dev both use standard connection strings
                var connectionString = IsRunningInScaleway
                    ? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                    : builder.Configuration.GetConnectionString(connectionName);

                builder.Services.AddDbContext<T>(options =>
                    options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                );
            }

            return builder;
        }

        private IHostApplicationBuilder AddDefaultBlobStorage()
        {
            if (IsRunningInScaleway)
            {
                var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT")
                    ?? throw new InvalidOperationException("S3_ENDPOINT environment variable is required for Scaleway.");
                var s3Client = new AmazonS3Client(
                    Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
                    Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
                    new AmazonS3Config { ServiceURL = s3Endpoint, ForcePathStyle = true }
                );
                builder.Services.AddSingleton<IBlobStorageClient>(sp =>
                    new S3BlobStorageClient(s3Client, s3Endpoint, sp.GetRequiredService<TimeProvider>())
                );
            }
            else if (IsRunningInAzure)
            {
                var defaultBlobStorageUri = new Uri(Environment.GetEnvironmentVariable("BLOB_STORAGE_URL")!);
                builder.Services.AddSingleton<IBlobStorageClient>(sp =>
                    new AzureBlobStorageClient(new BlobServiceClient(defaultBlobStorageUri, DefaultAzureCredential), sp.GetRequiredService<TimeProvider>())
                );
            }
            else
            {
                // Local dev: use S3 (SeaweedFS/MinIO) if available, else Azure emulator
                var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
                if (s3Endpoint is not null)
                {
                    var s3Client = new AmazonS3Client(new AmazonS3Config
                    {
                        ServiceURL = s3Endpoint,
                        ForcePathStyle = true,
                        UseHttp = !s3Endpoint.StartsWith("https")
                    });
                    builder.Services.AddSingleton<IBlobStorageClient>(sp =>
                        new S3BlobStorageClient(s3Client, s3Endpoint, sp.GetRequiredService<TimeProvider>())
                    );
                }
                else
                {
                    var connectionString = builder.Configuration.GetConnectionString("blob-storage");
                    builder.Services.AddSingleton<IBlobStorageClient>(sp =>
                        new AzureBlobStorageClient(new BlobServiceClient(connectionString), sp.GetRequiredService<TimeProvider>())
                    );
                }
            }

            return builder;
        }

        /// <summary>
        ///     Register different storage accounts for BlobStorage using .NET Keyed services, when a service needs to access
        ///     multiple storage accounts.
        /// </summary>
        public IHostApplicationBuilder AddNamedBlobStorages((string ConnectionName, string EnvironmentVariable)?[] connections)
        {
            if (IsRunningInAzure)
            {
                foreach (var connection in connections)
                {
                    var storageEndpointUri = new Uri(Environment.GetEnvironmentVariable(connection!.Value.EnvironmentVariable)!);
                    builder.Services.AddKeyedSingleton<IBlobStorageClient>(connection.Value.ConnectionName,
                        (sp, _) => new AzureBlobStorageClient(new BlobServiceClient(storageEndpointUri, DefaultAzureCredential), sp.GetRequiredService<TimeProvider>())
                    );
                }
            }
            else
            {
                var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
                if (s3Endpoint is not null)
                {
                    var s3Client = new AmazonS3Client(new AmazonS3Config
                    {
                        ServiceURL = s3Endpoint,
                        ForcePathStyle = true,
                        UseHttp = !s3Endpoint.StartsWith("https")
                    });
                    foreach (var connection in connections)
                    {
                        builder.Services.AddKeyedSingleton<IBlobStorageClient>(connection!.Value.ConnectionName,
                            (sp, _) => new S3BlobStorageClient(s3Client, s3Endpoint, sp.GetRequiredService<TimeProvider>())
                        );
                    }
                }
                else
                {
                    var connectionString = builder.Configuration.GetConnectionString("blob-storage");
                    foreach (var connection in connections)
                    {
                        builder.Services.AddKeyedSingleton<IBlobStorageClient>(connection!.Value.ConnectionName,
                            (sp, _) => new AzureBlobStorageClient(new BlobServiceClient(connectionString), sp.GetRequiredService<TimeProvider>())
                        );
                    }
                }
            }

            return builder;
        }

        private IHostApplicationBuilder AddConfigureOpenTelemetry()
        {
            builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
                {
                    // ReSharper disable once RedundantLambdaParameterType
                    options.Filter = (HttpContext httpContext) =>
                    {
                        var requestPath = httpContext.Request.Path.ToString();

                        if (EndpointTelemetryFilter.ExcludedPaths.Any(requestPath.StartsWith))
                        {
                            return false;
                        }

                        if (EndpointTelemetryFilter.ExcludedFileExtensions.Any(requestPath.EndsWith))
                        {
                            return false;
                        }

                        return true;
                    };
                }
            );

            builder.Logging.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                }
            );

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                    {
                        metrics.AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .AddRuntimeInstrumentation();
                    }
                )
                .WithTracing(tracing =>
                    {
                        // We want to view all traces in development
                        if (builder.Environment.IsDevelopment()) tracing.SetSampler(new AlwaysOnSampler());

                        tracing.AddAspNetCoreInstrumentation().AddGrpcClientInstrumentation().AddHttpClientInstrumentation();
                    }
                );

            return builder;
        }

        private IHostApplicationBuilder AddOpenTelemetryExporters()
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services
                    .Configure<OpenTelemetryLoggerOptions>(logging => logging.AddOtlpExporter())
                    .ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter())
                    .ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
            }

            return builder;
        }
    }
}
