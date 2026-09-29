namespace EpmLogCollector.Services;

public interface IGraphAuthenticationService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}