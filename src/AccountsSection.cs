namespace MultiRoblox;

/// <summary>
/// The "Accounts" part of the main window: saved accounts, a game box, and Launch. Each selected
/// account starts the game in its own Roblox client, one after another.
/// </summary>
internal sealed class AccountsSection : TableLayoutPanel
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan GapBetweenLaunches = TimeSpan.FromSeconds(4);

    private readonly Func<bool> _multiInstanceActive;
    private readonly Func<Icon?> _icon;
    private readonly AppData _data;
    private readonly Dictionary<string, AccountWindow> _windows = new();

    private readonly ListBox _list;
    private readonly TextBox _game;
    private readonly Button _add;
    private readonly Button _remove;
    private readonly Button _browse;
    private readonly Button _launch;
    private readonly Label _empty;
    private bool _busy;

    public AccountsSection(int width, Func<bool> multiInstanceActive, Func<Icon?> icon)
    {
        _multiInstanceActive = multiInstanceActive;
        _icon = icon;

        var (data, readCleanly) = AccountStore.Load();
        _data = data;
        if (readCleanly)
        {
            AccountStore.DeleteOrphanProfiles(_data);
        }

        ColumnCount = 1;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0, 0, 0, 12);

        var header = new Label
        {
            Text = "Accounts",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 2),
        };

        var explain = new Label
        {
            Text = "Sign in to each account once. Select one or more (Ctrl+click), paste a game link, and press Launch: " +
                   "every account gets its own Roblox window.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(0, 0, 0, 6),
        };

        _list = new ListBox
        {
            SelectionMode = SelectionMode.MultiExtended,
            IntegralHeight = false,
            Size = new Size(width, 112),
            Margin = new Padding(0),
        };
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += (_, _) => BrowseSelected();

        _empty = new Label
        {
            Text = "No accounts yet. Press \"Add account\" to sign in to your first one.",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(0, 4, 0, 0),
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 10),
        };
        _add = MakeButton("Add account");
        _remove = MakeButton("Remove");
        _browse = MakeButton("Open roblox.com");
        _add.Click += async (_, _) => await AddAccountAsync();
        _remove.Click += (_, _) => RemoveSelected();
        _browse.Click += (_, _) => BrowseSelected();
        buttons.Controls.Add(_add);
        buttons.Controls.Add(_remove);
        buttons.Controls.Add(_browse);

        var gameLabel = new Label
        {
            Text = "Game link or place ID",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2),
        };
        _game = new TextBox
        {
            Width = width,
            PlaceholderText = "https://www.roblox.com/games/…  or  1818",
            Text = _data.LastGame,
            Margin = new Padding(0, 0, 0, 8),
        };

        _launch = new Button
        {
            Text = "Launch selected accounts",
            Size = new Size(width, 38),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Margin = new Padding(0),
        };
        _launch.Click += async (_, _) => await LaunchSelectedAsync();

        Controls.Add(header);
        Controls.Add(explain);
        Controls.Add(_list);
        Controls.Add(_empty);
        Controls.Add(buttons);
        Controls.Add(gameLabel);
        Controls.Add(_game);
        Controls.Add(_launch);

        ReloadList();
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Padding = new Padding(6, 2, 6, 2),
        Margin = new Padding(0, 0, 6, 0),
    };

    private List<SavedAccount> SelectedAccounts => _list.SelectedItems.Cast<SavedAccount>().ToList();

    private void ReloadList(SavedAccount? select = null)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var account in _data.Accounts)
        {
            _list.Items.Add(account);
        }

        if (select != null)
        {
            _list.SelectedItem = select;
        }
        else if (_list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }

        _list.EndUpdate();
        _empty.Visible = _data.Accounts.Count == 0;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int selected = _list.SelectedItems.Count;
        _add.Enabled = !_busy;
        _remove.Enabled = !_busy && selected > 0;
        _browse.Enabled = !_busy && selected == 1;
        _launch.Enabled = !_busy && selected > 0;
        _launch.Text = _busy
            ? "Launching…"
            : selected > 1 ? $"Launch {selected} accounts" : "Launch selected account";
    }

    private bool TrySave()
    {
        try
        {
            AccountStore.Save(_data);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(FindForm(), $"Couldn't save your accounts:\n{ex.Message}", "MultiRoblox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private AccountWindow OpenWindow(SavedAccount account)
    {
        if (_windows.TryGetValue(account.Id, out var existing) && !existing.IsDisposed)
        {
            if (existing.WindowState == FormWindowState.Minimized)
            {
                existing.WindowState = FormWindowState.Normal;
            }

            existing.Activate();
            return existing;
        }

        var window = new AccountWindow(account, signInOnly: false, _icon());
        window.FormClosed += (_, _) => _windows.Remove(account.Id);
        _windows[account.Id] = window;
        window.Show();
        return window;
    }

    private async Task AddAccountAsync()
    {
        var account = new SavedAccount { AddedUtc = DateTime.UtcNow };
        var window = new AccountWindow(account, signInOnly: true, _icon());
        window.Show();

        var user = await window.SignedIn;
        if (user is null)
        {
            AccountStore.TryDeleteProfile(account.Id);
            return;
        }

        var duplicate = _data.Accounts.FirstOrDefault(a => a.UserId == user.Id);
        if (duplicate != null)
        {
            AccountStore.TryDeleteProfile(account.Id);
            MessageBox.Show(FindForm(), $"{duplicate} is already in your list.", "MultiRoblox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            ReloadList(duplicate);
            return;
        }

        account.UserId = user.Id;
        account.Username = user.Name;
        account.DisplayName = user.DisplayName;
        _data.Accounts.Add(account);
        TrySave();
        ReloadList(account);
    }

    private void RemoveSelected()
    {
        var selected = SelectedAccounts;
        if (selected.Count == 0)
        {
            return;
        }

        string names = string.Join("\n", selected.Select(a => "  " + a));
        var answer = MessageBox.Show(
            FindForm(),
            $"Remove these accounts from MultiRoblox?\n\n{names}\n\nThis deletes their saved sign-in on this PC. The Roblox accounts themselves aren't affected.",
            "MultiRoblox",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);
        if (answer != DialogResult.OK)
        {
            return;
        }

        foreach (var account in selected)
        {
            if (_windows.TryGetValue(account.Id, out var window))
            {
                window.Close();
            }

            _data.Accounts.Remove(account);
            AccountStore.TryDeleteProfile(account.Id);
        }

        TrySave();
        ReloadList();
    }

    private void BrowseSelected()
    {
        if (_busy || SelectedAccounts is not [var account])
        {
            return;
        }

        OpenWindow(account).Browse(RobloxWeb.HomeUrl);
    }

    private async Task LaunchSelectedAsync()
    {
        var selected = SelectedAccounts;
        if (selected.Count == 0 || _busy)
        {
            return;
        }

        if (!RobloxWeb.TryMakeLaunchUrl(_game.Text, out string url, out string error))
        {
            MessageBox.Show(FindForm(), error, "MultiRoblox", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _game.Focus();
            return;
        }

        if (!_multiInstanceActive() && (selected.Count > 1 || RobloxProcesses.Count() > 0))
        {
            var answer = MessageBox.Show(
                FindForm(),
                "Multi-instance isn't active yet, so only one Roblox window can stay open: each new one will close the last.\n\n" +
                "Close Roblox, wait for the button above to say \"Multi-instance: ON\", then launch. Launch anyway?",
                "MultiRoblox",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
            {
                return;
            }
        }

        _data.LastGame = _game.Text.Trim();
        TrySave();

        _busy = true;
        UpdateButtons();
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0)
                {
                    // Give the previous client a moment to start before the next one launches.
                    await Task.Delay(GapBetweenLaunches);
                }

                var launch = OpenWindow(selected[i]).LaunchAsync(url);
                await Task.WhenAny(launch, Task.Delay(LaunchTimeout));
            }
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }
}
