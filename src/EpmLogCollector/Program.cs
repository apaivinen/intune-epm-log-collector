using System.Globalization;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using EpmLogCollector.Configuration;
using EpmLogCollector.Clients;
using EpmLogCollector.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<GraphAuthenticationOptions>()
            .Configure(options =>
            {
                options.TenantId = context.Configuration[GraphAuthenticationOptions.TenantIdSetting] ?? string.Empty;
                options.ClientId = context.Configuration[GraphAuthenticationOptions.ClientIdSetting] ?? string.Empty;
                options.ClientSecret = context.Configuration[GraphAuthenticationOptions.ClientSecretSetting] ?? string.Empty;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.TenantId), "GraphTenantId must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "GraphClientId must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "GraphClientSecret must be configured.");

        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GraphAuthenticationOptions>>().Value;
            return new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
        });
        services.AddSingleton<IGraphAuthenticationService, GraphAuthenticationService>();

        services.AddOptions<GraphApiOptions>()
            .Configure(options =>
            {
                options.BaseUrl = context.Configuration[GraphApiOptions.BaseUrlSetting]
                    ?? GraphApiOptions.DefaultBaseUrl;
            })
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                    && baseUri.Scheme == Uri.UriSchemeHttps,
                "GraphBaseUrl must be an absolute HTTPS URL.");

        services.AddHttpClient<GraphClient>();

        services.AddOptions<BlobCheckpointOptions>()
            .Configure(options =>
            {
                options.ContainerName = context.Configuration[BlobCheckpointOptions.ContainerNameSetting]
                    ?? BlobCheckpointOptions.DefaultContainerName;
                options.BlobName = context.Configuration[BlobCheckpointOptions.BlobNameSetting]
                    ?? BlobCheckpointOptions.DefaultBlobName;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.ContainerName), "CheckpointContainerName must not be empty.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BlobName), "CheckpointBlobName must not be empty.");

        services.AddSingleton(serviceProvider =>
        {
            var connectionString = context.Configuration[BlobCheckpointOptions.StorageConnectionSetting];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("AzureWebJobsStorage must be configured for checkpoint storage.");
            }

            return new BlobServiceClient(connectionString);
        });
        services.AddSingleton<ICheckpointBlobStore>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<BlobCheckpointOptions>>().Value;
            var containerClient = serviceProvider
                .GetRequiredService<BlobServiceClient>()
                .GetBlobContainerClient(options.ContainerName);
            return new BlobCheckpointStore(containerClient, options.BlobName);
        });
        services.AddSingleton<ICheckpointService, BlobCheckpointService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<CheckpointedCollectionRunner>();

        services.AddOptions<CollectionOptions>()
            .Configure(options =>
            {
                var configuredOverlap = context.Configuration[CollectionOptions.OverlapWindowSetting];
                if (string.IsNullOrWhiteSpace(configuredOverlap))
                {
                    return;
                }

                if (!TimeSpan.TryParse(configuredOverlap, CultureInfo.InvariantCulture, out var overlapWindow))
                {
                    throw new InvalidOperationException("CollectionOverlap must be a valid TimeSpan, such as 00:05:00.");
                }

                options.OverlapWindow = overlapWindow;
            })
            .Validate(options => options.OverlapWindow >= TimeSpan.Zero, "CollectionOverlap cannot be negative.");
        services.AddSingleton<ElevationRequestTimestampFilter>();

        services.AddOptions<LogsIngestionOptions>()
            .Configure(options =>
            {
                options.Endpoint = context.Configuration[LogsIngestionOptions.EndpointSetting] ?? string.Empty;
                options.DataCollectionRuleImmutableId =
                    context.Configuration[LogsIngestionOptions.RuleImmutableIdSetting] ?? string.Empty;
                options.StreamName = context.Configuration[LogsIngestionOptions.StreamNameSetting] ?? string.Empty;

                var configuredMaxBatchSize = context.Configuration[LogsIngestionOptions.MaxBatchSizeBytesSetting];
                if (!string.IsNullOrWhiteSpace(configuredMaxBatchSize))
                {
                    if (!int.TryParse(configuredMaxBatchSize, NumberStyles.None, CultureInfo.InvariantCulture, out var maxBatchSizeBytes))
                    {
                        throw new InvalidOperationException("LogsIngestionMaxBatchSizeBytes must be an integer.");
                    }

                    options.MaxBatchSizeBytes = maxBatchSizeBytes;
                }
            })
            .Validate(
                options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri)
                    && endpointUri.Scheme == Uri.UriSchemeHttps,
                "LogsIngestionEndpoint must be an absolute HTTPS URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DataCollectionRuleImmutableId),
                "DataCollectionRuleImmutableId must be configured.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StreamName), "DataCollectionStreamName must be configured.")
            .Validate(
                options => options.MaxBatchSizeBytes is > 2 and <= LogsIngestionOptions.MaximumPayloadSizeBytes,
                $"LogsIngestionMaxBatchSizeBytes must be between 3 and {LogsIngestionOptions.MaximumPayloadSizeBytes}.");

        services.AddSingleton(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LogsIngestionOptions>>().Value;
            var credential = serviceProvider.GetRequiredService<TokenCredential>();
            var clientOptions = new Azure.Monitor.Ingestion.LogsIngestionClientOptions();
            clientOptions.Retry.MaxRetries = 0;
            return new Azure.Monitor.Ingestion.LogsIngestionClient(
                new Uri(options.Endpoint),
                credential,
                clientOptions);
        });
        services.AddSingleton<ILogsIngestionTransport, AzureMonitorLogsIngestionTransport>();
        services.AddSingleton<LogsIngestionClient>();
    })
    .Build();

host.Run();