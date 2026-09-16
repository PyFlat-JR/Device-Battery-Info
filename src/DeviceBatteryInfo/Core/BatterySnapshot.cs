namespace DeviceBatteryInfo.Core;

public sealed record BatterySnapshot
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required BatterySourceKind Kind { get; init; }

    public required BatteryReading Reading { get; init; }

    public bool IsStale { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }
}
