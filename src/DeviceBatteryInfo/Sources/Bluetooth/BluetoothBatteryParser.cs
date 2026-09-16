namespace DeviceBatteryInfo.Sources.Bluetooth;

internal static class BluetoothBatteryParser
{
    public static int? ParsePercent(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var digits = new string([.. rawValue.Where(char.IsDigit)]);
        if (!int.TryParse(digits, out var value) || value < 0 || value > 100)
        {
            return null;
        }

        return value;
    }
}
