import AppKit
import Darwin

/// Finds, counts, starts and closes Roblox clients (RobloxPlayer) on this Mac.
///
/// Why starting a second client needs care: Roblox.app sets `LSMultipleInstancesProhibited`, so
/// macOS's normal launch path (`open`, clicking a link, NSWorkspace) hands every game to the copy
/// that's already running. Roblox's own menu-bar helper starts the player binary directly with
/// `-protocolString <roblox-player: link>`; we do the same, which gives each launch its own
/// process. Nothing in Roblox.app is modified.
enum RobloxClient {
    static let bundleIdentifier = "com.roblox.RobloxPlayer"

    struct Running {
        let pid: pid_t
        /// The idle menu-bar copy Roblox keeps around (`-launchToTray`), not a game window.
        let isTrayHelper: Bool
    }

    enum LaunchError: LocalizedError {
        case notInstalled
        case spawnFailed(Int32)

        var errorDescription: String? {
            switch self {
            case .notInstalled:
                return "Roblox isn't installed. Get it from roblox.com/download, open it once, then try again."
            case .spawnFailed(let code):
                return "Couldn't start Roblox (\(String(cString: strerror(code))))."
            }
        }
    }

    static var appURL: URL? {
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleIdentifier) {
            return url
        }
        let fallback = URL(fileURLWithPath: "/Applications/Roblox.app")
        return FileManager.default.fileExists(atPath: fallback.path) ? fallback : nil
    }

    static var isInstalled: Bool { executableURL != nil }

    private static var executableURL: URL? {
        guard let app = appURL else { return nil }
        let exe = app.appendingPathComponent("Contents/MacOS/RobloxPlayer")
        return FileManager.default.isExecutableFile(atPath: exe.path) ? exe : nil
    }

    // MARK: - Launching

    /// - Parameter newInstance: true starts a separate client; false hands the link to macOS,
    ///   which sends it to the Roblox window that's already open (Roblox's normal behaviour).
    static func launch(_ link: URL, newInstance: Bool) throws {
        guard let exe = executableURL else { throw LaunchError.notInstalled }

        guard newInstance else {
            NSWorkspace.shared.open(link)
            return
        }

        try spawnDetached(exe.path, arguments: ["-protocolString", link.absoluteString])
    }

    /// posix_spawn with "responsibility disclaimed", so macOS treats the new client as its own
    /// app: Roblox asks for microphone/camera access under its own name, not MultiRoblox's.
    private static func spawnDetached(_ path: String, arguments: [String]) throws {
        var attributes: posix_spawnattr_t?
        posix_spawnattr_init(&attributes)
        defer { posix_spawnattr_destroy(&attributes) }

        // Own process group: quitting MultiRoblox never takes the clients down with it.
        posix_spawnattr_setflags(&attributes, Int16(POSIX_SPAWN_SETPGROUP))
        posix_spawnattr_setpgroup(&attributes, 0)
        disclaimResponsibility(&attributes)

        var fileActions: posix_spawn_file_actions_t?
        posix_spawn_file_actions_init(&fileActions)
        defer { posix_spawn_file_actions_destroy(&fileActions) }
        for fd in [STDIN_FILENO, STDOUT_FILENO, STDERR_FILENO] {
            posix_spawn_file_actions_addopen(&fileActions, fd, "/dev/null", fd == STDIN_FILENO ? O_RDONLY : O_WRONLY, 0)
        }

        let argv: [UnsafeMutablePointer<CChar>?] = ([path] + arguments).map { strdup($0) } + [nil]
        defer { argv.forEach { free($0) } }

        var pid: pid_t = 0
        let result = posix_spawn(&pid, path, &fileActions, &attributes, argv, environ)
        if result != 0 {
            throw LaunchError.spawnFailed(result)
        }
    }

    /// `responsibility_spawnattrs_setdisclaim` is what Terminal and other launchers use. It's looked
    /// up at runtime; if it's ever missing the launch still works, just without the disclaim.
    private static func disclaimResponsibility(_ attributes: inout posix_spawnattr_t?) {
        typealias SetDisclaim = @convention(c) (UnsafeMutablePointer<posix_spawnattr_t?>, Int32) -> Int32
        guard let symbol = dlsym(UnsafeMutableRawPointer(bitPattern: -2), "responsibility_spawnattrs_setdisclaim") else {
            return
        }
        let setDisclaim = unsafeBitCast(symbol, to: SetDisclaim.self)
        _ = setDisclaim(&attributes, 1)
    }

    // MARK: - Running clients

    static func running() -> [Running] {
        var pids = [pid_t](repeating: 0, count: 8192)
        let count = Int(proc_listallpids(&pids, Int32(MemoryLayout<pid_t>.stride * pids.count)))
        guard count > 0 else { return [] }

        var pathBuffer = [CChar](repeating: 0, count: Int(MAXPATHLEN) * 4)
        var found: [Running] = []
        for pid in pids.prefix(count) where pid > 0 {
            guard proc_pidpath(pid, &pathBuffer, UInt32(pathBuffer.count)) > 0 else { continue }
            let path = String(cString: pathBuffer)
            guard path.hasSuffix(".app/Contents/MacOS/RobloxPlayer") else { continue }
            found.append(Running(pid: pid, isTrayHelper: arguments(of: pid).contains("-launchToTray")))
        }
        return found
    }

    /// Game clients only (the idle menu-bar helper isn't counted).
    static func runningClientCount() -> Int {
        running().filter { !$0.isTrayHelper }.count
    }

    /// Asks every game client to quit, then force-closes any still open after a few seconds.
    /// Roblox's idle menu-bar helper is left alone.
    static func closeAll() async -> (closed: Int, failed: Int) {
        let clients = running().filter { !$0.isTrayHelper }
        for client in clients {
            if NSRunningApplication(processIdentifier: client.pid)?.terminate() != true {
                kill(client.pid, SIGTERM)
            }
        }

        try? await Task.sleep(for: .seconds(3))

        var failed = 0
        for client in clients where kill(client.pid, 0) == 0 {
            let app = NSRunningApplication(processIdentifier: client.pid)
            if app?.forceTerminate() != true && kill(client.pid, SIGKILL) != 0 {
                failed += 1
            }
        }
        return (clients.count - failed, failed)
    }

    /// Reads a process's command-line arguments (KERN_PROCARGS2).
    private static func arguments(of pid: pid_t) -> [String] {
        var mib: [Int32] = [CTL_KERN, KERN_PROCARGS2, pid]
        var size = 0
        guard sysctl(&mib, 3, nil, &size, nil, 0) == 0, size > MemoryLayout<Int32>.size else { return [] }

        var data = [UInt8](repeating: 0, count: size)
        guard sysctl(&mib, 3, &data, &size, nil, 0) == 0, size > MemoryLayout<Int32>.size else { return [] }

        let argc = Int(data.withUnsafeBytes { $0.load(as: Int32.self) })
        var index = MemoryLayout<Int32>.size
        while index < size && data[index] != 0 { index += 1 } // executable path
        while index < size && data[index] == 0 { index += 1 } // padding

        var arguments: [String] = []
        while arguments.count < argc && index < size {
            let start = index
            while index < size && data[index] != 0 { index += 1 }
            arguments.append(String(decoding: data[start..<index], as: UTF8.self))
            index += 1
        }
        return arguments
    }
}
