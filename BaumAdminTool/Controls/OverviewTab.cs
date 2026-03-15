using BaumAdminTool.Models;
using BaumAdminTool.Services;

namespace BaumAdminTool.Controls;

internal sealed class OverviewTab : Panel
{
    private readonly Panel  _cards;
    private readonly Button _refreshBtn;
    private readonly Label  _statusLabel;
    private bool            _loading;

    public OverviewTab()
    {
        BackColor = AppTheme.BgMain;
        Dock      = DockStyle.Fill;

        // Toolbar
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 42,
            BackColor = AppTheme.BgPanel,
            Padding   = new Padding(8, 6, 8, 6),
        };

        _refreshBtn = AppTheme.MakeButton("⟳  Refresh");
        _refreshBtn.Size     = new Size(100, 28);
        _refreshBtn.Location = new Point(8, 7);
        _refreshBtn.Click   += async (_, _) => await LoadAsync();

        _statusLabel = AppTheme.MakeLabel("", AppTheme.FontSmall, AppTheme.TextMuted);
        _statusLabel.Location = new Point(120, 13);
        toolbar.Controls.AddRange(new Control[] { _refreshBtn, _statusLabel });

        // Scrollable card area
        _cards = new Panel
        {
            Dock          = DockStyle.Fill,
            AutoScroll    = true,
            BackColor     = AppTheme.BgMain,
            Padding       = new Padding(12),
        };
        AppTheme.ApplyDarkScrollBar(_cards);

        Controls.Add(_cards);
        Controls.Add(toolbar);

        Load(async () => await LoadAsync());
    }

    static void Load(Func<Task> fn) => Task.Run(fn);

    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        SafeSet(_refreshBtn, b => b.Enabled = false);
        SafeSet(_statusLabel, l => l.Text    = "Loading...");

        try
        {
            var snap = await SystemInfoService.GetSnapshotAsync();
            if (IsDisposed) return;
            Invoke(() => BuildCards(snap));
            SafeSet(_statusLabel, l => l.Text = $"Last updated {DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            SafeSet(_statusLabel, l => l.Text = $"Error: {ex.Message}");
        }
        finally
        {
            _loading = false;
            SafeSet(_refreshBtn, b => b.Enabled = true);
        }
    }

    void BuildCards(SystemSnapshot s)
    {
        _cards.SuspendLayout();
        _cards.Controls.Clear();

        int pad  = 12;
        int col1 = 0;
        int col2 = 0;
        int cardW = (_cards.ClientSize.Width - pad * 3) / 2;
        if (cardW < 200) cardW = 200;

        void AddCard(Panel card, int col, ref int yRef)
        {
            card.Left = pad + col * (cardW + pad);
            card.Top  = pad + yRef;
            card.Width = cardW;
            _cards.Controls.Add(card);
            yRef += card.Height + pad;
        }

        AddCard(MakeMachineCard(s),  0, ref col1);
        AddCard(MakeCpuRamCard(s),   0, ref col1);
        foreach (var disk in s.Disks)
            AddCard(MakeDiskCard(disk), 0, ref col1);

        AddCard(MakeNetworkCard(s),  1, ref col2);
        AddCard(MakeGpuCard(s),      1, ref col2);

        // Track content height so AutoScroll works
        int totalH = Math.Max(col1, col2) + pad;
        _cards.AutoScrollMinSize = new Size(0, totalH);

        _cards.ResumeLayout(true);
    }

    // ── Card factories ──────────────────────────────────────────────────────

    Panel MakeMachineCard(SystemSnapshot s)
    {
        var rows = new[]
        {
            ("Host",    s.HostName),
            ("User",    s.UserName),
            ("OS",      s.OsVersion),
            ("Build",   s.OsBuild),
            ("Uptime",  s.UptimeStr),
        };
        return MakeInfoCard("Machine", rows);
    }

    Panel MakeCpuRamCard(SystemSnapshot s)
    {
        var rows = new[]
        {
            ("CPU",      s.CpuName),
            ("Cores",    $"{s.CpuCores} physical / {s.CpuLogical} logical"),
            ("CPU Load", $"{s.CpuPercent:F0}%"),
            ("RAM",      $"{s.RamUsedGb} used / {s.RamTotalGb} total"),
            ("RAM %",    $"{s.RamPct:F0}%"),
        };
        var card = MakeInfoCard("CPU & Memory", rows);

        // RAM bar
        var bar = MakeBar(s.RamPct, AppTheme.Accent);
        bar.Location = new Point(12, card.Height - 22);
        card.Controls.Add(bar);
        card.Height += 10;
        return card;
    }

    Panel MakeDiskCard(DiskInfo d)
    {
        var rows = new[]
        {
            ("Drive",     $"{d.Letter} — {d.Label}"),
            ("Total",     d.TotalGb),
            ("Used",      $"{d.UsedGb} ({d.UsedPct:F0}%)"),
            ("Free",      d.FreeGb),
            ("BitLocker", d.BitLockerStatus),
        };
        var color = d.UsedPct > 90 ? AppTheme.Danger
                  : d.UsedPct > 70 ? AppTheme.Warning
                  : AppTheme.Success;
        var card = MakeInfoCard($"Disk  {d.Letter}", rows);
        var bar  = MakeBar(d.UsedPct, color);
        bar.Location = new Point(12, card.Height - 22);
        card.Controls.Add(bar);
        card.Height += 10;
        return card;
    }

    Panel MakeNetworkCard(SystemSnapshot s)
    {
        if (s.Networks.Count == 0)
            return MakeInfoCard("Network", new[] { ("Status", "No active adapters") });

        var net  = s.Networks[0];
        var rows = new[]
        {
            ("Adapter", net.AdapterName),
            ("IP",      net.IpAddress),
            ("Subnet",  net.SubnetMask),
            ("Gateway", net.Gateway),
            ("DNS",     net.DnsServers.Length > 0 ? string.Join(", ", net.DnsServers) : "N/A"),
            ("MAC",     net.MacAddress),
        };
        var card = MakeInfoCard("Network", rows);

        // Extra adapters (brief)
        foreach (var extra in s.Networks.Skip(1))
        {
            var lbl = AppTheme.MakeLabel($"  + {extra.AdapterName}  {extra.IpAddress}",
                AppTheme.FontSmall, AppTheme.TextMuted);
            lbl.Location = new Point(12, card.Height - 4);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);
            card.Height += 18;
        }
        return card;
    }

    Panel MakeGpuCard(SystemSnapshot s)
    {
        var rows = new[] { ("GPU", s.GpuName) };
        return MakeInfoCard("GPU", rows);
    }

    // ── Shared helpers ──────────────────────────────────────────────────────

    static Panel MakeInfoCard(string title, (string label, string value)[] rows)
    {
        const int rowH   = 22;
        const int headerH = 30;
        const int padX    = 12;
        int h = headerH + rows.Length * rowH + 14;

        var card = new Panel
        {
            BackColor = AppTheme.BgCard,
            Height    = h,
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border, 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Title
        var titleLbl = AppTheme.MakeLabel(title, AppTheme.FontBold, AppTheme.Accent);
        titleLbl.Location = new Point(padX, 7);
        card.Controls.Add(titleLbl);

        // Separator
        var sep = new Panel { BackColor = AppTheme.Border, Height = 1, Left = 0, Top = headerH - 1 };
        card.Controls.Add(sep);
        card.Resize += (_, _) => sep.Width = card.Width;

        // Rows
        for (int i = 0; i < rows.Length; i++)
        {
            int y = headerH + 4 + i * rowH;
            var (lbl, val) = rows[i];

            var lKey = AppTheme.MakeLabel(lbl + ":", AppTheme.FontSmall, AppTheme.TextMuted);
            lKey.Location = new Point(padX, y + 2);
            lKey.Width    = 90;
            lKey.AutoSize = false;

            var lVal = AppTheme.MakeLabel(val, AppTheme.FontSmall, AppTheme.TextPrimary);
            lVal.AutoSize = false;
            lVal.Left     = padX + 94;
            lVal.Top      = y + 2;
            lVal.Anchor   = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            card.Controls.AddRange(new Control[] { lKey, lVal });
            card.Resize += (_, _) => lVal.Width = card.Width - lVal.Left - padX;
        }

        return card;
    }

    static Panel MakeBar(double pct, Color color)
    {
        var track = new Panel { Height = 6, BackColor = AppTheme.BgPanel };
        var fill  = new Panel { Height = 6, BackColor = color, Dock = DockStyle.Left };
        track.Controls.Add(fill);
        track.Resize  += (_, _) => fill.Width = (int)(track.Width * Math.Clamp(pct / 100.0, 0, 1));
        track.Width    = 100; // will be resized
        track.Anchor   = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        return track;
    }

    void SafeSet<T>(T ctrl, Action<T> act) where T : Control
    {
        if (ctrl.IsDisposed) return;
        if (ctrl.InvokeRequired) ctrl.Invoke(() => act(ctrl));
        else act(ctrl);
    }
}
