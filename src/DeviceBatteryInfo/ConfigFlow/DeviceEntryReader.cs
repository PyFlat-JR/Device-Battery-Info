using DeviceBatteryInfo.Core;
using MacroDeck.Sdk.ConfigFlow;

namespace DeviceBatteryInfo.ConfigFlow;

internal static class DeviceEntryReader
{
    private static readonly TimeSpan BetweenReads = TimeSpan.FromMilliseconds(40);

    public static async Task<IReadOnlyList<BatterySlot>> ReadAsync(
        IIntegrationConfig config,
        CancellationToken cancellationToken
    )
    {
        var entries = await Retry(
            () => config.GetEntriesAsync(cancellationToken),
            cancellationToken
        );
        if (entries.Count == 0)
        {
            return [];
        }

        var ordered = entries.OrderBy(e => e.Id).ToArray();
        var slots = new List<BatterySlot>(ordered.Length);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in ordered)
        {
            async Task<string?> Read(string key)
            {
                await Task.Delay(BetweenReads, cancellationToken).ConfigureAwait(false);
                return await Retry(
                        () => config.GetStringAsync(entry.Id, key, cancellationToken),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            var name = await Read(DeviceConfigKeys.Name) is { Length: > 0 } n ? n : entry.Title;
            var type = DeviceConfigKeys.ParseType(await Read(DeviceConfigKeys.Type));
            var id = BatterySlots.Reserve(BatterySlots.Slug(name), used);

            slots.Add(
                type switch
                {
                    DeviceType.System => new BatterySlot(
                        id,
                        name,
                        BatterySourceKind.System,
                        DeviceType.System
                    ),
                    DeviceType.Bluetooth => new BatterySlot(
                        id,
                        name,
                        DeviceConfigKeys.ParseKind(await Read(DeviceConfigKeys.BluetoothKind)),
                        DeviceType.Bluetooth,
                        BluetoothFriendlyName: await Read(DeviceConfigKeys.BluetoothName)
                    ),
                    DeviceType.RazerDeathAdderV3Pro => await ReadRazerAsync(id, name, Read),
                    _ => new BatterySlot(
                        id,
                        name,
                        BatterySourceKind.Phone,
                        DeviceType.AdbPhone,
                        AdbAddress: (await Read(DeviceConfigKeys.AdbAddress))?.Trim(),
                        AdbExecutable: await Read(DeviceConfigKeys.AdbExecutable)
                            is { Length: > 0 } exe
                            ? exe
                            : "adb"
                    ),
                }
            );
        }

        return slots;
    }

    private static async Task<BatterySlot> ReadRazerAsync(
        string id,
        string name,
        Func<string, Task<string?>> read
    )
    {
        var model = DeviceModelCatalog.ById(await read(DeviceConfigKeys.CatalogDevice));
        var vendorId = model is { VendorId: > 0 }
            ? model.VendorId
            : DeviceConfigKeys.ParseUsbId(await read(DeviceConfigKeys.VendorId)) ?? 0x1532;
        var productId = model is { ProductId: > 0 }
            ? model.ProductId
            : DeviceConfigKeys.ParseUsbId(await read(DeviceConfigKeys.ProductId)) ?? 0x00B7;

        return new BatterySlot(
            id,
            name,
            BatterySourceKind.Mouse,
            DeviceType.RazerDeathAdderV3Pro,
            VendorId: vendorId,
            ProductId: productId
        );
    }

    private static async Task<T> Retry<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < 4 && IsTransient(exception))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150 * attempt), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception.GetType().Name == "HostInvocationException"
        && (
            exception.Message.Contains("too quickly", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
        );
}
