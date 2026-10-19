import Foundation
import WebKit

/// One saved Roblox account. Its sign-in isn't stored here: it lives in the account's own
/// WebKit website-data store (WKWebsiteDataStore(forIdentifier: id)), like a separate browser profile.
struct SavedAccount: Codable, Identifiable, Hashable {
    var id: UUID
    var userId: Int64
    var username: String
    var displayName: String
    var added: Date

    var label: String {
        if username.isEmpty { return "New account" }
        if !displayName.isEmpty && displayName.caseInsensitiveCompare(username) != .orderedSame {
            return "\(displayName) (@\(username))"
        }
        return "@\(username)"
    }
}

struct AppData: Codable {
    var accounts: [SavedAccount] = []
    var lastGame: String = ""
    var multiInstance: Bool = true
}

/// ~/Library/Application Support/MultiRoblox/accounts.json (names and IDs only, no passwords or sessions).
enum AccountStore {
    static var folder: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("MultiRoblox", isDirectory: true)
    }

    private static var fileURL: URL { folder.appendingPathComponent("accounts.json") }

    static func load() -> AppData {
        guard let data = try? Data(contentsOf: fileURL) else { return AppData() }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return (try? decoder.decode(AppData.self, from: data)) ?? AppData()
    }

    static func save(_ appData: AppData) throws {
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        try encoder.encode(appData).write(to: fileURL, options: .atomic)
    }

    /// Deletes an account's website data (its sign-in) from this Mac.
    /// WebKit refuses while a just-closed window's web view is still being torn down, so retry briefly.
    @MainActor
    static func deleteWebsiteData(for id: UUID) async {
        for attempt in 0..<6 {
            if attempt > 0 { try? await Task.sleep(for: .milliseconds(500)) }
            do {
                try await WKWebsiteDataStore.remove(forIdentifier: id)
                return
            } catch {
                continue
            }
        }
    }
}
