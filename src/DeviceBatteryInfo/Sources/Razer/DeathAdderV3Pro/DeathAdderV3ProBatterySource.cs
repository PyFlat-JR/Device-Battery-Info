using System.Collections.Concurrent;
using DeviceBatteryInfo.Core;
using Serilog;

namespace DeviceBatteryInfo.Sources.Razer.DeathAdderV3Pro;

internal sealed class DeathAdderV3ProBatterySource(
    IRazerHidTransport transport,
    string devicePath,
    BatterySlot slot
) : IBatterySource
{
    private readonly IRazerHidTransport _transport = transport;
    private readonly string _devicePath = devicePath;

    public string Id { get; } = slot.Id;

    public string DisplayName { get; } = slot.DisplayName;

    public BatterySourceKind Kind { get; } =
        slot.Kind == BatterySourceKind.Other ? BatterySourceKind.Mouse : slot.Kind;

    public async ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken)
    {
        var rawLevel = await QueryAsync(
            _transport,
            _devicePath,
            DeathAdderV3ProReportProtocol.CommandBatteryLevel,
            cancellationToken
        );
        var rawCharging = await QueryAsync(
            _transport,
            _devicePath,
            DeathAdderV3ProReportProtocol.CommandChargingStatus,
            cancellationToken
        );

        var percent = DeathAdderV3ProReportProtocol.PercentFromRaw(rawLevel);
        var charging = DeathAdderV3ProReportProtocol.IsChargingFromRaw(rawCharging);

        return new BatteryReading
        {
            Percent = percent,
            Status =
                charging ? BatteryStatus.Charging
                : percent >= 100 ? BatteryStatus.Full
                : BatteryStatus.Discharging,
        };
    }

    internal static async Task<byte> QueryAsync(
        IRazerHidTransport transport,
        string devicePath,
        byte commandId,
        CancellationToken cancellationToken
    )
    {
        var response = await transport.ExchangeAsync(
            devicePath,
            DeathAdderV3ProReportProtocol.BuildRequest(commandId),
            response => DeathAdderV3ProReportProtocol.IsCompletedResponse(response, commandId),
            cancellationToken
        );
        return DeathAdderV3ProReportProtocol.ReadResponseValue(response);
    }
}

internal sealed class DeathAdderV3ProBatterySourceProvider(
    IRazerHidTransport transport,
    DeviceCatalog catalog,
    ILogger logger
) : IBatterySourceProvider
{
    private readonly IRazerHidTransport _transport = transport;
    private readonly DeviceCatalog _catalog = catalog;
    private readonly ILogger _logger = logger.ForContext<DeathAdderV3ProBatterySourceProvider>();

    // The HID path each configured device last answered on. A dongle keeps its path while plugged into
    // the same port, so this saves re-probing every interface on every poll.
    private readonly ConcurrentDictionary<string, string> _resolvedPaths = new(
        StringComparer.Ordinal
    );

    public async ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(
        CancellationToken cancellationToken
    )
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<IBatterySource>();
        }

        var sources = new List<IBatterySource>();

        // Group configured entries by the product they point at; each product is then matched against
        // its own connected units so a DeathAdder entry never claims a different model's dongle.
        var byProduct = _catalog
            .Devices.Where(d => d.Type == DeviceType.RazerDeathAdderV3Pro)
            .GroupBy(d =>
                (
                    VendorId: d.VendorId == 0 ? 0x1532 : d.VendorId,
                    ProductId: d.ProductId == 0 ? 0x00B7 : d.ProductId
                )
            );

        foreach (var product in byProduct)
        {
            var entries = product.OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
            var candidates = _transport.FindCandidates(
                product.Key.VendorId,
                product.Key.ProductId,
                interfaceNumber: null,
                DeathAdderV3ProReportProtocol.ReportLength
            );

            if (entries.Length == 1)
            {
                if (candidates.Count == 0)
                {
                    continue;
                }

                var path = await ResolvePathAsync(entries[0].Id, candidates, cancellationToken);
                sources.Add(new DeathAdderV3ProBatterySource(_transport, path, entries[0]));
                continue;
            }

            // Two or more entries for the same model: bind each to a distinct physical unit, in a
            // stable order, rather than letting every entry race for whichever unit answers first.
            var units = candidates
                .GroupBy(PhysicalUnitKey)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (IReadOnlyList<RazerHidCandidate>)[.. g])
                .ToArray();

            _logger.Information(
                "Razer {Product}: {EntryCount} configured, {UnitCount} unit(s) connected.",
                entries[0].DisplayName,
                entries.Length,
                units.Length
            );

            for (var i = 0; i < entries.Length && i < units.Length; i++)
            {
                var path = await ResolvePathAsync(entries[i].Id, units[i], cancellationToken);
                sources.Add(new DeathAdderV3ProBatterySource(_transport, path, entries[i]));
            }
        }

        return sources;
    }

    // A key that is the same for every HID collection of one physical mouse and different between two
    // identical mice. The USB serial is ideal; a wireless dongle that reports none falls back to the
    // parent-instance token embedded in the device path
    // (\\?\hid#vid_1532&pid_00b7&mi_00#8&1abcd&0&0000#{guid} -> "8&1abcd"), and an unparseable path
    // falls back to the whole path (each collection its own unit - the pre-grouping behaviour).
    private static string PhysicalUnitKey(RazerHidCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.SerialNumber))
        {
            return "s:" + candidate.SerialNumber.Trim().ToLowerInvariant();
        }

        var parts = candidate.Path.Split('#');
        if (parts.Length >= 4)
        {
            var instance = parts[2].Split('&');
            if (instance.Length >= 2)
            {
                return "i:" + string.Join('&', instance[0], instance[1]).ToLowerInvariant();
            }
        }

        return "p:" + candidate.Path.ToLowerInvariant();
    }

    private async Task<string> ResolvePathAsync(
        string deviceId,
        IReadOnlyList<RazerHidCandidate> candidates,
        CancellationToken cancellationToken
    )
    {
        if (
            _resolvedPaths.TryGetValue(deviceId, out var cached)
            && candidates.Any(c =>
                string.Equals(c.Path, cached, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return cached;
        }

        foreach (var candidate in candidates)
        {
            try
            {
                // A value coming back at all is the signal that this is the right interface: the
                // exchange only completes once the device echoes a finished battery frame, and any
                // 0-255 answer is plausible.
                _ = await DeathAdderV3ProBatterySource.QueryAsync(
                    _transport,
                    candidate.Path,
                    DeathAdderV3ProReportProtocol.CommandBatteryLevel,
                    cancellationToken
                );
                _resolvedPaths[deviceId] = candidate.Path;
                _logger.Information(
                    "Razer device {DeviceId} answered on interface {Interface} ({Product}).",
                    deviceId,
                    candidate.InterfaceNumber,
                    candidate.ProductName
                );
                return candidate.Path;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Debug(exception, "Razer candidate {Path} did not answer.", candidate.Path);
            }
        }

        // Nothing answered; keep the most likely path so the widget shows the device as stale rather
        // than hiding it, and try again next cycle.
        return candidates[0].Path;
    }
}
