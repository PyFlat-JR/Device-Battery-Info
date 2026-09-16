using DeviceBatteryInfo.Core;
using MacroDeck.Localization;

namespace DeviceBatteryInfo.ConfigFlow;

/// <summary>A specific, confirmed device model listed under "Other devices" in the config flow.</summary>
internal sealed record CatalogDevice(
    string Id,
    string BrandId,
    LocalizedText BrandLabel,
    LocalizedText ModelLabel,
    DeviceType BackendType,
    int VendorId = 0,
    int ProductId = 0
);

/// <summary>Add an entry here for each new device model (see <c>docs/adding-a-device.md</c>).</summary>
internal static class DeviceModelCatalog
{
    private static readonly IReadOnlyList<CatalogDevice> Entries =
    [
        new CatalogDevice(
            "razer-deathadder-v3-pro",
            "razer",
            Strings.ConfigFlow.Device.Brand.Razer(),
            Strings.ConfigFlow.Device.Model.RazerDeathAdderV3Pro(),
            DeviceType.RazerDeathAdderV3Pro,
            VendorId: 0x1532,
            ProductId: 0x00B7
        ),
    ];

    public static bool NeedsDetailsStep(DeviceType backendType) =>
        backendType is DeviceType.AdbPhone or DeviceType.Bluetooth;

    public static IReadOnlyList<(string Id, LocalizedText Label)> Brands =>
        Entries.GroupBy(e => e.BrandId).Select(g => (g.Key, g.First().BrandLabel)).ToArray();

    public static IReadOnlyList<CatalogDevice> ModelsFor(string? brandId) =>
        Entries.Where(e => e.BrandId == brandId).ToArray();

    public static CatalogDevice? ById(string? id) =>
        id is { Length: > 0 } ? Entries.FirstOrDefault(e => e.Id == id) : null;

    // Assumes at most one entry per backend type.
    public static CatalogDevice? ForBackendType(DeviceType type) =>
        Entries.FirstOrDefault(e => e.BackendType == type);
}
