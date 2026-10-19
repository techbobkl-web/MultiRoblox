import AppKit
import WebKit

struct RobloxUser: Decodable {
    let id: Int64
    let name: String
    let displayName: String
}

/// A small browser window signed in as one Roblox account, using that account's own isolated
/// WebKit data store. Games start through roblox.com's normal Play flow: when the site hands off
/// to a "roblox-player:" link, this window passes it to Roblox, so the client plays as this account.
///
/// The account's session cookie never leaves WebKit: sign-in is detected by asking roblox.com
/// "who am I" from inside the page.
@MainActor
final class AccountBrowser: NSObject, NSWindowDelegate, WKNavigationDelegate, WKUIDelegate {
    let accountID: UUID
    private var accountLabel: String
    private let signInOnly: Bool
    private let launcher: (URL) throws -> Void

    private let window: NSWindow
    private let webView: WKWebView
    private let banner = NSTextField(wrappingLabelWithString: "")

    private var signInTimer: Timer?
    private var checkingSignIn = false
    private var launchPage: URL?
    private var launchContinuation: CheckedContinuation<Bool, Never>?
    private var signInContinuation: CheckedContinuation<RobloxUser?, Never>?
    private var onLoginPage = false
    private var autoClose: DispatchWorkItem?

    var onClose: (() -> Void)?

    /// - Parameters:
    ///   - signInOnly: true to add a new account: shows the login page and closes once signed in.
    ///   - launcher: starts Roblox with the roblox-player link the site produced.
    init(accountID: UUID, label: String, signInOnly: Bool, launcher: @escaping (URL) throws -> Void) {
        self.accountID = accountID
        self.accountLabel = label
        self.signInOnly = signInOnly
        self.launcher = launcher

        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = WKWebsiteDataStore(forIdentifier: accountID)
        webView = WKWebView(frame: .zero, configuration: configuration)
        // Present as Safari so roblox.com shows its normal desktop site and Play button.
        webView.customUserAgent =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Safari/605.1.15"

        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 560, height: 780),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false)
        window.isReleasedWhenClosed = false
        window.title = signInOnly ? "Add a Roblox account" : "Roblox: \(label)"

        super.init()

        webView.navigationDelegate = self
        webView.uiDelegate = self
        window.delegate = self

        banner.font = .systemFont(ofSize: 12.5)
        banner.textColor = .labelColor
        let bannerBox = NSView()
        bannerBox.wantsLayer = true
        bannerBox.layer?.backgroundColor = NSColor.systemYellow.withAlphaComponent(0.18).cgColor
        banner.translatesAutoresizingMaskIntoConstraints = false
        bannerBox.addSubview(banner)

        let stack = NSStackView(views: [bannerBox, webView])
        stack.orientation = .vertical
        stack.spacing = 0
        stack.distribution = .fill
        NSLayoutConstraint.activate([
            banner.leadingAnchor.constraint(equalTo: bannerBox.leadingAnchor, constant: 12),
            banner.trailingAnchor.constraint(equalTo: bannerBox.trailingAnchor, constant: -12),
            banner.topAnchor.constraint(equalTo: bannerBox.topAnchor, constant: 8),
            banner.bottomAnchor.constraint(equalTo: bannerBox.bottomAnchor, constant: -8),
            bannerBox.widthAnchor.constraint(equalTo: stack.widthAnchor),
            webView.widthAnchor.constraint(equalTo: stack.widthAnchor),
        ])
        window.contentView = stack
        window.center()

        if signInOnly {
            setBanner("Sign in to the Roblox account you want to add. The sign-in stays on this Mac, in a browser profile used only for this account.")
        }
    }

    // MARK: - Public

    func show() {
        window.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    func close() {
        window.close()
    }

    /// Shows the login page and waits. Returns the account once signed in, or nil if the window was closed.
    func signIn() async -> RobloxUser? {
        show()
        webView.load(URLRequest(url: RobloxLinks.login))
        signInTimer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            Task { @MainActor in await self?.checkSignIn() }
        }
        return await withCheckedContinuation { signInContinuation = $0 }
    }

    /// Opens a roblox.com launch page; returns true once Roblox has been started as this account.
    func launch(_ page: URL) async -> Bool {
        finishLaunch(false)
        autoClose?.cancel()
        launchPage = page
        setBanner("Starting the game as \(accountLabel)… If it doesn't start in a few seconds, press the green Play button.")
        show()
        webView.load(URLRequest(url: page))
        return await withCheckedContinuation { launchContinuation = $0 }
    }

    /// Stops waiting for the current launch (the window stays open so the user can finish it).
    func stopWaiting() {
        finishLaunch(false)
    }

    func browse() {
        autoClose?.cancel()
        launchPage = nil
        setBanner("Signed in as \(accountLabel). Pick any game and press Play: it opens in its own Roblox window.")
        show()
        webView.load(URLRequest(url: RobloxLinks.home))
    }

    // MARK: - NSWindowDelegate

    func windowWillClose(_ notification: Notification) {
        signInTimer?.invalidate()
        signInTimer = nil
        autoClose?.cancel()
        webView.stopLoading()
        finishLaunch(false)
        finishSignIn(nil)
        onClose?()
    }

    // MARK: - WKNavigationDelegate

    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction) async -> WKNavigationActionPolicy {
        guard let url = navigationAction.request.url, let scheme = url.scheme?.lowercased() else {
            return .allow
        }
        switch scheme {
        case "roblox-player", "roblox":
            handOff(url)
            return .cancel
        case "http", "https", "about", "blob", "data":
            return .allow
        default:
            return .cancel
        }
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        guard !signInOnly, let url = webView.url else { return }

        if url.path.lowercased().hasPrefix("/login") {
            onLoginPage = true
            setBanner("\(accountLabel) has been signed out. Sign in again here" + (launchPage != nil ? " and the game will start." : "."))
        } else if onLoginPage {
            // Just signed back in: carry on with the launch that sent us to the login page.
            onLoginPage = false
            if let page = launchPage {
                webView.load(URLRequest(url: page))
            }
        }
    }

    // MARK: - WKUIDelegate

    /// Pop-ups (target=_blank, window.open) open in this same window and account.
    func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                 for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
        if let url = navigationAction.request.url {
            webView.load(URLRequest(url: url))
        }
        return nil
    }

    // MARK: - Private

    private func setBanner(_ text: String) {
        banner.stringValue = text
    }

    private func handOff(_ link: URL) {
        do {
            try launcher(link)
        } catch {
            setBanner(error.localizedDescription)
            finishLaunch(false)
            return
        }

        finishLaunch(true)
        if launchPage != nil {
            launchPage = nil
            setBanner("Roblox is starting as \(accountLabel). This window closes by itself.")
            let work = DispatchWorkItem { [weak self] in self?.close() }
            autoClose = work
            DispatchQueue.main.asyncAfter(deadline: .now() + 4, execute: work)
        } else {
            setBanner("Roblox is starting as \(accountLabel).")
        }
    }

    private func finishLaunch(_ launched: Bool) {
        launchContinuation?.resume(returning: launched)
        launchContinuation = nil
    }

    private func finishSignIn(_ user: RobloxUser?) {
        signInContinuation?.resume(returning: user)
        signInContinuation = nil
    }

    private func checkSignIn() async {
        guard !checkingSignIn, let url = webView.url,
              url.host?.lowercased() == "www.roblox.com",
              !url.path.lowercased().hasPrefix("/login")
        else { return }

        checkingSignIn = true
        defer { checkingSignIn = false }

        let script = """
            const response = await fetch("https://users.roblox.com/v1/users/authenticated", { credentials: "include" });
            return response.ok ? await response.text() : null;
            """
        guard let result = try? await webView.callAsyncJavaScript(script, contentWorld: .page),
              let json = result as? String,
              let user = try? JSONDecoder().decode(RobloxUser.self, from: Data(json.utf8))
        else { return }

        signInTimer?.invalidate()
        signInTimer = nil
        accountLabel = user.name
        finishSignIn(user)
        close()
    }
}
