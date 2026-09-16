using System.Collections.Concurrent;

namespace DeviceBatteryInfo.Core;

/// <summary>
/// Tracks how a device's charge level has moved recently, so a rate of change can be reported
/// without pinning it to a fixed unit. History is appended only from <see cref="BatteryRegistry.Changed"/>,
/// on the poll loop's thread; <see cref="GetTrend"/> is read concurrently from invocation scopes, so
/// each recorded step replaces the segment with a new immutable instance rather than mutating one in
/// place.
/// </summary>
public sealed class BatteryTrendTracker
{
    // Bounds how far back a rate can look, so a segment cannot grow unbounded in memory
    private static readonly TimeSpan MaxHistory = TimeSpan.FromHours(3);

    // Below this the delta is dominated by poll jitter rather than the device's actual drain, so
    // GetTrend withholds a reading rather than publish a noisy one.
    public static readonly TimeSpan MinWindow = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, Segment> _segments = new(StringComparer.Ordinal);

    public BatteryTrendTracker(BatteryRegistry registry) => registry.Changed += OnSnapshotChanged;

    private void OnSnapshotChanged(object? sender, BatterySnapshotChangedEventArgs e)
    {
        if (e.Current is not { } current)
        {
            if (e.Previous is { } removed)
            {
                _segments.TryRemove(removed.Id, out _);
            }

            return;
        }

        if (current.Reading.Percent is not { } percent)
        {
            return;
        }

        _segments.AddOrUpdate(
            current.Id,
            _ => Segment.Start(current.Reading.IsCharging, current.UpdatedUtc, percent),
            (_, existing) =>
                existing.Record(current.Reading.IsCharging, current.UpdatedUtc, percent, MaxHistory)
        );
    }

    /// <summary>The change over whatever window of history is available for the device's current
    /// charging segment, or null when there is not yet enough of it to say anything. A charge-state
    /// flip (discharging to charging or back) starts a fresh segment, so the two rates never mix.</summary>
    public BatteryTrend? GetTrend(BatterySnapshot snapshot)
    {
        if (
            snapshot.Reading.Percent is not { } percent
            || !_segments.TryGetValue(snapshot.Id, out var segment)
            || segment.IsCharging != snapshot.Reading.IsCharging
        )
        {
            return null;
        }

        var window = snapshot.UpdatedUtc - segment.Oldest.Timestamp;
        return window < MinWindow
            ? null
            : new BatteryTrend(
                percent - segment.Oldest.Percent,
                window,
                snapshot.Reading.IsCharging
            );
    }

    private readonly record struct Sample(DateTimeOffset Timestamp, int Percent);

    private sealed record Segment(bool IsCharging, Sample[] Samples)
    {
        public Sample Oldest => Samples[0];

        public static Segment Start(bool isCharging, DateTimeOffset timestamp, int percent) =>
            new(isCharging, [new Sample(timestamp, percent)]);

        public Segment Record(
            bool isCharging,
            DateTimeOffset timestamp,
            int percent,
            TimeSpan maxHistory
        )
        {
            if (isCharging != IsCharging)
            {
                return Start(isCharging, timestamp, percent);
            }

            var samples =
                Samples[^1].Percent == percent
                    ? Samples
                    : [.. Samples, new Sample(timestamp, percent)];

            var cutoff = timestamp - maxHistory;
            var trimStart = 0;
            while (trimStart < samples.Length - 1 && samples[trimStart].Timestamp < cutoff)
            {
                trimStart++;
            }

            return new Segment(isCharging, trimStart == 0 ? samples : samples[trimStart..]);
        }
    }
}

public sealed record BatteryTrend(int DeltaPercent, TimeSpan Window, bool IsCharging);
