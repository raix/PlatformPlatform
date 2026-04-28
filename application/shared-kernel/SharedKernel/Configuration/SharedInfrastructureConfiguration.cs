using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SharedKernel.Integrations.BlobStorage;
using SharedKernel.Telemetry;

namespace SharedKernel.Configuration;

public static class SharedInfrastructureConfiguration
{
    public static readonly bool IsRunningInScaleway = Environment.GetEnvironmentVariable("SCW_SECRET_KEY") is not null;

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddSharedInfrastructure<T>(string connectionName)
            where T : DbContext
        {
            builder
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
        private IHostApplicationBuilder ConfigureDatabaseContext<T>(string connectionName)
            where T : DbContext
        {
            // Scaleway RDB and local dev both use standard connection strings
            var connectionString = IsRunningInScaleway
                ? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
                : builder.Configuration.GetConnectionString(connectionName);

            builder.Services.AddDbContext<T>(options =>
                options.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
            );

            return builder;
        }

        private IHostApplicationBuilder AddDefaultBlobStorage()
        {
            var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
            if (s3Endpoint is null)
            {
                // Register a no-op client for test/build scenarios where S3 is not available
                builder.Services.TryAddSingleton<IBlobStorageClient>(sp =>
                    new S3BlobStorageClient(new AmazonS3Client(new AmazonS3Config { ServiceURL = "http://localhost:8333", ForcePathStyle = true }), "http://localhost:8333", sp.GetRequiredService<TimeProvider>())
                );
                return builder;
            }

            var s3Config = new AmazonS3Config
            {
                ServiceURL = s3Endpoint,
                ForcePathStyle = true,
                UseHttp = !IsRunningInScaleway && !s3Endpoint.StartsWith("https")
            };

            var s3Client = IsRunningInScaleway
                ? new AmazonS3Client(
                    Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
                    Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
                    s3Config
                )
                : new AmazonS3Client(s3Config);

            builder.Services.AddSingleton<IBlobStorageClient>(sp =>
                new S3BlobStorageClient(s3Client, s3Endpoint, sp.GetRequiredService<TimeProvider>())
            );

            return builder;
        }

        /// <summary>
        ///     Register different storage accounts for BlobStorage using .NET Keyed services, when a service needs to access
        ///     multiple storage accounts.
        /// </summary>
        public IHostApplicationBuilder AddNamedBlobStorages((string ConnectionName, string EnvironmentVariable)?[] connections)
        {
            var s3Endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT");
            if (s3Endpoint is null)
            {
                // Register no-op keyed clients for test/build scenarios
                foreach (var connection in connections)
                {
                    builder.Services.TryAddKeyedSingleton<IBlobStorageClient>(connection!.Value.ConnectionName,
                        (sp, _) => new S3BlobStorageClient(new AmazonS3Client(new AmazonS3Config { ServiceURL = "http://localhost:8333", ForcePathStyle = true }), "http://localhost:8333", sp.GetRequiredService<TimeProvider>())
                    );
                }
                return builder;
            }

            var s3Config = new AmazonS3Config
            {
                ServiceURL = s3Endpoint,
                ForcePathStyle = true,
                UseHttp = !IsRunningInScaleway && !s3Endpoint.StartsWith("https")
            };

            var s3Client = IsRunningInScaleway
                ? new AmazonS3Client(
                    Environment.GetEnvironmentVariable("SCW_ACCESS_KEY"),
                    Environment.GetEnvironmentVariable("SCW_SECRET_KEY"),
                    s3Config
                )
                : new AmazonS3Client(s3Config);

            foreach (var connection in connections)
            {
                builder.Services.AddKeyedSingleton<IBlobStorageClient>(connection!.Value.ConnectionName,
                    (sp, _) => new S3BlobStorageClient(s3Client, s3Endpoint, sp.GetRequiredService<TimeProvider>())
                );
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
