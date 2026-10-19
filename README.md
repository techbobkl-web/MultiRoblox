# MultiRoblox

Run more than one Roblox client at the same time, each signed in to a **different account**.
There's a version for **Windows** and one for **Mac**.

## Download

Get it from the [Releases page](../../releases/latest):

| You have | Download | What to do |
| --- | --- | --- |
| Windows 10/11 (64-bit) | **MultiRoblox.exe** | Run it. No installer, and the PC doesn't need .NET. |
| Mac with macOS 14 or newer (Apple silicon or Intel) | **MultiRoblox-mac.zip** | Unzip it and drag **MultiRoblox** into Applications. |

**Status:**

- **Mac:** tested on a real Mac (macOS 26, Roblox 0.743). Two or three Roblox clients stayed open
  side by side, and quitting one left the others running. Not tested: signing in to accounts and
  joining a game through the account list, because that needs real Roblox accounts.
- **Windows:** builds on GitHub without errors, but **hasn't been run on a real PC yet**.

### First launch: getting past the security warning

Neither app is signed by a paid Apple or Microsoft developer account, so your computer warns you
the first time.

- **Mac:** double-click MultiRoblox. macOS says it can't check it for malicious software. Click
  **Done**, open **System Settings → Privacy & Security**, scroll down to *"MultiRoblox" was blocked*,
  and click **Open Anyway**. (On older macOS you can instead right-click the app → **Open** → **Open**.)
  You only do this once.
- **Windows:** if SmartScreen says *"Windows protected your PC"*, click **More info → Run anyway**.

Only do this for a copy you downloaded from this repo's Releases page.

## Playing on different accounts at the same time

Both versions work the same way:

1. Open MultiRoblox and check that **Multi-instance** is on.
2. Under **Accounts**, press **Add account**. A sign-in window opens: sign in to Roblox there
   (including any 2-step code or captcha). It closes by itself once you're in, and the account
   shows up in the list. Repeat for each account. You only do this once per account.
3. Paste a game link (like `https://www.roblox.com/games/920587237/Adopt-Me`) or just its place ID
   into **Game link or place ID**. Private-server and share links work too.
4. Select the accounts to play on (Ctrl+click on Windows, ⌘-click on Mac) and press **Launch**.
   MultiRoblox starts them one after another, a few seconds apart, and each account gets its own
   Roblox window.

For each account, a small browser window opens on roblox.com, starts the game, and closes itself.
If the game doesn't start within a few seconds, press the green **Play** button in that window.
**Open roblox.com** (or double-click an account) opens that account's browser so you can pick any
game, join a friend, and so on.

**How sign-ins are kept:** each account has its own private browser profile inside MultiRoblox
(Edge WebView2 on Windows, WebKit on Mac). MultiRoblox never sees your password. The sign-in stays in
that profile on your computer, like a normal browser. Anyone who can use your computer account could
use those sign-ins, so don't share the folder. **Remove** deletes an account's profile. To sign it
out everywhere too, use *Log out of all other sessions* in Roblox's security settings.

**Close all Roblox** closes every Roblox game window. Unsaved progress in those games may be lost.

## Windows details

How multi-instance works: when the Roblox client (`RobloxPlayerBeta.exe`) starts, it creates a
named mutex called `ROBLOX_singletonMutex` (newer builds also use an event called
`ROBLOX_singletonEvent`). If another client already has it, the new client quits. MultiRoblox
creates and holds both **before** any client starts, so every new client can run. This is the same
approach as [MultiBloxy](https://github.com/Zgoly/MultiBloxy) and Fishstrap's multi-instance option.

- **Open MultiRoblox before any Roblox window is open.** If Roblox is already running, the status
  says *"Roblox already running — close it first"*. Close every Roblox window (or use **Close all
  Roblox**) and multi-instance turns on by itself.
- You can also join games from roblox.com or Bloxstrap as usual while it's on: each join opens a
  new client.
- **Start with Windows** starts the app hidden in the tray when you sign in to Windows.
- Closing the window keeps the app running in the tray (needed to keep multi-instance on). To quit,
  right-click the tray icon and choose **Exit**.
- Tray icon colours: green = on, amber = waiting, grey = off.
- The account windows need the Microsoft Edge WebView2 Runtime. It's already on Windows 11 and most
  Windows 10 PCs. If it's missing, the app says so and tells you where to get it.

If it doesn't work:

- **"Another program is holding the Roblox lock"**: another multi-instance tool, or Bloxstrap's
  own multi-instance setting, already holds it. Use one tool, not both.
- **Microsoft Store version of Roblox** isn't supported. Use the one from roblox.com or Bloxstrap.
- Turning multi-instance **off** while several clients are open leaves them running, but the next
  client you launch may close the others.

## Mac details

How multi-instance works: Roblox.app tells macOS to allow only one copy
(`LSMultipleInstancesProhibited`), so opening a game from a browser, `open -n`, or Finder always goes
to the Roblox that's already open. Roblox's own launcher starts the game binary directly with
`-protocolString <roblox-player: link>`. MultiRoblox does the same for each launch, so every launch
gets its own Roblox window. Roblox.app itself isn't copied, modified, or re-signed.

- Multi-instance applies to launches **from MultiRoblox** (the account list, or **Open roblox.com**).
  Clicking Play in Safari or Chrome still goes to the Roblox window that's already open.
- Turning **Multi-instance** off makes MultiRoblox launches behave like normal Roblox: each launch
  goes to the open window.
- The running count doesn't include the small Roblox helper that sits in the menu bar.
- Install Roblox from roblox.com and open it once before using MultiRoblox.

## What MultiRoblox doesn't do

It doesn't inject into, patch, or read the memory of the Roblox process, doesn't change any Roblox
files, and doesn't touch Roblox's anti-cheat (Hyperion). Games are started through roblox.com's
normal Play flow. Roblox changes its client from time to time and has blocked multi-instance tools
before, so an update could stop this from working.

## Use at your own risk

Running several clients at once isn't something Roblox officially supports. Using it is at your own
risk under the [Roblox Terms of Use](https://www.roblox.com/info/terms). Don't use it for botting,
AFK farming, or anything else that breaks Roblox's rules or a game's rules. Accounts that do can be
banned.

## Building it yourself

You only need this if you're changing the code. Every push and pull request builds both apps on
GitHub (`.github/workflows/build.yml`), and pushing a tag like `v2.0.1` publishes a new release
with both downloads.

**Mac app** (needs Xcode or the Xcode command-line tools, macOS 14+):

```bash
mac/scripts/build-app.sh
```

That makes `mac/dist/MultiRoblox.app` (universal, ad-hoc signed) and `mac/dist/MultiRoblox-mac.zip`.
For a quick debug run: `cd mac && swift run`.

**Windows app** (needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer;
builds on Windows, Mac or Linux):

```bash
dotnet publish windows/MultiRoblox.csproj -c Release -o windows/publish
```

That makes one self-contained `windows/publish/MultiRoblox.exe`.

## Project layout

| Path | What it does |
| --- | --- |
| `windows/src/RobloxSingletonLock.cs` | Creates and holds `ROBLOX_singletonMutex` and `ROBLOX_singletonEvent` |
| `windows/src/MainForm.cs` | Window, tray icon, and the on/off/waiting logic |
| `windows/src/AccountsSection.cs` | Account list, game box, and Launch button |
| `windows/src/AccountWindow.cs` | One account's browser (its own WebView2 profile) |
| `windows/src/AccountStore.cs` | Saves the account list (names only) to `%LOCALAPPDATA%\MultiRoblox` |
| `windows/src/RobloxWeb.cs`, `RobloxProcesses.cs`, `StartWithWindows.cs`, `AppIcons.cs`, `Program.cs` | Links, process count/close, startup entry, icon, app entry point |
| `mac/Sources/MultiRoblox/RobloxClient.swift` | Finds Roblox, starts a separate client per launch, counts and closes clients |
| `mac/Sources/MultiRoblox/AccountBrowser.swift` | One account's browser (its own WebKit data store) |
| `mac/Sources/MultiRoblox/AppModel.swift`, `ContentView.swift` | App state and the main window |
| `mac/Sources/MultiRoblox/AccountStore.swift`, `RobloxLinks.swift` | Saved account list (names only); game link handling |
| `mac/scripts/build-app.sh` | Builds, signs (ad-hoc) and zips `MultiRoblox.app` |
