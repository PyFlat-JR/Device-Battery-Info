using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.Adb;

internal static class AdbBatteryParser
{
    public static BatteryReading Parse(string dumpsysOutput)
    {
        int? level = null;
        int? scale = null;
        var status = BatteryStatus.Unknown;

        foreach (var raw in dumpsysOutput.Split('\n'))
        {
            var line = raw.Trim();
            if (TryValue(line, "level:", out var levelValue))
            {
                level = levelValue;
            }
            else if (TryValue(line, "scale:", out var scaleValue))
            {
                scale = scaleValue;
            }
            else if (TryValue(line, "status:", out var statusValue))
            {
                // Android BatteryManager.BATTERY_STATUS_* codes
                status = statusValue switch
                {
                    2 => BatteryStatus.Charging,
                    3 => BatteryStatus.Discharging,
                    4 => BatteryStatus.Discharging,
                    5 => BatteryStatus.Full,
                    _ => BatteryStatus.Unknown,
                };
            }
        }

        if (level is null)
        {
            return BatteryReading.Unavailable;
        }

        var percent = scale is > 0 and not 100
            ? (int)Math.Round(level.Value * 100.0 / scale.Value)
            : level.Value;

        return new BatteryReading { Percent = Math.Clamp(percent, 0, 100), Status = status };
    }

    private static bool TryValue(string line, string key, out int value)
    {
        value = 0;
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(line[key.Length..].Trim(), out value);
    }
}
