using System.Diagnostics.Eventing.Reader;

namespace BaumAdminTool.Controls;

internal sealed class LogsTab : Panel
{
    private readonly DataGridView _grid;
    private readonly RichTextBox  _detail;
    private readonly ComboBox     _levelFilter;
    private readonly ComboBox     _logFilter;
    private readonly Label        _countLabel;
    private List<LogRow>          _all = new();

    record LogRow(DateTime Time, string Level, string Log, string Source, int EventId, string Message);

    public LogsTab()
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

        var refreshBtn = AppTheme.MakeButton("⟳  Load Logs");
        refreshBtn.SetBounds(8, 7, 110, 28);
        refreshBtn.Click += async (_, _) => await LoadAsync(refreshBtn);

        var levelLbl = AppTheme.MakeLabel("Level:", AppTheme.FontSmall, AppTheme.TextMuted);
        levelLbl.Location = new Point(128, 13);

        _levelFilter = MakeCombo(new[] { "Critical + Error", "Critical Only", "Error Only", "Warning", "All" });
        _levelFilter.SetBounds(172, 9, 150, 24);
        _levelFilter.SelectedIndex = 0;
        _levelFilter.SelectedIndexChanged += (_, _) => ApplyFilter();

        var logLbl = AppTheme.MakeLabel("Log:", AppTheme.FontSmall, AppTheme.TextMuted);
        logLbl.Location = new Point(334, 13);

        _logFilter = MakeCombo(new[] { "All", "System", "Application", "Security" });
        _logFilter.SetBounds(364, 9, 130, 24);
        _logFilter.SelectedIndex = 0;
        _logFilter.SelectedIndexChanged += (_, _) => ApplyFilter();

        _countLabel = AppTheme.MakeLabel("", AppTheme.FontSmall, AppTheme.TextMuted);
        _countLabel.Location = new Point(506, 13);

        toolbar.Controls.AddRange(new Control[]
            { refreshBtn, levelLbl, _levelFilter, logLbl, _logFilter, _countLabel });

        // Split: grid on top, detail panel below
        var split = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor   = AppTheme.BgMain,
            BorderStyle = BorderStyle.None,
            SplitterWidth = 4,
            SplitterDistance = 380,
        };
        split.Panel1.BackColor = AppTheme.BgMain;
        split.Panel2.BackColor = AppTheme.BgDeep;

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
        _grid.SelectionChanged += OnSelectionChanged;
        split.Panel1.Controls.Add(_grid);

        // Detail pane
        var detailHeader = new Label
        {
            Text      = "Event Detail",
            ForeColor = AppTheme.Accent,
            Font      = AppTheme.FontBold,
            Dock      = DockStyle.Top,
            Height    = 24,
            Padding   = new Padding(8, 4, 0, 0),
            BackColor = AppTheme.BgPanel,
        };

        _detail = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            BackColor   = AppTheme.BgDeep,
            ForeColor   = AppTheme.TextPrimary,
            Font        = AppTheme.FontMono,
            ReadOnly    = true,
            BorderStyle = BorderStyle.None,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };
        AppTheme.ApplyDarkScrollBar(_detail);
        split.Panel2.Controls.Add(_detail);
        split.Panel2.Controls.Add(detailHeader);

        Controls.Add(split);
        Controls.Add(toolbar);
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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time",    HeaderText = "Time",     FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Level",   HeaderText = "Level",    FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Log",     HeaderText = "Log",      FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source",  HeaderText = "Source",   FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EventId", HeaderText = "Event ID", FillWeight = 8  });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message",  FillWeight = 28 });
    }

    async Task LoadAsync(Button btn)
    {
        btn.Enabled      = false;
        _countLabel.Text = "Reading event logs...";
        _all             = new();

        _all = await Task.Run(ReadLogs);

        if (!IsDisposed)
            Invoke(() =>
            {
                ApplyFilter();
                btn.Enabled = true;
            });
    }

    static List<LogRow> ReadLogs()
    {
        var rows    = new List<LogRow>();
        var logNames = new[] { "System", "Application" };

        // Level 1 = Critical, Level 2 = Error, Level 3 = Warning
        const string query =
            "*[System[Level<=3 and TimeCreated[timediff(@SystemTime) <= 2592000000]]]"; // 30 days

        foreach (var logName in logNames)
        {
            try
            {
                var q = new EventLogQuery(logName, PathType.LogName, query)
                    { ReverseDirection = true };
                using var reader = new EventLogReader(q);
                int n = 0;
                EventRecord? rec;
                while ((rec = reader.ReadEvent()) != null && n < 300)
                {
                    using (rec)
                    {
                        var level = rec.Level switch
                        {
                            1 => "Critical",
                            2 => "Error",
                            3 => "Warning",
                            _ => rec.LevelDisplayName ?? "Info",
                        };

                        var msg = TryFormat(rec);
                        rows.Add(new LogRow(
                            rec.TimeCreated ?? DateTime.MinValue,
                            level,
                            logName,
                            rec.ProviderName ?? "",
                            (int)(rec.Id & 0xFFFF),
                            msg.Split('\n')[0].Trim()   // first line only in grid
                        ));
                        n++;
                    }
                }
            }
            catch { }
        }

        return rows.OrderByDescending(r => r.Time).ToList();
    }

    static string TryFormat(EventRecord r)
    {
        try   { return r.FormatDescription() ?? r.ToXml(); }
        catch { return r.ToXml(); }
    }

    void ApplyFilter()
    {
        var level = _levelFilter.SelectedIndex;
        var log   = _logFilter.SelectedItem as string ?? "All";

        var filtered = _all.Where(r =>
        {
            bool levelOk = level switch
            {
                0 => r.Level is "Critical" or "Error",
                1 => r.Level == "Critical",
                2 => r.Level == "Error",
                3 => r.Level == "Warning",
                _ => true,
            };
            bool logOk = log == "All" || r.Log == log;
            return levelOk && logOk;
        }).ToList();

        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var r in filtered)
        {
            var idx = _grid.Rows.Add(
                r.Time.ToString("MM/dd HH:mm:ss"),
                r.Level,
                r.Log,
                r.Source,
                r.EventId,
                r.Message);

            _grid.Rows[idx].DefaultCellStyle.ForeColor = r.Level switch
            {
                "Critical" => AppTheme.Danger,
                "Error"    => Color.FromArgb(255, 160, 80),
                "Warning"  => AppTheme.Warning,
                _          => AppTheme.TextPrimary,
            };
            _grid.Rows[idx].Tag = r;
        }
        _grid.ResumeLayout(false);
        _countLabel.Text = $"{filtered.Count} events";
    }

    void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0) return;
        if (_grid.SelectedRows[0].Tag is not LogRow row) return;

        // Re-read the full message from the stored row
        _detail.Text = $"Time:     {row.Time:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Level:    {row.Level}\n" +
                       $"Log:      {row.Log}\n" +
                       $"Source:   {row.Source}\n" +
                       $"Event ID: {row.EventId}\n\n" +
                       $"{row.Message}";
    }

    static ComboBox MakeCombo(string[] items)
    {
        var cb = new ComboBox
        {
            DropDownStyle  = ComboBoxStyle.DropDownList,
            BackColor      = AppTheme.BgCard,
            ForeColor      = AppTheme.TextPrimary,
            Font           = AppTheme.FontBody,
            FlatStyle      = FlatStyle.Flat,
        };
        cb.Items.AddRange(items);
        return cb;
    }
}
