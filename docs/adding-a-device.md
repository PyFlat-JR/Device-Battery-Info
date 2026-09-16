# Adding a device

Device Battery Info has three generic backends (This PC, an Android phone over adb, a Windows
Bluetooth device) plus a growing **catalog** of specific products under "Other devices" - each catalog
entry is confirmed against the exact hardware it names, never a whole brand or category (see
`ConfigFlow/DeviceModelCatalog.cs` for what is in the catalog today). To support a device none of
these cover, add a **battery source provider**.

## The contract

Two small interfaces in `src/DeviceBatteryInfo/Core/`:

```csharp
public interface IBatterySource
{
    string Id { get; }            // stable, lowercase kebab-case, unique. It is the variable prefix:
                                  // battery_<Id>_percent and so on. Treat it as a public API.
    string DisplayName { get; }
    BatterySourceKind Kind { get; }
    ValueTask<BatteryReading> ReadAsync(CancellationToken cancellationToken);
}

public interface IBatterySourceProvider
{
    // Called every poll cycle. Cheap. Returns an empty list for the normal "nothing connected" case,
    // never throws for it.
    ValueTask<IReadOnlyList<IBatterySource>> DiscoverAsync(CancellationToken cancellationToken);
}
```

`BatteryReading` carries `Percent` (`int?`, null when unknown), `Status`
(`Unknown`/`Discharging`/`Charging`/`Full`), and optional `TimeToFull` / `TimeToEmpty`. Return
`BatteryReading.Unavailable` when the device is reachable but has no number to give; **throw** when
the read itself failed (the poll loop turns repeated throws into a stale, dimmed reading rather than
a wrong one).

## Steps

1. Add a folder under `src/DeviceBatteryInfo/Sources/<YourDevice>/`. If the device shares
   infrastructure with an existing backend (another Razer HID device, say), reuse that backend's
   shared utilities instead of copying them - `Sources/Razer/` holds the generic HID transport and
   interop, and `Sources/Razer/DeathAdderV3Pro/` holds only what is specific to that one confirmed
   model; a second Razer device gets its own `Sources/Razer/<Model>/` folder next to it, not a fork
   of the transport.
2. Put the wire/parse logic in a **pure static** class (no I/O) so it is unit-testable, and the
   actual I/O behind a small interface with a real implementation, mirroring `Sources/Adb/`
   (`AdbBatteryParser` + `IAdbCommandRunner`).
3. Implement `IBatterySource` and an `IBatterySourceProvider` that discovers it. Gate discovery on
   `OperatingSystem.IsWindows()`, and have it read the configured device set from `DeviceCatalog`
   (see the DeathAdder V3 Pro provider for the pattern) rather than adding an options flag - there is
   no default/seeded device, and no `appsettings.json`: a fresh install shows nothing until the user
   adds a device through the config flow.
4. If the device needs configuration (an address, an id, a name), add config-flow fields for it (see
   step 6) - device configuration lives entirely in the config-flow entries read by
   `DeviceEntryReader`, not in `BatteryPluginOptions` (that type only holds plugin-wide settings like
   the poll interval).
5. Register the provider in `Sources/BatterySourceRegistration.cs`:
   `services.AddSingleton<IBatterySourceProvider, YourProvider>();`
6. Make the device selectable in the config flow. It belongs under **Other devices** (not a
   top-level category), so add an entry to `ConfigFlow/DeviceModelCatalog.cs` with its brand,
   model, the backend `DeviceType` it maps to and, for a USB device, its `VendorId` / `ProductId`
   (the flow reads them from here, so the user is never asked). Reuse an existing `DeviceType` when
   the details it needs already exist; if the entry fully describes its backend it completes straight
   from the model step (see `DeviceModelCatalog.NeedsDetailsStep`). A genuinely new backend that
   still needs user input also needs a new `DeviceType` value plus matching cases in
   `DeviceConfigFlow.BuildDetailsStepAsync` / `ValidateDetails` / `Complete`, a `NeedsDetailsStep`
   entry, and a branch in `DeviceEntryReader`.

   There is deliberately no "enter your own USB ids" path in the UI: a raw vendor/product id is not
   enough to talk to a device (the DeathAdder V3 Pro, for instance, needs its whole HID feature-report
   protocol), so an unlisted device is a catalog entry plus the source above, not a config option.
7. Add tests next to the others (`tests/DeviceBatteryInfo.Tests/BatterySourceParsingTests.cs`
   and `DeviceConfigFlowTests.cs` are the patterns), then `dotnet build && dotnet test`.

Nothing else changes: the poll loop discovers your provider automatically, the registry stores its
readings, and the variable catalogue creates `battery_<Id>_*` from any source it sees, because
`BatterySlots.Reserve`/`Slug` (used by `DeviceEntryReader`) is the one place a device id is derived
from its name. (A JSON descriptor format for simple HID devices, so this needs no C#, is a planned
follow-up.)
