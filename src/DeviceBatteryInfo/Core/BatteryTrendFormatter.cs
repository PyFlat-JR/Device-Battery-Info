namespace DeviceBatteryInfo.Core;

/// <summary>Turns a <see cref="BatteryTrend"/> into the two shapes callers need: a short display
/// string, and a normalized rate for automations. Below a 1h window the display string shows the
/// raw, non-extrapolated delta over whatever window is actually available (so "30m" and "45m" both
/// occur depending on the device); from 1h onward the tracked window keeps growing (up to its
/// bounded history), so the display string switches to the normalized percent-per-hour rate
/// instead of stretching out to e.g. "-15%/3h".</summary>
internal static class BatteryTrendFormatter
{
    private static readonly TimeSpan NormalizeFrom = TimeSpan.FromHours(1);

    public static string? FormatText(BatteryTrend? trend)
    {
        if (trend is not { DeltaPercent: not 0 } t)
        {
            return null;
        }

        if (t.Window < NormalizeFrom)
        {
            return FormatSigned(t.DeltaPercent, FormatWindow(t.Window));
        }

        var perHour = (int)Math.Round(t.DeltaPercent / t.Window.TotalHours, MidpointRounding.AwayFromZero);
        return perHour == 0 ? null : FormatSigned(perHour, "1h");
    }

    public static double? PercentPerHour(BatteryTrend? trend) =>
        trend is { DeltaPercent: not 0 } t
            ? Math.Round(t.DeltaPercent / t.Window.TotalHours, 1)
            : null;

    private static string FormatSigned(int value, string window) =>
        $"{(value > 0 ? "+" : "-")}{Math.Abs(value)}%/{window}";

    private static string FormatWindow(TimeSpan window)
    {
        var minutes = window.TotalMinutes;
        var step = minutes switch
        {
            < 10 => 1,
            < 60 => 5,
            _ => 15,
        };
        var rounded = Math.Max(step, (int)Math.Round(minutes / step) * step);

        if (rounded < 60)
        {
            return $"{rounded}m";
        }

        var hours = rounded / 60;
        var remainderMinutes = rounded % 60;
        return remainderMinutes == 0 ? $"{hours}h" : $"{hours}h{remainderMinutes}m";
    }
}
