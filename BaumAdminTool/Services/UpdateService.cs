using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BaumAdminTool.Services;

internal static class UpdateService
{
    private const string Owner = "Bruiserbaum";
    private const string Repo  = "BaumAdminTool";

    public record UpdateInfo(string Tag, Version Latest, string DownloadUrl);

    public static Version CurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        return new Version(v.Major, v.Minor, v.Build);
    }

    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "BaumAdminTool");
        http.Timeout = TimeSpan.FromSeconds(15);

        string json;
        try
        {
            json = await http.GetStringAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");
        }
        catch { return null; }

        var tagMatch = Regex.Match(json, @"""tag_name""\s*:\s*""v?([^""]+)""");
        var urlMatch = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]*Setup[^""]*.exe)""");

        if (!tagMatch.Success) return null;

        if (!Version.TryParse(tagMatch.Groups[1].Value, out var latest)) return null;
        var url = urlMatch.Success ? urlMatch.Groups[1].Value : string.Empty;

        return new UpdateInfo(tagMatch.Groups[1].Value, latest, url);
    }

    public static async Task ApplyAsync(string downloadUrl, IProgress<int> progress)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"BaumAdminTool-Update-{Guid.NewGuid():N}.exe");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "BaumAdminTool");
        using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        using var fs     = File.Create(tempFile);
        using var stream = await response.Content.ReadAsStreamAsync();

        var buf  = new byte[81920];
        long got = 0;
        int  n;
        while ((n = await stream.ReadAsync(buf)) > 0)
        {
            await fs.WriteAsync(buf.AsMemory(0, n));
            got += n;
            if (total > 0) progress.Report((int)(got * 100 / total));
        }
        fs.Close();

        // Launch installer silently; it will restart the app via /RESTARTAPPLICATIONS
        Process.Start(new ProcessStartInfo(tempFile,
            "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS")
        {
            UseShellExecute = true,
        });

        Application.Exit();
    }
}
