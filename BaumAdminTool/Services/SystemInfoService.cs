using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using BaumAdminTool.Models;
using Microsoft.Win32;

namespace BaumAdminTool.Services;

internal static class SystemInfoService
{
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint  dwLength;
        public uint  dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // ── Public API ─────────────────────────────────────────────────────────────

    public static async Task<SystemSnapshot> GetSnapshotAsync()
    {
        var cpuTask  = GetCpuInfoAsync();
        var gpuTask  = GetGpuNameAsync();
        var diskTask = Task.Run(GetDisks);
        var netTask  = Task.Run(GetNetworks);
        var blTask   = GetBitLockerStatusAsync();

        await Task.WhenAll(cpuTask, gpuTask, diskTask, netTask, blTask);

        var (cpuName, cpuCores, cpuLogical, cpuPct) = cpuTask.Result;
        var gpuName  = gpuTask.Result;
        var blStatus = blTask.Result;

        var disks = diskTask.Result.Select(d =>
        {
            var key = d.Letter.TrimEnd('\\', ':');
            return d with { BitLockerStatus = blStatus.GetValueOrDefault(key, "N/A") };
        }).ToList();

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);

        return new SystemSnapshot(
            HostName:      Environment.MachineName,
            UserName:      Environment.UserName,
            OsVersion:     GetOsVersion(),
            OsBuild:       GetOsBuild(),
            Uptime:        TimeSpan.FromMilliseconds(Environment.TickCount64),
            CpuName:       cpuName,
            CpuCores:      cpuCores,
            CpuLogical:    cpuLogical,
            CpuPercent:    cpuPct,
            RamTotalBytes: (long)mem.ullTotalPhys,
            RamUsedBytes:  (long)(mem.ullTotalPhys - mem.ullAvailPhys),
            GpuName:       gpuName,
            Disks:         disks,
            Networks:      netTask.Result);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    static async Task<string> GetGpuNameAsync()
    {
        // wmic is deprecated on Win11 — use CimInstance via PowerShell
        var output = await RunCommandAsync("powershell.exe",
            "-NoProfile -Command \"Get-CimInstance -ClassName Win32_VideoController" +
            " | Where-Object { $_.Name -notlike '*Basic*' -and $_.Name -notlike '*Microsoft*' }" +
            " | Select-Object -First 1 -ExpandProperty Name\"");
        var name = output.Trim();
        if (!string.IsNullOrEmpty(name)) return name;

        // Fallback: return first adapter including basic ones
        var fallback = await RunCommandAsync("powershell.exe",
            "-NoProfile -Command \"(Get-CimInstance -ClassName Win32_VideoController | Select-Object -First 1).Name\"");
        return fallback.Trim().Length > 0 ? fallback.Trim() : "N/A";
    }

    static async Task<(string name, int cores, int logical, double pct)> GetCpuInfoAsync()
    {
        string cpuName = "Unknown";
        try
        {
            using var key = Registry.LocalMachine
                .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpuName = (key?.GetValue("ProcessorNameString") as string ?? "Unknown").Trim();
        }
        catch { }

        var cimOut = await RunCommandAsync("powershell.exe",
            "-NoProfile -Command \"$c=Get-CimInstance Win32_Processor | Select-Object -First 1;" +
            " Write-Output \\\"Cores=$($c.NumberOfCores)\\\"; Write-Output \\\"Load=$($c.LoadPercentage)\\\"\"");
        int.TryParse(ParseWmicValue(cimOut, "Cores"), out int cores);
        double.TryParse(ParseWmicValue(cimOut, "Load"), out double pct);
        if (cores == 0) cores = Environment.ProcessorCount;

        return (cpuName, cores, Environment.ProcessorCount, pct);
    }

    static List<DiskInfo> GetDisks()
    {
        var list = new List<DiskInfo>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed) continue;
            try
            {
                list.Add(new DiskInfo(
                    Letter:          d.Name,
                    Label:           d.VolumeLabel.Length > 0 ? d.VolumeLabel : d.Name,
                    TotalBytes:      d.TotalSize,
                    FreeBytes:       d.TotalFreeSpace,
                    BitLockerStatus: "Checking..."));
            }
            catch { }
        }
        return list;
    }

    static List<NetworkInfo> GetNetworks()
    {
        var list = new List<NetworkInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = nic.GetIPProperties();
            var ipv4  = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 == null) continue;

            var dns = props.DnsAddresses
                .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.ToString()).ToArray();

            var gateway = props.GatewayAddresses
                .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .FirstOrDefault() ?? "N/A";

            var mac = string.Join("-", nic.GetPhysicalAddress().GetAddressBytes()
                .Select(b => b.ToString("X2")));

            list.Add(new NetworkInfo(nic.Name, ipv4.Address.ToString(),
                ipv4.IPv4Mask.ToString(), gateway, dns, mac));
        }
        return list;
    }

    static async Task<Dictionary<string, string>> GetBitLockerStatusAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ps = await RunCommandAsync("powershell.exe",
                "-NoProfile -Command \"Get-BitLockerVolume | Select-Object MountPoint,ProtectionStatus | ConvertTo-Csv -NoTypeInformation\"");

            foreach (var line in ps.Split('\n').Skip(1))
            {
                var trimmed = line.Trim().Trim('"');
                if (string.IsNullOrEmpty(trimmed)) continue;
                var parts = trimmed.Split("\",\"");
                if (parts.Length < 2) continue;
                var mount  = parts[0].Trim('"').TrimEnd('\\', ':').Trim();
                var status = parts[1].Trim('"') switch
                {
                    "On"  => "Encrypted",
                    "Off" => "Not Encrypted",
                    var s => s
                };
                if (!string.IsNullOrEmpty(mount))
                    result[mount] = status;
            }
        }
        catch { }
        return result;
    }

    static string GetOsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name    = key?.GetValue("ProductName")    as string ?? "Windows";
            var display = key?.GetValue("DisplayVersion") as string;
            return display != null ? $"{name} {display}" : name;
        }
        catch { return "Windows"; }
    }

    static string GetOsBuild()
    {
        try
        {
            using var key = Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            return key?.GetValue("CurrentBuildNumber") as string ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    internal static async Task<string> RunWmicAsync(string args)
        => await RunCommandAsync("wmic", args);

    internal static async Task<string> RunCommandAsync(string exe, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output;
        }
        catch { return ""; }
    }

    internal static string ParseWmicValue(string output, string key)
    {
        var prefix = key + "=";
        return output.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ?.Substring(prefix.Length).Trim() ?? "N/A";
    }
}
