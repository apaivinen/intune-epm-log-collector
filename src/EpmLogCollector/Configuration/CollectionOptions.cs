namespace EpmLogCollector.Configuration;

public sealed class CollectionOptions
{
    public const string OverlapWindowSetting = "CollectionOverlap";

    public TimeSpan OverlapWindow { get; set; } = TimeSpan.FromMinutes(5);
}