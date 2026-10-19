// swift-tools-version:5.10
import PackageDescription

let package = Package(
    name: "MultiRoblox",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "MultiRoblox",
            path: "Sources/MultiRoblox"
        ),
    ]
)
