using System.Net;
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
    private const int MaxRetryAttempts = 3;
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

            using var response = await SendWithRetryAsync(pageUri, accessToken, cancellationToken);
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

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Uri pageUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, pageUri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxRetryAttempts)
            {
                await Task.Delay(GetExponentialRetryDelay(attempt), cancellationToken);
                continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && attempt < MaxRetryAttempts)
            {
                await Task.Delay(GetExponentialRetryDelay(attempt), cancellationToken);
                continue;
            }

            if (!IsTransientStatusCode(response.StatusCode) || attempt >= MaxRetryAttempts)
            {
                return response;
            }

            var retryDelay = GetRetryDelay(response, attempt);
            response.Dispose();
            await Task.Delay(retryDelay, cancellationToken);
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var statusCodeValue = (int)statusCode;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            || statusCodeValue is >= 500 and <= 599;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } retryAfterDelay)
        {
            return retryAfterDelay > TimeSpan.Zero ? retryAfterDelay : TimeSpan.Zero;
        }

        if (retryAfter?.Date is { } retryAfterDate)
        {
            var delayUntilRetry = retryAfterDate - DateTimeOffset.UtcNow;
            return delayUntilRetry > TimeSpan.Zero ? delayUntilRetry : TimeSpan.Zero;
        }

        return GetExponentialRetryDelay(attempt);
    }

    private static TimeSpan GetExponentialRetryDelay(int attempt)
    {
        return TimeSpan.FromSeconds(Math.Min(30, 1 << attempt));
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