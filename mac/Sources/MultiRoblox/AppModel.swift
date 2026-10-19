import AppKit
import SwiftUI

@MainActor
final class AppModel: ObservableObject {
    @Published private(set) var accounts: [SavedAccount]
    @Published var selection = Set<UUID>()
    @Published var game: String
    @Published var multiInstance: Bool {
        didSet { persist() }
    }
    @Published private(set) var clientCount = 0
    @Published private(set) var robloxInstalled = RobloxClient.isInstalled
    @Published private(set) var busy = false
    @Published var alert: AlertMessage?

    struct AlertMessage: Identifiable {
        let id = UUID()
        let title: String
        let message: String
    }

    private var browsers: [UUID: AccountBrowser] = [:]
    private var timer: Timer?

    private static let launchTimeout: Duration = .seconds(45)
    private static let gapBetweenLaunches: Duration = .seconds(4)

    init() {
        let data = AccountStore.load()
        accounts = data.accounts
        game = data.lastGame
        multiInstance = data.multiInstance
        selection = Set(data.accounts.prefix(1).map(\.id))

        refresh()
        timer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.refresh() }
        }
    }

    var statusText: String {
        if !robloxInstalled {
            return "Roblox isn't installed. Get it from roblox.com/download, then open it once."
        }
        return multiInstance
            ? "Multi-instance on. Every launch from MultiRoblox opens its own Roblox window."
            : "Multi-instance off. Launches go to the Roblox window that's already open, like normal."
    }

    func refresh() {
        clientCount = RobloxClient.runningClientCount()
        robloxInstalled = RobloxClient.isInstalled
    }

    // MARK: - Accounts

    func addAccount() async {
        let id = UUID()
        var browser: AccountBrowser? = AccountBrowser(accountID: id, label: "New account", signInOnly: true) { _ in }
        let signedIn = await browser?.signIn()
        browser = nil // release the web view so its data store can be deleted if needed
        guard let user = signedIn else {
            await AccountStore.deleteWebsiteData(for: id)
            return
        }

        if let existing = accounts.first(where: { $0.userId == user.id }) {
            await AccountStore.deleteWebsiteData(for: id)
            selection = [existing.id]
            alert = AlertMessage(title: "Already added", message: "\(existing.label) is already in your list.")
            return
        }

        let account = SavedAccount(id: id, userId: user.id, username: user.name, displayName: user.displayName, added: Date())
        accounts.append(account)
        selection = [account.id]
        persist()
    }

    func removeSelected() async {
        let removing = accounts.filter { selection.contains($0.id) }
        guard !removing.isEmpty else { return }

        let confirm = NSAlert()
        confirm.messageText = removing.count == 1 ? "Remove \(removing[0].label)?" : "Remove \(removing.count) accounts?"
        confirm.informativeText = "This deletes their saved sign-in on this Mac. The Roblox accounts themselves aren't affected."
        confirm.addButton(withTitle: "Remove")
        confirm.addButton(withTitle: "Cancel")
        guard confirm.runModal() == .alertFirstButtonReturn else { return }

        for account in removing {
            browsers[account.id]?.close()
            browsers[account.id] = nil
        }
        accounts.removeAll { selection.contains($0.id) }
        selection = []
        persist()

        for account in removing {
            await AccountStore.deleteWebsiteData(for: account.id)
        }
    }

    func browseSelected() {
        guard selection.count == 1, let account = accounts.first(where: { selection.contains($0.id) }) else { return }
        browser(for: account).browse()
    }

    // MARK: - Launching

    func launchSelected() async {
        let selected = accounts.filter { selection.contains($0.id) }
        guard !selected.isEmpty, !busy else { return }

        let page: URL
        switch RobloxLinks.launchPage(for: game) {
        case .success(let url): page = url
        case .failure(let error):
            alert = AlertMessage(title: "Which game?", message: error.localizedDescription)
            return
        }

        if !robloxInstalled {
            alert = AlertMessage(title: "Roblox isn't installed", message: RobloxClient.LaunchError.notInstalled.localizedDescription)
            return
        }

        if !multiInstance && (selected.count > 1 || clientCount > 0) {
            let warn = NSAlert()
            warn.messageText = "Multi-instance is off"
            warn.informativeText = "Only one Roblox window can be open, so each launch replaces the last. Turn multi-instance on to play several accounts at once. Launch anyway?"
            warn.addButton(withTitle: "Launch Anyway")
            warn.addButton(withTitle: "Cancel")
            guard warn.runModal() == .alertFirstButtonReturn else { return }
        }

        persist()
        busy = true
        defer { busy = false }

        for (index, account) in selected.enumerated() {
            if index > 0 {
                // Give the previous client a moment to start before the next one launches.
                try? await Task.sleep(for: Self.gapBetweenLaunches)
            }

            let browser = browser(for: account)
            await withTaskGroup(of: Void.self) { group in
                group.addTask { @MainActor in _ = await browser.launch(page) }
                group.addTask { @MainActor in
                    // Don't wait forever (e.g. the account needs signing in again): move on.
                    try? await Task.sleep(for: Self.launchTimeout)
                    if !Task.isCancelled { browser.stopWaiting() }
                }
                await group.next()
                group.cancelAll()
            }
        }
        refresh()
    }

    func closeAllRoblox() async {
        let count = RobloxClient.runningClientCount()
        guard count > 0 else { return }

        let confirm = NSAlert()
        confirm.messageText = "Close \(count) Roblox window\(count == 1 ? "" : "s")?"
        confirm.informativeText = "Anything unsaved in those games may be lost."
        confirm.addButton(withTitle: "Close All")
        confirm.addButton(withTitle: "Cancel")
        guard confirm.runModal() == .alertFirstButtonReturn else { return }

        busy = true
        let result = await RobloxClient.closeAll()
        busy = false
        refresh()
        if result.failed > 0 {
            alert = AlertMessage(title: "Some didn't close", message: "\(result.failed) Roblox window(s) couldn't be closed. Try Force Quit (⌥⌘Esc).")
        }
    }

    // MARK: - Private

    private func browser(for account: SavedAccount) -> AccountBrowser {
        if let existing = browsers[account.id] {
            return existing
        }

        let browser = AccountBrowser(accountID: account.id, label: account.label, signInOnly: false) { [weak self] link in
            try RobloxClient.launch(link, newInstance: self?.multiInstance ?? true)
        }
        browser.onClose = { [weak self] in self?.browsers[account.id] = nil }
        browsers[account.id] = browser
        return browser
    }

    private func persist() {
        let data = AppData(accounts: accounts, lastGame: game.trimmingCharacters(in: .whitespacesAndNewlines), multiInstance: multiInstance)
        do {
            try AccountStore.save(data)
        } catch {
            alert = AlertMessage(title: "Couldn't save", message: error.localizedDescription)
        }
    }
}
