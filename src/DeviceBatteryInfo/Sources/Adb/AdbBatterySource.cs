using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.Adb;

internal sealed class AdbBatterySource(IAdbCommandRunner runner, BatterySlot slot) : IBatterySource
{
    private readonly IAdbCommandRunner _runner = runner;
    private readonly string _executable = string.IsNullOrWhiteSpace(slot.AdbExecutable)
        ? "adb"
        : slot.AdbExecutable!;
    private readonly string _address = slot.AdbAddress!;

    public string Id { get; } = slot.Id;

    public string DisplayName { get; } = slot.DisplayName;

    public BatterySourceKind Kind => BatterySourceKind.Phone;

    public async ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken)
    {
        await _runner.RunAsync(_executable, ["connect", _address], cancellationToken);
        var output = await _runner.RunAsync(
            _executable,
            ["-s", _address, "shell", "dumpsys", "battery"],
            cancellationToken
        );
        return AdbBatteryParser.Parse(output);
    }
}

internal sealed class AdbBatterySourceProvider(IAdbCommandRunner runner, DeviceCatalog catalog)
    : IBatterySourceProvider
{
    private readonly IAdbCommandRunner _runner = runner;
    private readonly DeviceCatalog _catalog = catalog;

    public ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        var sources = _catalog
            .Devices.Where(d =>
                d.Type == DeviceType.AdbPhone && !string.IsNullOrWhiteSpace(d.AdbAddress)
            )
            .Select(IBatterySource (d) => new AdbBatterySource(_runner, d))
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<IBatterySource>>(sources);
    }
}
