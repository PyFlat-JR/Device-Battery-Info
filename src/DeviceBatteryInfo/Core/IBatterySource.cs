namespace DeviceBatteryInfo.Core;

/// <summary>One battery whose level can be polled. To add a device, implement this plus an
/// <see cref="IBatterySourceProvider"/> that discovers it, and register the provider in
/// <c>Program.cs</c> - see <c>docs/adding-a-device.md</c>.</summary>
public interface IBatterySource
{
    // Lowercase kebab-case, unique, and a public API: it prefixes every battery_<id>_* variable, so
    // changing it breaks bindings users already made.
    string Id { get; }

    string DisplayName { get; }

    BatterySourceKind Kind { get; }

    ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken);
}
