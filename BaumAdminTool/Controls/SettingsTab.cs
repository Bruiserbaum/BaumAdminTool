using BaumAdminTool.Services;

namespace BaumAdminTool.Controls;

internal sealed class SettingsTab : Panel
{
    public SettingsTab()
    {
        BackColor = AppTheme.BgMain;
        Dock      = DockStyle.Fill;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BgMain };
        AppTheme.ApplyDarkScrollBar(scroll);
        Controls.Add(scroll);

        int y = 24;

        // ── Updates ───────────────────────────────────────────────────────
        AddSectionHeader(scroll, "UPDATES", ref y);

        var updateCard = AddCard(scroll, ref y, 380, 100);

        var checkBtn = AppTheme.MakeButton("Check for Updates", AppTheme.Accent);
        checkBtn.ForeColor = Color.White;
        checkBtn.SetBounds(16, 14, 170, 32);
        updateCard.Controls.Add(checkBtn);

        // Progress bar (two nested panels: track + fill)
        var pbTrack = new Panel
        {
            BackColor = AppTheme.BgPanel,
            Bounds    = new Rectangle(16, 54, 340, 8),
            Visible   = false,
        };
        var pbFill = new Panel { Dock = DockStyle.Left, BackColor = AppTheme.Accent, Width = 0 };
        pbTrack.Controls.Add(pbFill);
        updateCard.Controls.Add(pbTrack);

        var statusLbl = new Label
        {
            Text      = "Click \"Check for Updates\" to check for a newer version.",
            ForeColor = AppTheme.TextSecondary,
            Font      = AppTheme.FontSmall,
            Bounds    = new Rectangle(16, 68, 344, 24),
            AutoSize  = false,
            BackColor = Color.Transparent,
        };
        updateCard.Controls.Add(statusLbl);

        checkBtn.Click += async (_, _) =>
        {
            checkBtn.Enabled  = false;
            statusLbl.Text    = "Checking GitHub...";
            statusLbl.ForeColor = AppTheme.TextSecondary;
            pbTrack.Visible   = false;
            pbFill.Width      = 0;

            UpdateService.UpdateInfo? info;
            try   { info = await UpdateService.CheckAsync(); }
            catch { info = null; }

            if (info == null)
            {
                statusLbl.Text      = "Could not reach GitHub. Check your internet connection.";
                statusLbl.ForeColor = AppTheme.Danger;
                checkBtn.Enabled    = true;
                return;
            }

            var current = UpdateService.CurrentVersion();
            if (info.Latest <= current)
            {
                statusLbl.Text      = $"You are up to date  (v{current})";
                statusLbl.ForeColor = AppTheme.Success;
                checkBtn.Enabled    = true;
                return;
            }

            statusLbl.Text      = $"Update available: v{info.Latest}  —  Downloading...";
            statusLbl.ForeColor = AppTheme.Warning;
            pbTrack.Visible     = true;

            var prog = new Progress<int>(pct =>
            {
                if (!IsDisposed)
                    Invoke(() => pbFill.Width = (int)(pbTrack.Width * pct / 100.0));
            });

            try
            {
                await UpdateService.ApplyAsync(info.DownloadUrl, prog);
                // ApplyAsync calls Application.Exit() on success — code below won't run
            }
            catch (Exception ex)
            {
                statusLbl.Text      = $"Download failed: {ex.Message}";
                statusLbl.ForeColor = AppTheme.Danger;
                pbTrack.Visible     = false;
                checkBtn.Enabled    = true;
            }
        };

        // ── Version ───────────────────────────────────────────────────────
        y += 12;
        AddSectionHeader(scroll, "VERSION", ref y);

        var verCard = AddCard(scroll, ref y, 380, 52);
        var verLbl  = AppTheme.MakeLabel(
            $"BaumAdminTool  v{UpdateService.CurrentVersion()}",
            AppTheme.FontBold, AppTheme.TextPrimary);
        verLbl.Location = new Point(16, 14);
        verCard.Controls.Add(verLbl);

        scroll.AutoScrollMinSize = new Size(0, y + 40);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static void AddSectionHeader(Panel parent, string text, ref int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            ForeColor = AppTheme.TextMuted,
            Font      = AppTheme.FontSmall,
            AutoSize  = true,
            Location  = new Point(24, y),
            BackColor = Color.Transparent,
        });
        y += 22;
    }

    static Panel AddCard(Panel parent, ref int y, int w, int h)
    {
        var card = new Panel { BackColor = AppTheme.BgCard, Bounds = new Rectangle(24, y, w, h) };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        parent.Controls.Add(card);
        y += h + 8;
        return card;
    }
}
