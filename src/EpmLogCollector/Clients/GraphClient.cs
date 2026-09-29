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
        var requests = new List<ElevationRequest>();
        var visitedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Uri? pageUri = requestsEndpoint;

        while (pageUri is not null)
        {
            if (!visitedPages.Add(pageUri.AbsoluteUri))
            {
                throw new InvalidDataException("Microsoft Graph returned a repeated pagination link.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
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

            if (collection?.Value is { Count: > 0 } pageRequests)
            {
                requests.AddRange(pageRequests);
            }

            pageUri = ResolveNextPageUri(collection?.NextLink);
        }

        return requests;
    }

    private static Uri CreateRequestsEndpoint(string baseUrl)
    {
        var normalizedBaseUrl = $"{baseUrl.TrimEnd('/')}/";
        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), ElevationRequestsPath);
    }

    private Uri? ResolveNextPageUri(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return null;
        }

        if (!Uri.TryCreate(requestsEndpoint, nextLink, out var nextPageUri)
            || !string.Equals(
                nextPageUri.GetLeftPart(UriPartial.Authority),
                requestsEndpoint.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Microsoft Graph returned a pagination link outside the configured Graph origin.");
        }

        return nextPageUri;
    }
}