using BaumAdminTool.Controls;

namespace BaumAdminTool;

internal sealed class MainForm : Form
{
    // ── Layout panels ───────────────────────────────────────────────────────
    private readonly Panel          _titleBar;
    private readonly Panel          _tabBar;
    private readonly Panel          _content;
    private readonly LiveOutputPanel _output;

    // ── Tabs ────────────────────────────────────────────────────────────────
    private readonly OverviewTab  _overviewTab;
    private readonly ProcessTab   _processTab;
    private readonly ToolboxTab   _toolboxTab;
    private readonly BackupTab    _backupTab;
    private readonly AppsTab      _appsTab;
    private readonly LogsTab      _logsTab;
    private readonly DevicesTab   _devicesTab;
    private readonly SettingsTab  _settingsTab;
    private readonly Panel[]      _tabs;
    private readonly Button[]     _tabBtns;
    private int _activeTab = 0;

    // ── Drag support ────────────────────────────────────────────────────────
    private Point _dragStart;
    private bool  _dragging;

    public MainForm()
    {
        // Form setup
        Text            = "BaumAdminTool";
        Size            = new Size(1200, 760);
        MinimumSize     = new Size(900, 580);
        FormBorderStyle = FormBorderStyle.None;
        BackColor       = AppTheme.BgDeep;
        StartPosition   = FormStartPosition.CenterScreen;
        Icon            = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        // ── Live output (bottom, add first for Fill to work) ──────────────
        _output = new LiveOutputPanel
        {
            Dock   = DockStyle.Bottom,
            Height = 180,
        };

        // ── Content area (Fill) ───────────────────────────────────────────
        _content = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = AppTheme.BgMain,
        };

        _overviewTab = new OverviewTab();
        _processTab  = new ProcessTab();
        _toolboxTab  = new ToolboxTab(_output);
        _backupTab   = new BackupTab(_output);
        _appsTab     = new AppsTab();
        _logsTab     = new LogsTab();
        _devicesTab  = new DevicesTab();
        _settingsTab = new SettingsTab();
        _tabs = new Panel[]
            { _overviewTab, _processTab, _toolboxTab, _backupTab, _appsTab, _logsTab, _devicesTab, _settingsTab };

        foreach (var tab in _tabs)
        {
            tab.Visible = false;
            _content.Controls.Add(tab);
        }
        _tabs[0].Visible = true;

        // ── Tab bar ───────────────────────────────────────────────────────
        _tabBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = AppTheme.BgPanel,
        };

        string[] tabNames =
            { "  Overview  ", "  Processes  ", "  Toolbox  ", "  Backup  ", "  Apps  ", "  Logs  ", "  Devices  ", "  Settings  " };
        _tabBtns = new Button[tabNames.Length];
        int bx = 8;
        for (int i = 0; i < tabNames.Length; i++)
        {
            int idx = i;
            var btn = new Button
            {
                Text      = tabNames[i],
                FlatStyle = FlatStyle.Flat,
                Font      = AppTheme.FontBold,
                ForeColor = AppTheme.TextSecondary,
                BackColor = AppTheme.BgPanel,
                Height    = 40,
                Cursor    = Cursors.Hand,
                Left      = bx,
                Top       = 0,
            };
            btn.FlatAppearance.BorderSize             = 0;
            btn.FlatAppearance.MouseOverBackColor     = AppTheme.BgCard;
            btn.FlatAppearance.MouseDownBackColor     = AppTheme.BgCard;
            btn.Width = TextRenderer.MeasureText(tabNames[i], AppTheme.FontBold).Width + 20;
            btn.Click += (_, _) => SelectTab(idx);
            _tabBar.Controls.Add(btn);
            _tabBtns[i] = btn;
            bx += btn.Width;
        }

        // ── Title bar ─────────────────────────────────────────────────────
        _titleBar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 36,
            BackColor = AppTheme.BgDeep,
        };

        var appTitle = new Label
        {
            Text      = "BaumAdminTool",
            ForeColor = AppTheme.TextPrimary,
            Font      = AppTheme.FontHeader,
            AutoSize  = true,
            Location  = new Point(12, 9),
        };

        var closeBtn = MakeTitleBtn("✕", AppTheme.Danger);
        var maxBtn   = MakeTitleBtn("□", AppTheme.Border);
        var minBtn   = MakeTitleBtn("─", AppTheme.Border);

        closeBtn.Click += (_, _) => Application.Exit();
        maxBtn.Click   += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
        minBtn.Click   += (_, _) => WindowState = FormWindowState.Minimized;

        _titleBar.Resize += (_, _) =>
        {
            closeBtn.Location = new Point(_titleBar.Width - 38, 0);
            maxBtn.Location   = new Point(_titleBar.Width - 76, 0);
            minBtn.Location   = new Point(_titleBar.Width - 114, 0);
        };

        _titleBar.Controls.AddRange(new Control[] { appTitle, minBtn, maxBtn, closeBtn });

        // Drag to move
        _titleBar.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; } };
        _titleBar.MouseMove += (_, e) => { if (_dragging) { var d = e.Location - new Size(_dragStart); Location += new Size(d); } };
        _titleBar.MouseUp   += (_, _) => _dragging = false;
        appTitle.MouseDown  += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = _titleBar.PointToClient(PointToScreen(e.Location)); } };
        appTitle.MouseMove  += (_, e) => { if (_dragging) { var d = _titleBar.PointToClient(PointToScreen(e.Location)) - new Size(_dragStart); Location += new Size(d); } };
        appTitle.MouseUp    += (_, _) => _dragging = false;

        // ── Assemble ─────────────────────────────────────────────────────
        // Fill and Bottom before Top controls
        Controls.Add(_content);
        Controls.Add(_output);
        Controls.Add(_tabBar);
        Controls.Add(_titleBar);

        SelectTab(0);
    }

    void SelectTab(int idx)
    {
        _activeTab = idx;
        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].Visible    = i == idx;
            _tabBtns[i].ForeColor  = i == idx ? AppTheme.Accent        : AppTheme.TextSecondary;
            _tabBtns[i].BackColor  = i == idx ? AppTheme.BgCard        : AppTheme.BgPanel;
        }
    }

    static Button MakeTitleBtn(string text, Color hoverColor)
    {
        var btn = new Button
        {
            Text      = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppTheme.TextSecondary,
            BackColor = AppTheme.BgDeep,
            Size      = new Size(36, 36),
            Font      = AppTheme.FontBody,
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize             = 0;
        btn.FlatAppearance.MouseOverBackColor     = hoverColor;
        btn.FlatAppearance.MouseDownBackColor     = hoverColor;
        return btn;
    }

    // Resize grip hit-test so borderless form can still be resized
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST   = 0x84;
        const int HTBOTTOMRIGHT  = 17;
        if (m.Msg == WM_NCHITTEST)
        {
            var pos  = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
            bool nearBottom = pos.Y >= ClientSize.Height - 12;
            bool nearRight  = pos.X >= ClientSize.Width  - 12;
            if (nearBottom && nearRight) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
        }
        base.WndProc(ref m);
    }

    // Drawn icon — avoids PNG-in-ICO incompatibility with System.Drawing.Icon
    static Icon GenerateIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            const float s = 32f;

            // Rounded-rect background
            using var bgBrush = new SolidBrush(Color.FromArgb(255, 18, 18, 26));
            using var bgPath  = new System.Drawing.Drawing2D.GraphicsPath();
            const int rr = 6;
            bgPath.AddArc(0, 0, rr * 2, rr * 2, 180, 90);
            bgPath.AddArc(32 - rr * 2, 0, rr * 2, rr * 2, 270, 90);
            bgPath.AddArc(32 - rr * 2, 32 - rr * 2, rr * 2, rr * 2, 0, 90);
            bgPath.AddArc(0, 32 - rr * 2, rr * 2, rr * 2, 90, 90);
            bgPath.CloseFigure();
            g.FillPath(bgBrush, bgPath);

            // Terminal window body
            using var termBrush   = new SolidBrush(Color.FromArgb(255, 22, 22, 38));
            using var accentBrush = new SolidBrush(AppTheme.Accent);
            using var accentPen   = new Pen(AppTheme.Accent, 1.2f);
            g.FillRectangle(termBrush, 4f, 5f, 23f, 18f);
            g.DrawRectangle(accentPen, 4f, 5f, 23f, 18f);

            // Title bar strip
            g.FillRectangle(accentBrush, 4f, 5f, 23f, 4f);

            // ">" prompt in green
            using var promptBrush = new SolidBrush(AppTheme.Success);
            using var font        = new Font("Consolas", 7f, FontStyle.Bold);
            g.DrawString(">", font, promptBrush, 5f, 11f);

            // Underscore cursor in white
            using var cursorBrush = new SolidBrush(AppTheme.TextPrimary);
            g.FillRectangle(cursorBrush, 13f, 19f, 6f, 1.5f);

            // Wrench badge (bottom-right)
            using var wrenchBrush = new SolidBrush(AppTheme.Warning);
            using var wrenchPen   = new Pen(AppTheme.Warning, 2f);
            wrenchPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            wrenchPen.EndCap   = System.Drawing.Drawing2D.LineCap.Round;
            g.FillEllipse(wrenchBrush, 20f, 20f, 5f, 5f);
            using var holeBrush = new SolidBrush(Color.FromArgb(255, 18, 18, 26));
            g.FillEllipse(holeBrush, 21.5f, 21.5f, 2f, 2f);
            g.DrawLine(wrenchPen, 24f, 24f, 27f, 27f);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
