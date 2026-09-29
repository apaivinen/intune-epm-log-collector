using System.Net.Http.Headers;
using System.Text.Json;
using EpmLogCollector.Configuration;
using EpmLogCollector.Models.Graph;
using EpmLogCollector.Services;
using Microsoft.Extensions.Options;

namespace EpmLogCollector.Clients;

public sealed class GraphClient(
    HttpClient httpClient,
    IGraphAuthenticationService authenticationService,
    IOptions<GraphApiOptions> options)
{
    private const string ElevationRequestsPath = "deviceManagement/elevationRequests";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri requestsEndpoint = CreateRequestsEndpoint(options.Value.BaseUrl);

    public async Task<IReadOnlyList<ElevationRequest>> GetElevationRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var accessToken = await authenticationService.GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var collection = await JsonSerializer.DeserializeAsync<GraphCollectionResponse<ElevationRequest>>(
            responseStream,
            SerializerOptions,
            cancellationToken);

        return collection?.Value ?? [];
    }

    private static Uri CreateRequestsEndpoint(string baseUrl)
    {
        var normalizedBaseUrl = $"{baseUrl.TrimEnd('/')}/";
        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), ElevationRequestsPath);
    }
}