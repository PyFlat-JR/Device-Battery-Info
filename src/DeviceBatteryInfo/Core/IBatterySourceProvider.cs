namespace DeviceBatteryInfo.Core;

/// <summary>Discovers the battery sources of one kind. Called every poll cycle: must be cheap, and must
/// return an empty list rather than throw when nothing is connected.</summary>
public interface IBatterySourceProvider
{
    ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(CancellationToken cancellationToken);
}
