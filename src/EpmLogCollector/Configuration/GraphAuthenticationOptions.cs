namespace EpmLogCollector.Configuration;

public sealed class GraphAuthenticationOptions
{
    public const string TenantIdSetting = "GraphTenantId";
    public const string ClientIdSetting = "GraphClientId";
    public const string ClientSecretSetting = "GraphClientSecret";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}