using BaumAdminTool.Services;

namespace BaumAdminTool.Controls;

internal sealed class BackupTab : Panel
{
    private readonly LiveOutputPanel _output;
    private readonly TextBox         _destBox;
    private readonly CheckBox        _mirrorChk;
    private readonly Button          _startBtn;
    private readonly Panel           _sourcePanel;
    private System.Diagnostics.Process? _roboCopyProc;

    private static readonly (string label, string path)[] _sources =
    {
        ("Desktop",   Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
        ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
        ("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")),
        ("Pictures",  Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
        ("Music",     Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
        ("Videos",    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
    };

    public BackupTab(LiveOutputPanel output)
    {
        _output   = output;
        BackColor = AppTheme.BgMain;
        Dock      = DockStyle.Fill;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BgMain };
        AppTheme.ApplyDarkScrollBar(scroll);

        int y = 20;

        // Source folders
        var srcHdr = AppTheme.MakeLabel("Source Folders", AppTheme.FontHeader, AppTheme.Accent);
        srcHdr.Location = new Point(20, y); scroll.Controls.Add(srcHdr); y += 28;

        _sourcePanel = new Panel { Left = 20, Top = y, Width = 400, Height = _sources.Length * 28 };
        for (int i = 0; i < _sources.Length; i++)
        {
            var (lbl, path) = _sources[i];
            var chk = new CheckBox
            {
                Text      = $"{lbl}  ({path})",
                ForeColor = AppTheme.TextPrimary,
                Font      = AppTheme.FontBody,
                Checked   = true,
                AutoSize  = true,
                Location  = new Point(0, i * 28),
                BackColor = Color.Transparent,
                Tag       = path,
            };
            _sourcePanel.Controls.Add(chk);
        }
        scroll.Controls.Add(_sourcePanel);
        y += _sourcePanel.Height + 20;

        // Destination
        var dstHdr = AppTheme.MakeLabel("Destination Folder", AppTheme.FontHeader, AppTheme.Accent);
        dstHdr.Location = new Point(20, y); scroll.Controls.Add(dstHdr); y += 28;

        _destBox = new TextBox
        {
            Left      = 20,
            Top       = y,
            Width     = 500,
            Height    = 26,
            BackColor = AppTheme.BgCard,
            ForeColor = AppTheme.TextPrimary,
            Font      = AppTheme.FontBody,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Select backup destination...",
        };
        scroll.Controls.Add(_destBox);

        var browseBtn = AppTheme.MakeButton("Browse...");
        browseBtn.SetBounds(528, y, 90, 26);
        browseBtn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select backup destination" };
            if (dlg.ShowDialog() == DialogResult.OK)
                _destBox.Text = dlg.SelectedPath;
        };
        scroll.Controls.Add(browseBtn);
        y += 44;

        // Options
        _mirrorChk = new CheckBox
        {
            Text      = "Mirror mode  (deletes files in destination not in source — use with care)",
            ForeColor = AppTheme.Warning,
            Font      = AppTheme.FontSmall,
            AutoSize  = true,
            Location  = new Point(20, y),
            BackColor = Color.Transparent,
        };
        scroll.Controls.Add(_mirrorChk);
        y += 36;

        // Info box
        var info = AppTheme.MakeLabel(
            "Each checked folder is copied individually into its own sub-folder inside the destination.\n" +
            "RoboCopy /E copies all subfolders including empty ones.  /MIR also deletes destination files.",
            AppTheme.FontSmall, AppTheme.TextMuted);
        info.Location = new Point(20, y);
        info.MaximumSize = new Size(620, 0);
        info.AutoSize = true;
        scroll.Controls.Add(info);
        y += 52;

        // Start/Stop button
        _startBtn = AppTheme.MakeButton("▶  Start Backup", AppTheme.Accent);
        _startBtn.ForeColor = Color.White;
        _startBtn.SetBounds(20, y, 160, 36);
        _startBtn.Click += OnStartStop;
        scroll.Controls.Add(_startBtn);

        scroll.AutoScrollMinSize = new Size(0, y + 60);
        Controls.Add(scroll);
    }

    async void OnStartStop(object? sender, EventArgs e)
    {
        // Stop
        if (_roboCopyProc != null && !_roboCopyProc.HasExited)
        {
            _roboCopyProc.Kill(true);
            _roboCopyProc = null;
            _output.AppendWarning("⏹ Backup stopped by user");
            ResetBtn();
            return;
        }

        if (string.IsNullOrWhiteSpace(_destBox.Text))
        {
            MessageBox.Show("Please select a destination folder.", "BaumAdminTool",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = _sourcePanel.Controls.OfType<CheckBox>()
            .Where(c => c.Checked && c.Tag is string)
            .Select(c => (string)c.Tag!)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Please select at least one source folder.", "BaumAdminTool",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _startBtn.Text      = "⏹  Stop Backup";
        _startBtn.BackColor = AppTheme.Danger;
        _startBtn.Enabled   = true;

        bool mirror = _mirrorChk.Checked;
        string dest = _destBox.Text;

        _output.AppendInfo($"▶ Backup started — {selected.Count} folder(s) → {dest}");

        foreach (var src in selected)
        {
            if (_roboCopyProc != null && _roboCopyProc.HasExited == false)
            {
                // already killed by Stop
                break;
            }
            var name    = Path.GetFileName(src);
            var dstPath = Path.Combine(dest, name);
            _output.AppendInfo($"  Copying: {src}  →  {dstPath}");

            await AdminActionService.RoboCopyAsync(src, dstPath, mirror,
                line => _output.Append(line),
                line => _output.AppendError(line),
                code =>
                {
                    // RoboCopy exit codes: 0/1 = success, ≥8 = error
                    if (code < 8) _output.AppendSuccess($"  ✔ {name} done (exit {code})");
                    else          _output.AppendError($"  ✘ {name} error (exit {code})");
                });
        }

        _output.AppendSuccess("✔ Backup job complete");
        ResetBtn();
    }

    void ResetBtn()
    {
        if (InvokeRequired) { Invoke(ResetBtn); return; }
        _startBtn.Text      = "▶  Start Backup";
        _startBtn.BackColor = AppTheme.Accent;
    }
}
