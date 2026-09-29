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
    })
    .Build();

host.Run();