using System.Net;
using System.Net.Http.Headers;
using System.Text;
using EpmLogCollector.Clients;
using EpmLogCollector.Configuration;
using EpmLogCollector.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class GraphClientTests
{
    [Fact]
    public async Task GetElevationRequestsAsync_SendsAuthenticatedRequestAndDeserializesResponse()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(
                "https://graph.microsoft.com/beta/deviceManagement/elevationRequests",
                request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            Assert.Contains(
                request.Headers.Accept,
                header => header.MediaType == "application/json");

            const string json = """
                {
                  "value": [
                    {
                      "id": "request-123",
                      "requestedByUserPrincipalName": "user@example.com",
                      "status": "pending"
                    }
                  ]
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }));

        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        var requests = await client.GetElevationRequestsAsync();

        var request = Assert.Single(requests);
        Assert.Equal("request-123", request.Id);
        Assert.Equal("user@example.com", request.RequestedByUserPrincipalName);
        Assert.Equal("pending", request.Status);
    }

    [Fact]
    public async Task GetElevationRequestsAsync_ThrowsForUnsuccessfulResponse()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }));
        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetElevationRequestsAsync());

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(1, requestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetElevationRequestsAsync_RetriesTransientStatusCodes(HttpStatusCode transientStatusCode)
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var transientResponse = new HttpResponseMessage(transientStatusCode);
                transientResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(transientResponse);
            }

            return Task.FromResult(CreateSuccessResponse());
        }));
        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        var requests = await client.GetElevationRequestsAsync();

        Assert.Single(requests);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetElevationRequestsAsync_RetriesTransportFailure()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                throw new HttpRequestException("Temporary network failure.");
            }

            return Task.FromResult(CreateSuccessResponse());
        }));
        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        var requests = await client.GetElevationRequestsAsync();

        Assert.Single(requests);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task GetElevationRequestsAsync_StopsAfterMaximumRetryAttempts()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            var transientResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            transientResponse.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(transientResponse);
        }));
        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetElevationRequestsAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal(4, requestCount);
    }

    [Fact]
    public async Task GetElevationRequestsAsync_FollowsAllPagesInOrder()
    {
        var requestedUrls = new List<string>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            requestedUrls.Add(request.RequestUri!.AbsoluteUri);

            var json = requestedUrls.Count switch
            {
                1 => """
                    {
                      "value": [{ "id": "request-1" }],
                      "@odata.nextLink": "https://graph.microsoft.com/beta/deviceManagement/elevationRequests?$skiptoken=page-2"
                    }
                    """,
                2 => """
                    {
                      "value": [{ "id": "request-2" }],
                      "@odata.nextLink": "https://graph.microsoft.com/beta/deviceManagement/elevationRequests?$skiptoken=page-3"
                    }
                    """,
                3 => """
                    {
                      "value": [{ "id": "request-3" }]
                    }
                    """,
                _ => throw new InvalidOperationException("Unexpected Graph page request.")
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }));
        var authenticationService = new StubGraphAuthenticationService();
        var client = new GraphClient(
            httpClient,
            authenticationService,
            Options.Create(new GraphApiOptions()));

        var requests = await client.GetElevationRequestsAsync();

        Assert.Equal(
            [
                "https://graph.microsoft.com/beta/deviceManagement/elevationRequests",
                "https://graph.microsoft.com/beta/deviceManagement/elevationRequests?$skiptoken=page-2",
                "https://graph.microsoft.com/beta/deviceManagement/elevationRequests?$skiptoken=page-3"
            ],
            requestedUrls);
        Assert.Equal(1, authenticationService.CallCount);
        Assert.Equal(["request-1", "request-2", "request-3"], requests.Select(request => request.Id));
    }

    [Fact]
    public async Task GetElevationRequestsAsync_RejectsPaginationLinkOutsideGraphOrigin()
    {
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            const string json = """
                {
                  "value": [],
                  "@odata.nextLink": "https://example.com/collect-token"
                }
                """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }));
        var client = new GraphClient(
            httpClient,
            new StubGraphAuthenticationService(),
            Options.Create(new GraphApiOptions()));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetElevationRequestsAsync());

        Assert.Equal(1, requestCount);
    }

    private sealed class StubGraphAuthenticationService : IGraphAuthenticationService
    {
        public int CallCount { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult("test-token");
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return sendAsync(request, cancellationToken);
        }
    }

    private static HttpResponseMessage CreateSuccessResponse()
    {
        const string json = """
            {
              "value": [{ "id": "request-123" }]
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}