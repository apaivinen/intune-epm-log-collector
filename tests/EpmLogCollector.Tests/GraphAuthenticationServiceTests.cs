using Azure.Core;
using EpmLogCollector.Services;
using Xunit;

namespace EpmLogCollector.Tests;

public sealed class GraphAuthenticationServiceTests
{
    [Fact]
    public async Task GetAccessTokenAsync_RequestsGraphDefaultScopeAndReturnsToken()
    {
        var credential = new StubTokenCredential("test-access-token");
        var service = new GraphAuthenticationService(credential);

        var accessToken = await service.GetAccessTokenAsync();

        Assert.Equal("test-access-token", accessToken);
        Assert.Equal("https://graph.microsoft.com/.default", credential.RequestedScope);
    }

    private sealed class StubTokenCredential(string token) : TokenCredential
    {
        public string? RequestedScope { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            RequestedScope = requestContext.Scopes.Single();
            return new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(5));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            RequestedScope = requestContext.Scopes.Single();
            return ValueTask.FromResult(new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}