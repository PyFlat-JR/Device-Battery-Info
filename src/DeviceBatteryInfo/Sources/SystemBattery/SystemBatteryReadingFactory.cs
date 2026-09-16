using DeviceBatteryInfo.Core;

namespace DeviceBatteryInfo.Sources.SystemBattery;

internal static class SystemBatteryReadingFactory
{
	public static bool HasBattery(in NativePowerStatus status) =>
		status.BatteryFlag != NativePowerStatus.FlagNoBattery
		&& status.BatteryFlag != NativePowerStatus.FlagUnknown
		&& status.BatteryLifePercent != NativePowerStatus.PercentUnknown;

	public static BatteryReading Create(in NativePowerStatus status)
	{
		int? percent = status.BatteryLifePercent == NativePowerStatus.PercentUnknown
			? null
			: Math.Clamp((int)status.BatteryLifePercent, 0, 100);

		BatteryStatus batteryStatus;
		if ((status.BatteryFlag & NativePowerStatus.FlagCharging) != 0)
		{
			batteryStatus = BatteryStatus.Charging;
		}
		else if (status.AcLineStatus == NativePowerStatus.AcOnline)
		{
			batteryStatus = percent is >= 100 ? BatteryStatus.Full : BatteryStatus.Charging;
		}
		else if (status.AcLineStatus == NativePowerStatus.AcOffline)
		{
			batteryStatus = BatteryStatus.Discharging;
		}
		else
		{
			batteryStatus = BatteryStatus.Unknown;
		}

		TimeSpan? timeToEmpty = batteryStatus == BatteryStatus.Discharging && status.BatteryLifeTime >= 0
			? TimeSpan.FromSeconds(status.BatteryLifeTime)
			: null;

		return new BatteryReading
		{
			Percent = percent,
			Status = batteryStatus,
			TimeToEmpty = timeToEmpty,
			// The Win32 API exposes no charge-rate field, so time-to-full is left for a later
			// IOCTL_BATTERY_QUERY_STATUS / WMI implementation.
			TimeToFull = null,
		};
	}
}
