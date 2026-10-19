import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            multiInstanceSection
            Divider()
            accountsSection
            Divider()
            footer
        }
        .padding(20)
        .frame(width: 440)
        .alert(item: $model.alert) { message in
            Alert(title: Text(message.title), message: Text(message.message))
        }
    }

    private var multiInstanceSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Multiple Roblox clients")
                .font(.title2.bold())

            Toggle(isOn: $model.multiInstance) {
                Text("Multi-instance")
                    .font(.headline)
            }
            .toggleStyle(.switch)
            .disabled(!model.robloxInstalled)

            Text(model.statusText)
                .font(.callout)
                .foregroundStyle(model.robloxInstalled ? (model.multiInstance ? Color.green : Color.secondary) : Color.orange)
                .fixedSize(horizontal: false, vertical: true)

            HStack {
                Text(model.clientCount == 1 ? "1 Roblox client running" : "\(model.clientCount) Roblox clients running")
                    .monospacedDigit()
                Spacer()
                Button("Close all Roblox") {
                    Task { await model.closeAllRoblox() }
                }
                .disabled(model.clientCount == 0 || model.busy)
            }
        }
    }

    private var accountsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Accounts")
                .font(.headline)
            Text("Sign in to each account once. Select one or more (⌘-click), paste a game link, and press Launch: every account gets its own Roblox window.")
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            List(model.accounts, selection: $model.selection) { account in
                Text(account.label)
                    .tag(account.id)
            }
            .frame(height: 130)
            .overlay {
                if model.accounts.isEmpty {
                    Text("No accounts yet. Press “Add account” to sign in to your first one.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                        .padding()
                }
            }
            .contextMenu(forSelectionType: UUID.self) { _ in } primaryAction: { _ in
                model.browseSelected()
            }

            HStack {
                Button("Add account") {
                    Task { await model.addAccount() }
                }
                Button("Remove") {
                    Task { await model.removeSelected() }
                }
                .disabled(model.selection.isEmpty || model.busy)
                Button("Open roblox.com") {
                    model.browseSelected()
                }
                .disabled(model.selection.count != 1 || model.busy)
            }
            .disabled(model.busy)

            VStack(alignment: .leading, spacing: 4) {
                Text("Game link or place ID")
                TextField("https://www.roblox.com/games/…  or  1818", text: $model.game)
                    .textFieldStyle(.roundedBorder)
                    .onSubmit { Task { await model.launchSelected() } }
            }

            Button {
                Task { await model.launchSelected() }
            } label: {
                Text(launchTitle)
                    .frame(maxWidth: .infinity)
            }
            .controlSize(.large)
            .buttonStyle(.borderedProminent)
            .disabled(model.selection.isEmpty || model.busy)
            .keyboardShortcut(.defaultAction)
        }
    }

    private var footer: some View {
        Text("Launches from MultiRoblox open as new Roblox windows. Opening a game from Safari or Chrome still goes to the window that's already open.")
            .font(.footnote)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)
    }

    private var launchTitle: String {
        if model.busy { return "Launching…" }
        let count = model.selection.count
        return count > 1 ? "Launch \(count) accounts" : "Launch selected account"
    }
}
