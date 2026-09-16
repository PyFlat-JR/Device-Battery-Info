namespace DeviceBatteryInfo.Core;

public sealed class BatteryPluginOptions
{
    public const string SectionName = "Battery";

    // Floored at 10 by BatteryPollingService: a read can mean spawning adb or PowerShell.
    public int PollIntervalSeconds { get; set; } = 10;

    public int StaleAfterFailures { get; set; } = 3;

    public int ReadTimeoutSeconds { get; set; } = 10;
}
