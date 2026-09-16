using DeviceBatteryInfo.ConfigFlow;
using DeviceBatteryInfo.Core;
using DeviceBatteryInfo.Sources.Adb;
using DeviceBatteryInfo.Sources.Bluetooth;
using DeviceBatteryInfo.Sources.Razer;
using DeviceBatteryInfo.Sources.Razer.DeathAdderV3Pro;
using DeviceBatteryInfo.Sources.SystemBattery;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceBatteryInfo.Sources;

internal static class BatterySourceRegistration
{
    public static IServiceCollection AddBatterySources(this IServiceCollection services)
    {
        services.AddSingleton<IAdbCommandRunner, ProcessAdbCommandRunner>();
        services.AddSingleton<IRazerHidTransport, HidSharpRazerTransport>();
        services.AddSingleton<IPnpBatteryReader, PowerShellPnpBatteryReader>();
        services.AddSingleton<IDeviceDiscovery, WindowsDeviceDiscovery>();

        services.AddSingleton<IBatterySourceProvider, SystemBatterySourceProvider>();
        services.AddSingleton<IBatterySourceProvider, DeathAdderV3ProBatterySourceProvider>();
        services.AddSingleton<IBatterySourceProvider, AdbBatterySourceProvider>();
        services.AddSingleton<IBatterySourceProvider, BluetoothBatterySourceProvider>();

        return services;
    }
}
