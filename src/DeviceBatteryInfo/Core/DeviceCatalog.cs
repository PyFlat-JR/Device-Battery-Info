namespace DeviceBatteryInfo.Core;

public sealed class DeviceCatalog
{
    private readonly Lock _gate = new();
    private IReadOnlyList<BatterySlot> _devices = [];

    public event EventHandler? Changed;

    public IReadOnlyList<BatterySlot> Devices
    {
        get
        {
            lock (_gate)
            {
                return _devices;
            }
        }
    }

    public void Set(IReadOnlyList<BatterySlot> devices)
    {
        lock (_gate)
        {
            if (SameIds(_devices, devices))
            {
                _devices = devices;
                return;
            }

            _devices = devices;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool SameIds(IReadOnlyList<BatterySlot> a, IReadOnlyList<BatterySlot> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Id != b[i].Id || a[i].DisplayName != b[i].DisplayName)
            {
                return false;
            }
        }

        return true;
    }
}
