namespace BaumAdminTool.Models;

internal record DiskInfo(
    string Letter,
    string Label,
    long   TotalBytes,
    long   FreeBytes,
    string BitLockerStatus)
{
    public double UsedPct  => TotalBytes > 0 ? (double)(TotalBytes - FreeBytes) / TotalBytes * 100 : 0;
    public string TotalGb  => $"{TotalBytes / 1_073_741_824.0:F1} GB";
    public string FreeGb   => $"{FreeBytes  / 1_073_741_824.0:F1} GB";
    public string UsedGb   => $"{(TotalBytes - FreeBytes) / 1_073_741_824.0:F1} GB";
}

internal record NetworkInfo(
    string   AdapterName,
    string   IpAddress,
    string   SubnetMask,
    string   Gateway,
    string[] DnsServers,
    string   MacAddress);

internal record ProcessEntry(
    int    Pid,
    string Name,
    double CpuPercent,
    long   MemoryBytes)
{
    public string MemoryMb => $"{MemoryBytes / 1_048_576.0:F0} MB";
    public string CpuStr   => $"{CpuPercent:F1}%";
}

internal record SystemSnapshot(
    string            HostName,
    string            UserName,
    string            OsVersion,
    string            OsBuild,
    TimeSpan          Uptime,
    string            CpuName,
    int               CpuCores,
    int               CpuLogical,
    double            CpuPercent,
    long              RamTotalBytes,
    long              RamUsedBytes,
    string            GpuName,
    List<DiskInfo>    Disks,
    List<NetworkInfo> Networks)
{
    public double RamPct     => RamTotalBytes > 0 ? (double)RamUsedBytes / RamTotalBytes * 100 : 0;
    public string RamTotalGb => $"{RamTotalBytes / 1_073_741_824.0:F1} GB";
    public string RamUsedGb  => $"{RamUsedBytes  / 1_073_741_824.0:F1} GB";
    public string UptimeStr  =>
        $"{(int)Uptime.TotalDays}d {Uptime.Hours}h {Uptime.Minutes}m";
}
