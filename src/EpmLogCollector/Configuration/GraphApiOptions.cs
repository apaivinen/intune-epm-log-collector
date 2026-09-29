namespace EpmLogCollector.Configuration;

public sealed class GraphApiOptions
{
    public const string BaseUrlSetting = "GraphBaseUrl";
    public const string DefaultBaseUrl = "https://graph.microsoft.com/beta";

    public string BaseUrl { get; set; } = DefaultBaseUrl;
}