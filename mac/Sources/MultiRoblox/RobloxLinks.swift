import Foundation

enum RobloxLinks {
    static let login = URL(string: "https://www.roblox.com/login")!
    static let home = URL(string: "https://www.roblox.com/home")!

    /// Turns what the user pasted into a roblox.com page that starts the game when opened.
    /// Accepts a place ID, a game link, a private-server link, or a share link. Only roblox.com
    /// addresses are allowed, so an account window never opens some other site.
    static func launchPage(for input: String) -> Result<URL, LinkError> {
        var text = input.trimmingCharacters(in: .whitespacesAndNewlines)
        if text.isEmpty { return .failure(.empty) }

        if text.allSatisfy(\.isASCIIDigit) {
            return .success(URL(string: "https://www.roblox.com/games/start?placeId=\(text)")!)
        }

        if !text.contains("://") { text = "https://" + text }
        guard let components = URLComponents(string: text),
              components.scheme?.lowercased() == "https",
              let host = components.host?.lowercased(),
              host == "roblox.com" || host.hasSuffix(".roblox.com"),
              let url = components.url
        else {
            return .failure(.notRoblox)
        }

        // roblox.com/games/123/Name only shows the game page; /games/start?placeId= starts it.
        // Private-server, share and start links already start the game by themselves.
        let segments = components.path.split(separator: "/")
        let isPrivateServer = components.queryItems?.contains { $0.name.caseInsensitiveCompare("privateServerLinkCode") == .orderedSame } ?? false
        if segments.count >= 2,
           segments[0].caseInsensitiveCompare("games") == .orderedSame,
           segments[1].allSatisfy(\.isASCIIDigit),
           !isPrivateServer {
            return .success(URL(string: "https://www.roblox.com/games/start?placeId=\(segments[1])")!)
        }
        return .success(url)
    }

    enum LinkError: LocalizedError {
        case empty
        case notRoblox

        var errorDescription: String? {
            switch self {
            case .empty: return "Paste a Roblox game link or a place ID first."
            case .notRoblox: return "That doesn't look like a roblox.com link. Paste a game link (roblox.com/games/…) or a place ID."
            }
        }
    }
}

private extension Character {
    var isASCIIDigit: Bool { isASCII && isNumber }
}
