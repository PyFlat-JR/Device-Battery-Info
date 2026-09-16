namespace DeviceBatteryInfo.Sources.Razer.DeathAdderV3Pro;

// Confirmed against a real DeathAdder V3 Pro only. The command class (0x07, "power") and command
// ids below are part of Razer's shared HID protocol and may work on other Razer mice, but don't
// assume it
// - see docs/adding-a-device.md before reusing this for a different model.
internal static class DeathAdderV3ProReportProtocol
{
    public const int ReportLength = 90;

    public const byte CommandBatteryLevel = 0x80;
    public const byte CommandChargingStatus = 0x84;

    private const int ChecksumFrom = 3;
    private const int ChecksumToExclusive = 88;
    private const int ChecksumIndex = 88;

    // Index into the raw response buffer (byte 0 is the report id, bytes 1.. are the Razer report),
    // so this is Razer argument 1 - the battery level / charging flag depending on the command.
    private const int ResponseValueIndex = 10;

    // Raw-buffer offsets for the frame header the device echoes back: status, then the command
    // class and id it is answering. Same +1 shift off the request layout as ResponseValueIndex.
    private const int StatusIndex = 1;
    private const int CommandClassIndex = 7;
    private const int CommandIdIndex = 8;

    private const byte StatusSuccessful = 0x02;
    private const byte CommandClassPower = 0x07;

    public static byte[] BuildRequest(byte commandId)
    {
        var report = new byte[ReportLength];
        report[1] = 0x1F;
        report[5] = 0x02;
        report[6] = 0x07;
        report[7] = commandId;

        byte checksum = 0;
        for (var i = ChecksumFrom; i < ChecksumToExclusive; i++)
        {
            checksum ^= report[i];
        }

        report[ChecksumIndex] = checksum;
        return report;
    }

    public static byte ReadResponseValue(ReadOnlySpan<byte> report) => report[ResponseValueIndex];

    // A dongle still mid-exchange with the mouse answers with a placeholder frame (zeroed payload,
    // no success status) instead of throwing - reading argument 1 off that is a spurious 0%, so the
    // transport must retry until this returns true rather than trust the first response.
    public static bool IsCompletedResponse(ReadOnlySpan<byte> report, byte commandId) =>
        report.Length > ResponseValueIndex
        && report[StatusIndex] == StatusSuccessful
        && report[CommandClassIndex] == CommandClassPower
        && report[CommandIdIndex] == commandId;

    public static byte ReadStatus(ReadOnlySpan<byte> report) =>
        report.Length > StatusIndex ? report[StatusIndex] : (byte)0;

    public static int PercentFromRaw(byte raw) =>
        (int)Math.Round(raw / 255.0 * 100.0, MidpointRounding.AwayFromZero);

    public static bool IsChargingFromRaw(byte raw) => raw == 1;
}
