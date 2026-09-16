using System.Diagnostics;

namespace DeviceBatteryInfo.Sources.Bluetooth;

// Interface so this can be tested without shelling out to PowerShell
internal interface IPnpBatteryReader
{
    Task<string?> ReadRawAsync(string friendlyName, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListDevicesWithBatteryAsync(CancellationToken cancellationToken);
}

internal sealed class PowerShellPnpBatteryReader : IPnpBatteryReader
{
    // DEVPKEY_Bluetooth_Battery: the well-known key Windows fills for HFP/A2DP audio devices
    private const string BatteryPropertyKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private static readonly string PowerShellExecutablePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe"
    );

    private const string FriendlyNameEnvironmentVariable = "DEVICE_BATTERY_INFO_PNP_FRIENDLY_NAME";

    public async Task<string?> ReadRawAsync(
        string friendlyName,
        CancellationToken cancellationToken
    )
    {
        var script =
            $"(Get-PnpDevice -FriendlyName $env:{FriendlyNameEnvironmentVariable} | ForEach-Object {{ "
            + $"(Get-PnpDeviceProperty -InstanceId $_.PNPDeviceID -KeyName '{BatteryPropertyKey}').Data }})";

        var output = await RunAsync(
            script,
            cancellationToken,
            new Dictionary<string, string?> { [FriendlyNameEnvironmentVariable] = friendlyName }
        );
        return output.Length == 0 ? null : output;
    }

    public async Task<IReadOnlyList<string>> ListDevicesWithBatteryAsync(
        CancellationToken cancellationToken
    )
    {
        // Querying every PnP node one at a time takes ~100s; filtering to the Bluetooth enumerators
        // (BTHENUM/BTHLE*) and batching the property lookup brings it under a second.
        var script =
            $"$k='{BatteryPropertyKey}'; "
            + "$dev = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | "
            + "Where-Object { $_.InstanceId -like 'BTHENUM*' -or $_.InstanceId -like 'BTHHFENUM*' -or $_.InstanceId -like 'BTHLE*' }; "
            + "if ($dev) { "
            + "$name = @{}; foreach ($d in $dev) { $name[$d.InstanceId] = $d.FriendlyName } "
            + "Get-PnpDeviceProperty -InstanceId $dev.InstanceId -KeyName $k -ErrorAction SilentlyContinue | "
            + "Where-Object { $_.Data -ne $null -and \"$($_.Data)\" -ne '' } | "
            + "ForEach-Object { $name[$_.InstanceId] } | Where-Object { $_ } | Sort-Object -Unique }";

        var output = await RunAsync(script, cancellationToken);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<string> RunAsync(
        string script,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? environment = null
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellExecutablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script },
        };
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start powershell.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell exited with {process.ExitCode}: {stderr.Trim()}"
            );
        }

        return stdout.Trim();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already gone.
        }
    }
}
