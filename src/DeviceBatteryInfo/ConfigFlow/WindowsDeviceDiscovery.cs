using DeviceBatteryInfo.Core;
using DeviceBatteryInfo.Sources.Bluetooth;
using DeviceBatteryInfo.Sources.Razer;

namespace DeviceBatteryInfo.ConfigFlow;

internal sealed class WindowsDeviceDiscovery(
    IRazerHidTransport hidTransport,
    IPnpBatteryReader pnpReader
) : IDeviceDiscovery
{
    public Task<IReadOnlyList<string>> ListBluetoothDeviceNamesAsync(
        CancellationToken cancellationToken
    ) => pnpReader.ListDevicesWithBatteryAsync(cancellationToken);

    public Task<IReadOnlyList<DiscoveredHidDevice>> ListHidDevicesAsync(
        CancellationToken cancellationToken
    ) =>
        // HidSharp opens every device to read its report descriptor, which can block - keep it off
        // the caller's thread
        Task.Run<IReadOnlyList<DiscoveredHidDevice>>(
            () =>
                hidTransport
                    .ListFeatureReportDevices()
                    .Select(c => new DiscoveredHidDevice(
                        c.VendorId,
                        c.ProductId,
                        c.InterfaceNumber,
                        c.ProductName
                    ))
                    .ToArray(),
            cancellationToken
        );
}
