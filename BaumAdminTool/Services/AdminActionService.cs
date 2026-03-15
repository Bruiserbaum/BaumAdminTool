using System.Diagnostics;

namespace BaumAdminTool.Services;

internal static class AdminActionService
{
    // ── Core runner ─────────────────────────────────────────────────────────

    public static async Task RunAsync(
        string         exe,
        string         args,
        Action<string> onOutput,
        Action<string> onError,
        Action<int>    onComplete)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) onOutput(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) onError(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        onComplete(proc.ExitCode);
    }

    static Task RunPs(string cmd, Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -Command \"{cmd.Replace("\"", "\\\"")}\"",
            o, e, c);

    // ── Network ─────────────────────────────────────────────────────────────

    public static Task FlushDnsAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("ipconfig.exe", "/flushdns", o, e, c);

    public static Task ResetWinsockAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("netsh.exe", "winsock reset", o, e, c);

    public static Task ResetTcpIpAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("netsh.exe", "int ip reset", o, e, c);

    public static Task ReleaseRenewIpAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("ipconfig /release; Start-Sleep 1; ipconfig /renew; Write-Output 'IP Released and Renewed'", o, e, c);

    public static Task PingTestAsync(string target, Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("ping.exe", $"-n 4 {target}", o, e, c);

    // ── System ──────────────────────────────────────────────────────────────

    public static Task RunSfcAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("sfc.exe", "/scannow", o, e, c);

    public static Task RunDismAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", o, e, c);

    public static Task ClearTempAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Remove-Item -Path $env:TEMP\\* -Recurse -Force -ErrorAction SilentlyContinue; Write-Output 'Temp files cleared'", o, e, c);

    public static Task EmptyRecycleBinAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Clear-RecycleBin -Force -ErrorAction SilentlyContinue; Write-Output 'Recycle bin emptied'", o, e, c);

    public static Task CheckDiskAsync(string driveLetter, Action<string> o, Action<string> e, Action<int> c)
        => RunPs($"Repair-Volume -DriveLetter {driveLetter.Trim(':', '\\')} -Scan; Write-Output 'Disk scan complete'", o, e, c);

    // ── Windows ─────────────────────────────────────────────────────────────

    public static Task GpUpdateAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunAsync("gpupdate.exe", "/force", o, e, c);

    public static Task RestartExplorerAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Stop-Process -Name explorer -Force; Start-Sleep 1; Start-Process explorer; Write-Output 'Explorer restarted'", o, e, c);

    public static Task RestartSpoolerAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Restart-Service -Name Spooler -Force; Write-Output 'Print Spooler restarted'", o, e, c);

    public static Task RestartWlanAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Restart-Service -Name Wlansvc -Force -ErrorAction SilentlyContinue; Write-Output 'WLAN AutoConfig restarted'", o, e, c);

    // ── Registry fixes ──────────────────────────────────────────────────────

    public static Task ClearRecentFilesAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Remove-Item 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RecentDocs' -Recurse -ErrorAction SilentlyContinue; Write-Output 'Recent files registry cleared'", o, e, c);

    public static Task ClearRunMruAsync(Action<string> o, Action<string> e, Action<int> c)
        => RunPs("Remove-Item 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\RunMRU' -Recurse -ErrorAction SilentlyContinue; Write-Output 'Run MRU cleared'", o, e, c);

    public static Task DisableStartupEntryAsync(string name, Action<string> o, Action<string> e, Action<int> c)
        => RunPs($"Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name '{name}' -ErrorAction SilentlyContinue; Write-Output 'Startup entry removed: {name}'", o, e, c);

    // ── RoboCopy backup ─────────────────────────────────────────────────────

    public static Task RoboCopyAsync(
        string         source,
        string         dest,
        bool           mirror,
        Action<string> onOutput,
        Action<string> onError,
        Action<int>    onComplete)
    {
        var flags = mirror ? "/MIR" : "/E";
        return RunAsync("robocopy.exe",
            $"\"{source}\" \"{dest}\" {flags} /R:2 /W:1 /NP /NDL",
            onOutput, onError, onComplete);
    }
}
