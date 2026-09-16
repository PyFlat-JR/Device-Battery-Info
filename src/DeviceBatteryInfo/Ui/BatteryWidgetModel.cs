using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Ui;

internal sealed record BatteryWidgetModel(
    IReadOnlyList<BatteryWidgetRow> Rows,
    BatteryWidgetOptions Options
);

internal sealed record BatteryWidgetRow(
    string Id,
    string Name,
    int? Percent,
    BatteryStatus Status,
    bool Charging,
    bool Stale,
    string? TimeToFull,
    string? Trend = null
)
{
    public string Color(int lowThreshold)
    {
        if (Stale || Percent is null)
        {
            return "#8A8A8A";
        }

        if (Charging)
        {
            return "#A97BE0";
        }

        return Percent switch
        {
            <= 0 => "#8A8A8A",
            var p when p <= lowThreshold => "#E5533D",
            < 50 => "#E8A13C",
            < 80 => "#3FB669",
            _ => "#4C9BE8",
        };
    }

    public string PercentText() => Percent is { } p ? $"{p}%" : "--";
}

internal enum BatterySortMode
{
    Manual,

    LowestFirst,

    Alphabetical,

    ChargingFirst,
}

internal sealed record BatteryWidgetOptions(
    IReadOnlyList<string> SourceIds,
    bool ShowBar,
    bool ShowPercent,
    bool ShowCharging,
    bool ShowTimeToFull,
    bool ShowTrend,
    int LowThreshold,
    BatterySortMode Sort = BatterySortMode.Manual,
    string Title = ""
)
{
    public static readonly BatteryWidgetOptions Default = new(
        [],
        ShowBar: true,
        ShowPercent: true,
        ShowCharging: true,
        ShowTimeToFull: true,
        ShowTrend: true,
        LowThreshold: 20
    );

    public const string SortManual = "manual";
    public const string SortLowestFirst = "lowest-first";
    public const string SortAlphabetical = "alphabetical";
    public const string SortChargingFirst = "charging-first";

    public static BatterySortMode ParseSort(string? value) =>
        value switch
        {
            SortLowestFirst => BatterySortMode.LowestFirst,
            SortAlphabetical => BatterySortMode.Alphabetical,
            SortChargingFirst => BatterySortMode.ChargingFirst,
            _ => BatterySortMode.Manual,
        };

    public static string SortValue(BatterySortMode mode) =>
        mode switch
        {
            BatterySortMode.LowestFirst => SortLowestFirst,
            BatterySortMode.Alphabetical => SortAlphabetical,
            BatterySortMode.ChargingFirst => SortChargingFirst,
            _ => SortManual,
        };

    public IReadOnlyList<BatteryWidgetRow> Order(IReadOnlyList<BatteryWidgetRow> rows) =>
        Sort switch
        {
            BatterySortMode.LowestFirst => rows.OrderBy(r => r.Stale || r.Percent is null ? 1 : 0)
                .ThenBy(r => r.Stale || r.Percent is null ? int.MaxValue : r.Percent!.Value)
                .ToArray(),
            BatterySortMode.Alphabetical => rows.OrderBy(
                    r => r.Name,
                    StringComparer.CurrentCultureIgnoreCase
                )
                .ToArray(),
            BatterySortMode.ChargingFirst => rows.OrderBy(r => r.Charging ? 0 : 1).ToArray(),
            _ => rows,
        };
}
