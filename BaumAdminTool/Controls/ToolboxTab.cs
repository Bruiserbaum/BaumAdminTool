using BaumAdminTool.Services;

namespace BaumAdminTool.Controls;

internal sealed class ToolboxTab : Panel
{
    private readonly LiveOutputPanel _output;

    public ToolboxTab(LiveOutputPanel output)
    {
        _output   = output;
        BackColor = AppTheme.BgMain;
        Dock      = DockStyle.Fill;

        var scroll = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            BackColor  = AppTheme.BgMain,
        };
        AppTheme.ApplyDarkScrollBar(scroll);

        int y = 12;

        void Section(string heading, (string label, string desc, Func<Task> action)[] items)
        {
            var hdr = AppTheme.MakeLabel(heading, AppTheme.FontHeader, AppTheme.Accent);
            hdr.Location = new Point(16, y);
            scroll.Controls.Add(hdr);
            y += 28;

            var sep = new Panel { BackColor = AppTheme.Border, Height = 1, Left = 16, Top = y, Width = 600 };
            scroll.Controls.Add(sep);
            y += 8;

            int col = 0;
            const int btnW = 200, btnH = 56, gapX = 10, gapY = 8, startX = 16;

            int rowStart = y;
            foreach (var (label, desc, action) in items)
            {
                int bx = startX + col * (btnW + gapX);
                var btn  = MakeActionButton(label, desc, action);
                btn.SetBounds(bx, y, btnW, btnH);
                scroll.Controls.Add(btn);

                col++;
                if (col >= 4) { col = 0; y += btnH + gapY; }
            }
            if (col > 0) y += btnH + gapY;
            y += 16;
        }

        Section("Network", new (string, string, Func<Task>)[]
        {
            ("Flush DNS",        "ipconfig /flushdns",        () => RunAction("Flush DNS",        AdminActionService.FlushDnsAsync)),
            ("Reset Winsock",    "netsh winsock reset",       () => RunAction("Reset Winsock",    AdminActionService.ResetWinsockAsync)),
            ("Reset TCP/IP",     "netsh int ip reset",        () => RunAction("Reset TCP/IP",     AdminActionService.ResetTcpIpAsync)),
            ("Release / Renew",  "Release & renew IP lease",  () => RunAction("Release/Renew IP", AdminActionService.ReleaseRenewIpAsync)),
            ("Ping 8.8.8.8",     "Test internet connectivity",() => RunAction("Ping Test",        (o, e, c) => AdminActionService.PingTestAsync("8.8.8.8", o, e, c))),
            ("Restart WLAN",     "Restart wireless service",  () => RunAction("Restart WLAN",     AdminActionService.RestartWlanAsync)),
        });

        Section("System Repair", new (string, string, Func<Task>)[]
        {
            ("SFC Scan",         "sfc /scannow",              () => RunAction("SFC Scan",         AdminActionService.RunSfcAsync)),
            ("DISM Repair",      "/RestoreHealth",            () => RunAction("DISM Repair",      AdminActionService.RunDismAsync)),
            ("Clear Temp Files", "Delete %TEMP% contents",    () => RunAction("Clear Temp",       AdminActionService.ClearTempAsync)),
            ("Empty Recycle Bin","Clear all recycle bins",    () => RunAction("Empty Recycle Bin",AdminActionService.EmptyRecycleBinAsync)),
            ("Check C: Disk",    "Repair-Volume -Scan",       () => RunAction("Check Disk C:",    (o, e, c) => AdminActionService.CheckDiskAsync("C", o, e, c))),
        });

        Section("Windows", new (string, string, Func<Task>)[]
        {
            ("GPUpdate",         "gpupdate /force",           () => RunAction("GPUpdate",         AdminActionService.GpUpdateAsync)),
            ("Restart Explorer", "Stop/start explorer.exe",   () => RunAction("Restart Explorer", AdminActionService.RestartExplorerAsync)),
            ("Restart Spooler",  "Restart Print Spooler svc", () => RunAction("Restart Spooler",  AdminActionService.RestartSpoolerAsync)),
        });

        Section("Registry", new (string, string, Func<Task>)[]
        {
            ("Clear Recent Files","Remove RecentDocs entries",() => RunAction("Clear Recent Files",AdminActionService.ClearRecentFilesAsync)),
            ("Clear Run MRU",    "Remove Run dialog history", () => RunAction("Clear Run MRU",    AdminActionService.ClearRunMruAsync)),
        });

        scroll.AutoScrollMinSize = new Size(0, y + 20);
        Controls.Add(scroll);
    }

    Button MakeActionButton(string label, string desc, Func<Task> action)
    {
        var btn = new Button
        {
            FlatStyle = FlatStyle.Flat,
            BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor    = Cursors.Hand,
            Padding   = new Padding(8, 0, 0, 0),
        };
        btn.FlatAppearance.BorderColor           = AppTheme.Border;
        btn.FlatAppearance.MouseOverBackColor     = AppTheme.BgCardHover;
        btn.FlatAppearance.MouseDownBackColor     = AppTheme.BgPanel;

        btn.Paint += (_, e) =>
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // Label
            TextRenderer.DrawText(e.Graphics, label,
                AppTheme.FontBold, new Rectangle(10, 8, btn.Width - 20, 20),
                AppTheme.TextPrimary, TextFormatFlags.Left);
            // Description
            TextRenderer.DrawText(e.Graphics, desc,
                AppTheme.FontSmall, new Rectangle(10, 28, btn.Width - 20, 18),
                AppTheme.TextMuted, TextFormatFlags.Left);
        };
        btn.Click += async (s, _) =>
        {
            btn.Enabled   = false;
            btn.BackColor = AppTheme.BgPanel;
            try   { await action(); }
            catch (Exception ex) { _output.AppendError($"Error: {ex.Message}"); }
            finally
            {
                btn.Enabled   = true;
                btn.BackColor = AppTheme.BgCard;
            }
        };
        return btn;
    }

    Task RunAction(string name, Func<Action<string>, Action<string>, Action<int>, Task> fn)
    {
        _output.AppendInfo($"▶ {name}");
        return fn(
            line => _output.Append(line),
            line => _output.AppendError(line),
            code => _output.AppendSuccess($"✔ {name} completed (exit {code})"));
    }
}
