using System.Diagnostics;

namespace BaumAdminTool.Controls;

internal sealed class DevicesTab : Panel
{
    private readonly DataGridView _grid;
    private readonly TextBox      _search;
    private readonly ComboBox     _classFilter;
    private readonly Label        _countLabel;
    private readonly Label        _statusLabel;
    private List<DeviceRow>       _all = new();

    record DeviceRow(
        string Name,
        string DeviceClass,
        string DriverVersion,
        string DriverDate,
        string Manufacturer,
        bool   IsSigned);

    public DevicesTab()
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

        var loadBtn = AppTheme.MakeButton("⟳  Load Devices");
        loadBtn.SetBounds(8, 7, 130, 28);
        loadBtn.Click += async (_, _) => await LoadAsync(loadBtn);

        var searchLbl = AppTheme.MakeLabel("Search:", AppTheme.FontSmall, AppTheme.TextMuted);
        searchLbl.Location = new Point(150, 13);

        _search = new TextBox
        {
            BackColor       = AppTheme.BgCard,
            ForeColor       = AppTheme.TextPrimary,
            Font            = AppTheme.FontBody,
            BorderStyle     = BorderStyle.FixedSingle,
            PlaceholderText = "Device name or manufacturer...",
        };
        _search.SetBounds(200, 9, 220, 24);
        _search.TextChanged += (_, _) => ApplyFilter();

        var classLbl = AppTheme.MakeLabel("Class:", AppTheme.FontSmall, AppTheme.TextMuted);
        classLbl.Location = new Point(432, 13);

        _classFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = AppTheme.BgCard,
            ForeColor     = AppTheme.TextPrimary,
            Font          = AppTheme.FontBody,
            FlatStyle     = FlatStyle.Flat,
        };
        _classFilter.SetBounds(472, 9, 170, 24);
        _classFilter.Items.Add("All Classes");
        _classFilter.SelectedIndex = 0;
        _classFilter.SelectedIndexChanged += (_, _) => ApplyFilter();

        _countLabel = AppTheme.MakeLabel("", AppTheme.FontSmall, AppTheme.TextMuted);
        _countLabel.Location = new Point(654, 13);

        toolbar.Controls.AddRange(new Control[]
            { loadBtn, searchLbl, _search, classLbl, _classFilter, _countLabel });

        // Status label (shown while loading)
        _statusLabel = new Label
        {
            Text      = "Click \"Load Devices\" to enumerate installed drivers.  This may take a few seconds.",
            ForeColor = AppTheme.TextMuted,
            Font      = AppTheme.FontBody,
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
            BackColor = AppTheme.BgMain,
        };

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
            Visible               = false,
        };
        StyleGrid();
        AppTheme.ApplyDarkScrollBar(_grid);

        Controls.Add(_grid);
        Controls.Add(_statusLabel);
        Controls.Add(toolbar);
    }

    // ── Grid styling ────────────────────────────────────────────────────────

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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",        HeaderText = "Device Name",      FillWeight = 32 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Class",       HeaderText = "Class",            FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Version",     HeaderText = "Driver Version",   FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",        HeaderText = "Driver Date",      FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Manufacturer",HeaderText = "Manufacturer",     FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Signed",      HeaderText = "Signed",           FillWeight = 8  });
    }

    // ── Data loading ────────────────────────────────────────────────────────

    async Task LoadAsync(Button btn)
    {
        btn.Enabled          = false;
        _statusLabel.Text    = "Querying Win32_PnPSignedDriver — please wait...";
        _statusLabel.Visible = true;
        _grid.Visible        = false;

        _all = await Task.Run(QueryDrivers);

        if (IsDisposed) return;
        Invoke(() =>
        {
            PopulateClassFilter();
            ApplyFilter();
            _statusLabel.Visible = false;
            _grid.Visible        = true;
            btn.Enabled          = true;
        });
    }

    static List<DeviceRow> QueryDrivers()
    {
        // Use ConvertTo-Csv for reliable structured output
        const string ps =
            "Get-CimInstance Win32_PnPSignedDriver" +
            " | Where-Object { $_.DeviceName -ne $null -and $_.DeviceName -ne '' }" +
            " | Select-Object DeviceName,DeviceClass,DriverVersion,DriverDate,Manufacturer,IsSigned" +
            " | ConvertTo-Csv -NoTypeInformation";

        string csv;
        try
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -Command \"{ps}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var proc = Process.Start(psi)!;
            csv = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
        }
        catch { return new List<DeviceRow>(); }

        var rows = new List<DeviceRow>();
        bool header = true;
        foreach (var line in csv.Split('\n'))
        {
            if (header) { header = false; continue; }  // skip CSV header
            var fields = SplitCsvLine(line.Trim());
            if (fields.Length < 6) continue;

            var name    = Unquote(fields[0]);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var date = ParseDriverDate(Unquote(fields[3]));

            rows.Add(new DeviceRow(
                Name:          name,
                DeviceClass:   Unquote(fields[1]),
                DriverVersion: Unquote(fields[2]),
                DriverDate:    date,
                Manufacturer:  Unquote(fields[4]),
                IsSigned:      string.Equals(Unquote(fields[5]), "True", StringComparison.OrdinalIgnoreCase)));
        }

        return rows.OrderBy(r => r.DeviceClass).ThenBy(r => r.Name).ToList();
    }

    // PowerShell serialises DateTime as e.g. "01/15/2024 00:00:00" or WMI-style
    static string ParseDriverDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "N/A") return "";
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }

    static string[] SplitCsvLine(string line)
    {
        // Handles quoted fields containing commas
        var fields = new List<string>();
        bool inQuote = false;
        var cur = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuote = !inQuote; }
            else if (c == ',' && !inQuote) { fields.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        fields.Add(cur.ToString());
        return fields.ToArray();
    }

    static string Unquote(string s) => s.Trim('"');

    // ── Filtering ────────────────────────────────────────────────────────────

    void PopulateClassFilter()
    {
        var classes = _all
            .Select(r => r.DeviceClass)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        _classFilter.Items.Clear();
        _classFilter.Items.Add("All Classes");
        foreach (var c in classes)
            _classFilter.Items.Add(c);
        _classFilter.SelectedIndex = 0;
    }

    void ApplyFilter()
    {
        var term  = _search.Text.Trim();
        var cls   = _classFilter.SelectedItem as string ?? "All Classes";

        var filtered = _all.Where(r =>
        {
            bool nameOk = string.IsNullOrEmpty(term) ||
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Manufacturer.Contains(term, StringComparison.OrdinalIgnoreCase);
            bool clsOk  = cls == "All Classes" ||
                string.Equals(r.DeviceClass, cls, StringComparison.OrdinalIgnoreCase);
            return nameOk && clsOk;
        }).ToList();

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var r in filtered)
        {
            var idx = _grid.Rows.Add(
                r.Name,
                r.DeviceClass,
                r.DriverVersion,
                r.DriverDate,
                r.Manufacturer,
                r.IsSigned ? "✔" : "✘");

            if (!r.IsSigned)
                _grid.Rows[idx].DefaultCellStyle.ForeColor = AppTheme.Warning;
        }
        _grid.ResumeLayout(false);
        _countLabel.Text = $"{filtered.Count} devices";
    }
}
