using Microsoft.Win32;

namespace BaumAdminTool.Controls;

internal sealed class AppsTab : Panel
{
    private readonly DataGridView _grid;
    private readonly TextBox      _search;
    private readonly Label        _countLabel;
    private List<AppRow>          _all = new();

    record AppRow(string Name, string Version, string Publisher, string InstallDate);

    public AppsTab()
    {
        BackColor = AppTheme.BgMain;
        Dock      = DockStyle.Fill;

        // Toolbar
        var toolbar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 42,
            BackColor = AppTheme.BgPanel,
        };

        var searchLbl = AppTheme.MakeLabel("Search:", AppTheme.FontSmall, AppTheme.TextMuted);
        searchLbl.Location = new Point(8, 13);

        _search = new TextBox
        {
            Location    = new Point(60, 9),
            Width       = 260,
            Height      = 24,
            BackColor   = AppTheme.BgCard,
            ForeColor   = AppTheme.TextPrimary,
            Font        = AppTheme.FontBody,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _search.TextChanged += (_, _) => ApplyFilter();

        var refreshBtn = AppTheme.MakeButton("⟳  Refresh");
        refreshBtn.SetBounds(332, 7, 100, 28);
        refreshBtn.Click += async (_, _) => await LoadAsync(refreshBtn);

        _countLabel = AppTheme.MakeLabel("", AppTheme.FontSmall, AppTheme.TextMuted);
        _countLabel.Location = new Point(444, 13);

        toolbar.Controls.AddRange(new Control[] { searchLbl, _search, refreshBtn, _countLabel });

        // Grid
        _grid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            BackgroundColor       = AppTheme.BgMain,
            GridColor             = AppTheme.Border,
            BorderStyle           = BorderStyle.None,
            RowHeadersVisible     = false,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            ReadOnly              = true,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            Font                  = AppTheme.FontBody,
            CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
        };
        StyleGrid();
        AppTheme.ApplyDarkScrollBar(_grid);

        Controls.Add(_grid);
        Controls.Add(toolbar);

        // Trigger on handle creation so Invoke is safe and the Task is properly awaited
        HandleCreated += async (_, _) => await LoadAsync(refreshBtn);
    }

    void StyleGrid()
    {
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor          = AppTheme.BgPanel,
            ForeColor          = AppTheme.Accent,
            Font               = AppTheme.FontBold,
            Padding            = new Padding(4, 0, 0, 0),
            SelectionBackColor = AppTheme.BgPanel,
            SelectionForeColor = AppTheme.Accent,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor          = AppTheme.BgMain,
            ForeColor          = AppTheme.TextPrimary,
            SelectionBackColor = AppTheme.BgCard,
            SelectionForeColor = AppTheme.TextPrimary,
            Padding            = new Padding(4, 0, 0, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor          = AppTheme.BgPanel,
            ForeColor          = AppTheme.TextPrimary,
            SelectionBackColor = AppTheme.BgCard,
            SelectionForeColor = AppTheme.TextPrimary,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",      HeaderText = "Application Name", FillWeight = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Version",   HeaderText = "Version",          FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Publisher", HeaderText = "Publisher",        FillWeight = 25 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",      HeaderText = "Installed",        FillWeight = 10 });
    }

    async Task LoadAsync(Button btn)
    {
        btn.Enabled      = false;
        _countLabel.Text = "Loading...";

        _all = await Task.Run(ReadRegistry);

        if (!IsDisposed)
        {
            ApplyFilter();
            btn.Enabled = true;
        }
    }

    static List<AppRow> ReadRegistry()
    {
        var seen = new Dictionary<string, AppRow>(StringComparer.OrdinalIgnoreCase);

        void ScanKey(RegistryKey? root, string path)
        {
            using var key = root?.OpenSubKey(path);
            if (key == null) return;
            foreach (var sub in key.GetSubKeyNames())
            {
                using var k = key.OpenSubKey(sub);
                if (k == null) continue;
                var name = k.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                // Skip Windows Updates / hotfixes
                if (name.StartsWith("KB") && name.Length < 12) continue;
                var version   = k.GetValue("DisplayVersion")  as string ?? "";
                var publisher = k.GetValue("Publisher")        as string ?? "";
                var date      = k.GetValue("InstallDate")      as string ?? "";
                if (date.Length == 8) // YYYYMMDD
                    date = $"{date[4..6]}/{date[6..8]}/{date[..4]}";
                seen[name] = new AppRow(name, version, publisher, date);
            }
        }

        ScanKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanKey(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanKey(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

        return seen.Values.OrderBy(a => a.Name).ToList();
    }

    void ApplyFilter()
    {
        var term = _search.Text.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? _all
            : _all.Where(a =>
                a.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var a in filtered)
            _grid.Rows.Add(a.Name, a.Version, a.Publisher, a.InstallDate);
        _grid.ResumeLayout(false);

        _countLabel.Text = $"{filtered.Count} apps";
    }
}
