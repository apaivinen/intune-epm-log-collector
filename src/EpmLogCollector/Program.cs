using Azure.Core;
using Azure.Identity;
using EpmLogCollector.Configuration;
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
    })
    .Build();

host.Run();