using Azure.Core;

namespace EpmLogCollector.Services;

public sealed class GraphAuthenticationService(TokenCredential credential) : IGraphAuthenticationService
{
    private static readonly TokenRequestContext GraphTokenRequest =
        new(["https://graph.microsoft.com/.default"]);

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await credential.GetTokenAsync(GraphTokenRequest, cancellationToken);
        return accessToken.Token;
    }
}