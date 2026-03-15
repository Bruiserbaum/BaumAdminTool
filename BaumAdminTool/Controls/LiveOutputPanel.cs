namespace BaumAdminTool.Controls;

internal sealed class LiveOutputPanel : Panel
{
    private readonly RichTextBox _box;
    private readonly CheckBox    _autoScroll;

    public LiveOutputPanel()
    {
        BackColor = AppTheme.BgDeep;
        Padding   = new Padding(0);

        // Header bar
        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 30,
            BackColor = AppTheme.BgPanel,
        };

        var title = AppTheme.MakeLabel("▼  Live Output", AppTheme.FontBold, AppTheme.Accent);
        title.Location = new Point(10, 6);

        var clearBtn = AppTheme.MakeButton("Clear");
        clearBtn.Size   = new Size(60, 22);
        clearBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        clearBtn.Click += (_, _) => _box.Clear();

        _autoScroll = new CheckBox
        {
            Text      = "Auto-scroll",
            ForeColor = AppTheme.TextMuted,
            Font      = AppTheme.FontSmall,
            Checked   = true,
            AutoSize  = true,
            Anchor    = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.Transparent,
        };

        header.Resize += (_, _) =>
        {
            clearBtn.Location   = new Point(header.Width - 68, 4);
            _autoScroll.Location = new Point(header.Width - 158, 7);
        };

        header.Controls.AddRange(new Control[] { title, _autoScroll, clearBtn });

        _box = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = AppTheme.BgDeep,
            ForeColor   = AppTheme.TextPrimary,
            Font        = AppTheme.FontMono,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };
        AppTheme.ApplyDarkScrollBar(_box);

        Controls.Add(_box);
        Controls.Add(header);
    }

    public void Append(string text, Color? color = null)
    {
        if (InvokeRequired) { Invoke(() => Append(text, color)); return; }
        var ts = DateTime.Now.ToString("HH:mm:ss");
        _box.SelectionStart  = _box.TextLength;
        _box.SelectionLength = 0;
        _box.SelectionColor  = AppTheme.TextMuted;
        _box.AppendText($"[{ts}] ");
        _box.SelectionColor  = color ?? AppTheme.TextPrimary;
        _box.AppendText(text + "\n");
        if (_autoScroll.Checked) _box.ScrollToCaret();
    }

    public void AppendSuccess(string t) => Append(t, AppTheme.Success);
    public void AppendError(string t)   => Append(t, AppTheme.Danger);
    public void AppendInfo(string t)    => Append(t, AppTheme.Accent);
    public void AppendWarning(string t) => Append(t, AppTheme.Warning);
}
