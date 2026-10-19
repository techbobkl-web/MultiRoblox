namespace MultiRoblox;

/// <summary>
/// Small window + tray icon. Closing the window hides it to the tray; multi-instance stays on
/// until you turn it off or choose Exit from the tray menu.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly Color OnColor = Color.FromArgb(46, 160, 67);
    private static readonly Color OffColor = Color.FromArgb(110, 118, 129);
    private static readonly Color WaitColor = Color.FromArgb(191, 135, 0);

    private readonly RobloxSingletonLock _lock = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private readonly Icon _iconOn = AppIcons.Create(OnColor);
    private readonly Icon _iconOff = AppIcons.Create(OffColor);
    private readonly Icon _iconWait = AppIcons.Create(WaitColor);

    private readonly CheckBox _toggle;
    private readonly Label _status;
    private readonly Label _count;
    private readonly Button _closeAll;
    private readonly CheckBox _startup;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _trayToggle;
    private readonly ToolStripMenuItem _trayCloseAll;

    /// <summary>What the user asked for. The lock itself may still be pending (Roblox already open).</summary>
    private bool _wantOn = true;
    private RobloxSingletonLock.Result? _lastAttempt;
    private int _robloxCount;
    private bool _allowShow;
    private bool _exiting;
    private bool _closingRoblox;
    private bool _shownTrayHint;
    private bool _updatingUi;

    public MainForm(bool startMinimized)
    {
        _allowShow = !startMinimized;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75F);
        Text = "MultiRoblox";
        Icon = _iconOff;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        const int width = 340;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        var title = new Label
        {
            Text = "Multiple Roblox clients",
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        };

        _toggle = new CheckBox
        {
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Size = new Size(width, 46),
            Margin = new Padding(0),
            Cursor = Cursors.Hand,
        };
        _toggle.FlatAppearance.BorderSize = 0;
        _toggle.CheckedChanged += (_, _) =>
        {
            if (!_updatingUi)
            {
                SetWanted(_toggle.Checked);
            }
        };

        _status = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(0, 10, 0, 12),
        };

        var countRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12),
        };
        _count = new Label
        {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 12, 0),
        };
        _closeAll = new Button
        {
            Text = "Close all Roblox",
            AutoSize = true,
            Padding = new Padding(6, 2, 6, 2),
            Margin = new Padding(0),
        };
        _closeAll.Click += async (_, _) => await CloseAllRobloxAsync();
        countRow.Controls.Add(_count);
        countRow.Controls.Add(_closeAll);

        _startup = new CheckBox
        {
            Text = "Start with Windows (hidden in the tray)",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
        };
        _startup.CheckedChanged += (_, _) =>
        {
            if (_updatingUi)
            {
                return;
            }

            try
            {
                StartWithWindows.Set(_startup.Checked);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                MessageBox.Show(this, $"Couldn't change the startup setting:\n{ex.Message}", "MultiRoblox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _updatingUi = true;
                _startup.Checked = StartWithWindows.IsEnabled;
                _updatingUi = false;
            }
        };

        var hint = new Label
        {
            Text = "Turn this on before opening Roblox, then join games from the website or Bloxstrap as usual. " +
                   "Closing this window keeps it running in the tray.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(0),
        };

        layout.Controls.Add(title);
        layout.Controls.Add(_toggle);
        layout.Controls.Add(_status);
        layout.Controls.Add(countRow);
        layout.Controls.Add(_startup);
        layout.Controls.Add(hint);
        Controls.Add(layout);

        _trayToggle = new ToolStripMenuItem("Multi-instance", null, (_, _) => SetWanted(!_wantOn));
        _trayCloseAll = new ToolStripMenuItem("Close all Roblox", null, async (_, _) => await CloseAllRobloxAsync());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open MultiRoblox", null, (_, _) => ShowFromTray()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(_trayToggle);
        menu.Items.Add(_trayCloseAll);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        _tray = new NotifyIcon
        {
            Icon = _iconOff,
            Text = "MultiRoblox",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowFromTray();
            }
        };

        ResumeLayout(performLayout: true);

        // Create the window handle now, even when starting hidden, so BeginInvoke works and the
        // lock is taken on this (UI) thread right away.
        _ = Handle;

        try
        {
            StartWithWindows.RefreshPathIfEnabled();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Not fatal; the checkbox just reflects whatever is there.
        }

        _updatingUi = true;
        _startup.Checked = SafeIsStartupEnabled();
        _updatingUi = false;

        _timer.Tick += (_, _) => RefreshState(tryAcquire: true);
        _timer.Start();
        RefreshState(tryAcquire: true);
    }

    public void ShowFromTray()
    {
        _allowShow = true;
        Show();
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Activate();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Lets the app start straight into the tray (--minimized) without flashing the window.
        base.SetVisibleCore(_allowShow && value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_exiting)
        {
            e.Cancel = true;
            Hide();
            if (!_shownTrayHint)
            {
                _shownTrayHint = true;
                _tray.ShowBalloonTip(
                    4000,
                    "MultiRoblox is still running",
                    "It keeps multi-instance on from the tray. Right-click the icon and choose Exit to quit.",
                    ToolTipIcon.Info);
            }

            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _lock.Release();
        _tray.Visible = false;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _lock.Dispose();
            _tray.Dispose();
            _iconOn.Dispose();
            _iconOff.Dispose();
            _iconWait.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetWanted(bool on)
    {
        _wantOn = on;
        if (!on)
        {
            _lock.Release();
            _lastAttempt = null;
        }

        RefreshState(tryAcquire: on);
    }

    private void RefreshState(bool tryAcquire)
    {
        _robloxCount = RobloxProcesses.Count();

        // The lock has to exist before the first client starts. If one is already open we wait,
        // and grab it automatically the moment the user has closed every client.
        if (tryAcquire && _wantOn && !_lock.IsHeld && _robloxCount == 0)
        {
            _lastAttempt = _lock.TryAcquire();
        }

        UpdateUi();
    }

    private void UpdateUi()
    {
        _updatingUi = true;
        try
        {
            string status;
            string trayText;
            Color color;
            Icon icon;

            if (_lock.IsHeld)
            {
                status = "Multi-instance active. Open as many Roblox clients as you like.";
                trayText = "MultiRoblox: multi-instance ON";
                color = OnColor;
                icon = _iconOn;
            }
            else if (!_wantOn)
            {
                status = "Multi-instance off. Roblox is back to one client at a time.";
                trayText = "MultiRoblox: multi-instance OFF";
                color = OffColor;
                icon = _iconOff;
            }
            else if (_robloxCount > 0)
            {
                status = "Roblox already running — close it first. Multi-instance turns on by itself once every Roblox window is closed.";
                trayText = "MultiRoblox: waiting — close Roblox first";
                color = WaitColor;
                icon = _iconWait;
            }
            else if (_lastAttempt == RobloxSingletonLock.Result.HeldByOtherProgram)
            {
                status = "Another program is holding the Roblox lock (another multi-instance tool, or Bloxstrap's " +
                         "multi-instance setting). Close it, or just use that one. Retrying…";
                trayText = "MultiRoblox: lock held by another program";
                color = WaitColor;
                icon = _iconWait;
            }
            else
            {
                status = "Couldn't take the Roblox lock" + (_lock.LastError is { } err ? $": {err}" : ".") + " Retrying…";
                trayText = "MultiRoblox: couldn't take the lock";
                color = WaitColor;
                icon = _iconWait;
            }

            _toggle.Checked = _wantOn;
            _toggle.Text = _wantOn ? (_lock.IsHeld ? "Multi-instance: ON" : "Multi-instance: ON (waiting)") : "Multi-instance: OFF";
            _toggle.BackColor = color;
            _toggle.FlatAppearance.CheckedBackColor = color;
            _toggle.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.15f);
            _toggle.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.05f);

            _status.Text = status;
            _status.ForeColor = color == OffColor ? SystemColors.ControlText : ControlPaint.Dark(color, 0.1f);

            _count.Text = _robloxCount == 1 ? "1 Roblox client running" : $"{_robloxCount} Roblox clients running";
            _closeAll.Enabled = _robloxCount > 0 && !_closingRoblox;
            _trayCloseAll.Enabled = _closeAll.Enabled;
            _trayToggle.Checked = _wantOn;

            Icon = icon;
            _tray.Icon = icon;
            _tray.Text = trayText;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private async Task CloseAllRobloxAsync()
    {
        int count = RobloxProcesses.Count();
        if (count == 0 || _closingRoblox)
        {
            return;
        }

        var answer = MessageBox.Show(
            Visible ? this : null,
            $"Close {count} Roblox client{(count == 1 ? "" : "s")}? Anything unsaved in those games may be lost.",
            "MultiRoblox",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (answer != DialogResult.OK)
        {
            return;
        }

        _closingRoblox = true;
        UpdateUi();
        try
        {
            var (_, failed) = await Task.Run(() => RobloxProcesses.CloseAll());
            if (failed > 0)
            {
                MessageBox.Show(
                    Visible ? this : null,
                    $"{failed} Roblox client{(failed == 1 ? "" : "s")} couldn't be closed. Try closing them from Task Manager.",
                    "MultiRoblox",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _closingRoblox = false;
            RefreshState(tryAcquire: true);
        }
    }

    private void ExitApp()
    {
        _exiting = true;
        _timer.Stop();
        _lock.Release();
        _tray.Visible = false;
        Application.Exit();
    }

    private static bool SafeIsStartupEnabled()
    {
        try
        {
            return StartWithWindows.IsEnabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
