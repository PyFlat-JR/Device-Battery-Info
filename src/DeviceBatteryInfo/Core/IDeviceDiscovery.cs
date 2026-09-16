namespace DeviceBatteryInfo.Core;

public interface IDeviceDiscovery
{
    Task<IReadOnlyList<string>> ListBluetoothDeviceNamesAsync(CancellationToken cancellationToken);

    // Not used by any picker today; kept for a future "scan for supported devices" step.
    Task<IReadOnlyList<DiscoveredHidDevice>> ListHidDevicesAsync(
        CancellationToken cancellationToken
    );
}

public sealed record DiscoveredHidDevice(
    int VendorId,
    int ProductId,
    int? InterfaceNumber,
    string ProductName
);
