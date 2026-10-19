import SwiftUI

@main
struct MultiRobloxApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var model = AppModel()

    var body: some Scene {
        Window("MultiRoblox", id: "main") {
            ContentView()
                .environmentObject(model)
        }
        .windowResizability(.contentSize)
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Diagnostics: `MultiRoblox --launch-test <roblox-player: link>` starts one extra client
        // through the same code path the account windows use, then exits.
        let arguments = CommandLine.arguments
        if let flag = arguments.firstIndex(of: "--launch-test"), flag + 1 < arguments.count,
           let link = URL(string: arguments[flag + 1]) {
            do {
                try RobloxClient.launch(link, newInstance: true)
                print("launched")
                exit(0)
            } catch {
                print("failed: \(error.localizedDescription)")
                exit(1)
            }
        }

        // Also needed when run straight from `swift run`, outside an .app bundle.
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }

    /// Account browser windows can stay open after the main window closes; quit with the main window.
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}
