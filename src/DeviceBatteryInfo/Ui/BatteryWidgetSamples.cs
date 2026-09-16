using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Ui;

internal static class BatteryWidgetSamples
{
    public static BatteryWidgetModel For(string localId) =>
        localId == BatteryWidgetTypes.TileId ? TileCharging() : Panel();

    public static BatteryWidgetModel Panel() =>
        new(
            [
                Row(
                    "mouse",
                    "Mouse",
                    82,
                    BatteryStatus.Charging,
                    charging: true,
                    timeToFull: "0:35"
                ),
                Row("phone", "Phone", 47, BatteryStatus.Discharging, trend: "-13%/1h"),
                Row("headset", "Headset", 100, BatteryStatus.Full),
            ],
            BatteryWidgetOptions.Default with
            {
                Title = "Batteries",
            }
        );

    public static BatteryWidgetModel PanelLow() =>
        new(
            [
                Row("mouse", "Mouse", 12, BatteryStatus.Discharging),
                Row("keyboard", "Keyboard", 6, BatteryStatus.Discharging),
                Row("phone", "Phone", 58, BatteryStatus.Discharging),
            ],
            BatteryWidgetOptions.Default with
            {
                Title = "Batteries",
            }
        );

    public static BatteryWidgetModel PanelCharging() =>
        new(
            [
                Row(
                    "phone",
                    "Phone",
                    54,
                    BatteryStatus.Charging,
                    charging: true,
                    trend: "+28%/30m"
                ),
                Row(
                    "mouse",
                    "Mouse",
                    91,
                    BatteryStatus.Charging,
                    charging: true,
                    timeToFull: "0:12"
                ),
                Row("laptop", "Laptop", 100, BatteryStatus.Full),
            ],
            BatteryWidgetOptions.Default with
            {
                Title = "Charging",
            }
        );

    public static BatteryWidgetModel PanelEmpty() =>
        new([], BatteryWidgetOptions.Default with { Title = "Batteries" });

    public static BatteryWidgetModel TileCharging() =>
        new(
            [Row("mouse", "Mouse", 82, BatteryStatus.Charging, charging: true, timeToFull: "0:35")],
            BatteryWidgetOptions.Default
        );

    public static BatteryWidgetModel TileDischarging() =>
        new([Row("phone", "Phone", 47, BatteryStatus.Discharging)], BatteryWidgetOptions.Default);

    public static BatteryWidgetModel TileLow() =>
        new([Row("mouse", "Mouse", 9, BatteryStatus.Discharging)], BatteryWidgetOptions.Default);

    private static BatteryWidgetRow Row(
        string id,
        string name,
        int percent,
        BatteryStatus status,
        bool charging = false,
        string? timeToFull = null,
        string? trend = null
    ) => new(id, name, percent, status, charging, Stale: false, timeToFull, trend);
}
