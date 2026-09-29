namespace EpmLogCollector.Configuration;

public sealed class LogsIngestionOptions
{
    public const string EndpointSetting = "LogsIngestionEndpoint";
    public const string RuleImmutableIdSetting = "DataCollectionRuleImmutableId";
    public const string StreamNameSetting = "DataCollectionStreamName";
    public const string MaxBatchSizeBytesSetting = "LogsIngestionMaxBatchSizeBytes";
    public const int MaximumPayloadSizeBytes = 1_000_000;
    public const int DefaultMaxBatchSizeBytes = 900_000;

    public string Endpoint { get; set; } = string.Empty;
    public string DataCollectionRuleImmutableId { get; set; } = string.Empty;
    public string StreamName { get; set; } = string.Empty;
    public int MaxBatchSizeBytes { get; set; } = DefaultMaxBatchSizeBytes;
}