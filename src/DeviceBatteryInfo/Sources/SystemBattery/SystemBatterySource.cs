using System.Runtime.InteropServices;
using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal sealed class SystemBatterySource(BatterySlot slot) : IBatterySource
{
    public string Id { get; } = slot.Id;

    public string DisplayName { get; } = slot.DisplayName;

    public BatterySourceKind Kind => BatterySourceKind.System;

    public ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken)
    {
        if (!NativePowerStatusApi.GetSystemPowerStatus(out var status))
        {
            throw new InvalidOperationException(
                $"GetSystemPowerStatus failed (Win32 error {Marshal.GetLastPInvokeError()})."
            );
        }

        return ValueTask.FromResult(SystemBatteryReadingFactory.Create(status));
    }
}

internal sealed class SystemBatterySourceProvider(DeviceCatalog catalog) : IBatterySourceProvider
{
    private readonly DeviceCatalog _catalog = catalog;

    public ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        if (
            !OperatingSystem.IsWindows()
            || !NativePowerStatusApi.GetSystemPowerStatus(out var status)
            || !SystemBatteryReadingFactory.HasBattery(status)
        )
        {
            return ValueTask.FromResult<IReadOnlyList<IBatterySource>>(
                Array.Empty<IBatterySource>()
            );
        }

        var sources = _catalog
            .Devices.Where(d => d.Type == DeviceType.System)
            .Select(IBatterySource (d) => new SystemBatterySource(d))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<IBatterySource>>(sources);
    }
}
