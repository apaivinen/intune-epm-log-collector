namespace EpmLogCollector.Configuration;

public sealed class BlobCheckpointOptions
{
    public const string StorageConnectionSetting = "AzureWebJobsStorage";
    public const string ContainerNameSetting = "CheckpointContainerName";
    public const string BlobNameSetting = "CheckpointBlobName";
    public const string DefaultContainerName = "checkpoints";
    public const string DefaultBlobName = "epm-elevation-requests.json";

    public string ContainerName { get; set; } = DefaultContainerName;
    public string BlobName { get; set; } = DefaultBlobName;
}