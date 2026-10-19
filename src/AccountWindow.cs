using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MultiRoblox;

/// <summary>
/// A small Edge (WebView2) browser that is signed in as one Roblox account, using that account's
/// own profile folder. Games start through roblox.com's normal Play flow: when the site hands off
/// to the "roblox-player:" link, this window launches it, so the client opens as this account.
/// </summary>
internal sealed class AccountWindow : Form
{
    private readonly SavedAccount _account;
    private readonly bool _signInOnly;
    private readonly WebView2 _web;
    private readonly Label _banner;
    private readonly System.Windows.Forms.Timer _signInPoll = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _autoClose = new() { Interval = 4000 };
    private readonly TaskCompletionSource<RobloxUser?> _signedIn = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<bool>? _launch;
    private string? _pendingUrl;
    private string? _launchUrl;
    private bool _onLoginPage;
    private bool _checkingSignIn;

    /// <param name="signInOnly">True to add a new account: opens the login page and closes once signed in.</param>
    public AccountWindow(SavedAccount account, bool signInOnly, Icon? icon)
    {
        _account = account;
        _signInOnly = signInOnly;

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75F);
        Text = signInOnly ? "Add a Roblox account" : $"Roblox: {account}";
        if (icon != null)
        {
            Icon = icon;
        }

        Size = new Size(540, 760);
        StartPosition = FormStartPosition.CenterScreen;

        _banner = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 56,
            Padding = new Padding(12, 6, 12, 6),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.FromArgb(255, 246, 214),
            ForeColor = Color.FromArgb(80, 55, 0),
        };
        _web = new WebView2 { Dock = DockStyle.Fill };

        // Docking runs from the back of the z-order: the banner (added last) takes the top first.
        Controls.Add(_web);
        Controls.Add(_banner);

        _signInPoll.Tick += async (_, _) => await CheckSignInAsync();
        _autoClose.Tick += (_, _) =>
        {
            _autoClose.Stop();
            Close();
        };

        Load += async (_, _) => await InitAsync();
    }

    public SavedAccount Account => _account;

    /// <summary>Completes with the signed-in user (sign-in mode), or null if the window was closed first.</summary>
    public Task<RobloxUser?> SignedIn => _signedIn.Task;

    /// <summary>Opens a roblox.com launch page; completes true once Roblox has been started as this account.</summary>
    public Task<bool> LaunchAsync(string url)
    {
        _launch?.TrySetResult(false);
        _launch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _launchUrl = url;
        _autoClose.Stop();
        SetBanner($"Starting the game as {_account}… If it doesn't start in a few seconds, press the green Play button.");
        Navigate(url);
        return _launch.Task;
    }

    public void Browse(string url)
    {
        _launchUrl = null;
        _autoClose.Stop();
        SetBanner($"Signed in as {_account}. Pick any game and press Play: it opens in its own Roblox window.");
        Navigate(url);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _signInPoll.Stop();
        _autoClose.Stop();
        _signedIn.TrySetResult(null);
        _launch?.TrySetResult(false);
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _signInPoll.Dispose();
            _autoClose.Dispose();
            _web.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Navigate(string url)
    {
        if (_web.CoreWebView2 is { } core)
        {
            core.Navigate(url);
        }
        else
        {
            _pendingUrl = url;
        }
    }

    private void SetBanner(string text) => _banner.Text = text;

    private async Task InitAsync()
    {
        try
        {
            Directory.CreateDirectory(_account.ProfileFolder);
            var environment = await CoreWebView2Environment.CreateAsync(null, _account.ProfileFolder);
            await _web.EnsureCoreWebView2Async(environment);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                this,
                "Accounts need the Microsoft Edge WebView2 Runtime, which isn't installed on this PC.\n\n" +
                "Get the \"Evergreen Bootstrapper\" from developer.microsoft.com/microsoft-edge/webview2, install it, and try again.",
                "MultiRoblox",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Close();
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't open the account browser:\n{ex.Message}", "MultiRoblox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        if (IsDisposed)
        {
            return;
        }

        var core = _web.CoreWebView2;
        core.Settings.IsStatusBarEnabled = false;
        core.NewWindowRequested += (_, e) =>
        {
            // Keep everything in this window (and this account's profile).
            e.Handled = true;
            core.Navigate(e.Uri);
        };
        core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
        core.NavigationCompleted += (_, _) => OnNavigationCompleted();

        if (_signInOnly)
        {
            SetBanner("Sign in to the Roblox account you want to add. The sign-in stays on this PC, in a browser profile used only for this account.");
            core.Navigate(RobloxWeb.LoginUrl);
            _signInPoll.Start();
        }
        else if (_pendingUrl != null)
        {
            core.Navigate(_pendingUrl);
            _pendingUrl = null;
        }
    }

    private void OnNavigationCompleted()
    {
        if (_signInOnly || _web.Source is not { } source)
        {
            return;
        }

        bool onLogin = source.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase);
        if (onLogin)
        {
            _onLoginPage = true;
            SetBanner($"{_account} has been signed out. Sign in again here" + (_launchUrl != null ? " and the game will start." : "."));
        }
        else if (_onLoginPage)
        {
            // Just signed back in: carry on with the launch that sent us to the login page.
            _onLoginPage = false;
            if (_launchUrl != null)
            {
                _web.CoreWebView2.Navigate(_launchUrl);
            }
        }
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        bool isRoblox = e.Uri.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
                        || e.Uri.StartsWith("roblox:", StringComparison.OrdinalIgnoreCase);
        if (!isRoblox)
        {
            return; // Anything else gets WebView2's normal "open this app?" prompt.
        }

        // Launch it ourselves, without the browser's confirmation prompt.
        e.Cancel = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            SetBanner("Couldn't start Roblox. Is Roblox (or Bloxstrap) installed on this PC?");
            _launch?.TrySetResult(false);
            return;
        }

        _launch?.TrySetResult(true);
        if (_launchUrl != null)
        {
            _launchUrl = null;
            SetBanner($"Roblox is starting as {_account}. This window closes by itself.");
            _autoClose.Start();
        }
        else
        {
            SetBanner($"Roblox is starting as {_account}.");
        }
    }

    private async Task CheckSignInAsync()
    {
        if (_checkingSignIn || _web.CoreWebView2 is null)
        {
            return;
        }

        _checkingSignIn = true;
        try
        {
            var cookies = await _web.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
            var session = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY" && c.Value.Length > 0);
            if (session is null)
            {
                return;
            }

            var user = await RobloxWeb.GetSignedInUserAsync(session.Value);
            if (user is null)
            {
                return;
            }

            _signInPoll.Stop();
            _signedIn.TrySetResult(user);
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Try again on the next tick.
        }
        finally
        {
            _checkingSignIn = false;
        }
    }
}
