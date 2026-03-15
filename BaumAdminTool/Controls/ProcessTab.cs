using BaumAdminTool.Models;
using BaumAdminTool.Services;

namespace BaumAdminTool.Controls;

internal sealed class ProcessTab : Panel
{
    private readonly DataGridView      _grid;
    private readonly ProcessService    _svc = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label             _countLabel;
    private readonly CheckBox          _autoRefresh;

    public ProcessTab()
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

        var refreshBtn = AppTheme.MakeButton("⟳  Refresh");
        refreshBtn.Size     = new Size(100, 28);
        refreshBtn.Location = new Point(8, 7);
        refreshBtn.Click   += (_, _) => DoSample();

        _autoRefresh = new CheckBox
        {
            Text      = "Auto (3s)",
            ForeColor = AppTheme.TextSecondary,
            Font      = AppTheme.FontSmall,
            Checked   = true,
            AutoSize  = true,
            Location  = new Point(118, 13),
            BackColor = Color.Transparent,
        };
        _autoRefresh.CheckedChanged += (_, _) => _timer.Enabled = _autoRefresh.Checked;

        _countLabel = AppTheme.MakeLabel("", AppTheme.FontSmall, AppTheme.TextMuted);
        _countLabel.Location = new Point(210, 13);

        toolbar.Controls.AddRange(new Control[] { refreshBtn, _autoRefresh, _countLabel });

        // DataGridView
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

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) => DoSample();
        _timer.Start();

        DoSample();
    }

    void StyleGrid()
    {
        _grid.EnableHeadersVisualStyles = false;

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.BgPanel,
            ForeColor = AppTheme.Accent,
            Font      = AppTheme.FontBold,
            Padding   = new Padding(4, 0, 0, 0),
            SelectionBackColor = AppTheme.BgPanel,
            SelectionForeColor = AppTheme.Accent,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.BgMain,
            ForeColor = AppTheme.TextPrimary,
            SelectionBackColor = AppTheme.BgCard,
            SelectionForeColor = AppTheme.TextPrimary,
            Padding   = new Padding(4, 0, 0, 0),
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AppTheme.BgPanel,
            ForeColor = AppTheme.TextPrimary,
            SelectionBackColor = AppTheme.BgCard,
            SelectionForeColor = AppTheme.TextPrimary,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",   HeaderText = "Process Name", FillWeight = 40 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PID",    HeaderText = "PID",          FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPU",    HeaderText = "CPU %",        FillWeight = 15 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Memory", HeaderText = "Memory (MB)",  FillWeight = 20 });
    }

    void DoSample()
    {
        Task.Run(() =>
        {
            var entries = _svc.Sample(40);
            if (IsDisposed) return;
            Invoke(() => UpdateGrid(entries));
        });
    }

    void UpdateGrid(List<ProcessEntry> entries)
    {
        _grid.SuspendLayout();
        _grid.Rows.Clear();
        foreach (var e in entries)
        {
            var row = _grid.Rows[_grid.Rows.Add(e.Name, e.Pid, e.CpuStr, e.MemoryMb)];
            if (e.CpuPercent > 20)
                row.DefaultCellStyle.ForeColor = AppTheme.Warning;
            else if (e.CpuPercent > 5)
                row.DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
        }
        _grid.ResumeLayout(false);
        _countLabel.Text = $"{entries.Count} processes";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _svc.Dispose(); }
        base.Dispose(disposing);
    }
}
