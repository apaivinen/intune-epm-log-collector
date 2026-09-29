namespace EpmLogCollector.Configuration;

public sealed class LogsIngestionOptions
{
    public const string EndpointSetting = "LogsIngestionEndpoint";
    public const string RuleImmutableIdSetting = "DataCollectionRuleImmutableId";
    public const string StreamNameSetting = "DataCollectionStreamName";

    public string Endpoint { get; set; } = string.Empty;
    public string DataCollectionRuleImmutableId { get; set; } = string.Empty;
    public string StreamName { get; set; } = string.Empty;
}