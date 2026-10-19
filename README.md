# MultiRoblox

A small Windows tray app that lets you run more than one Roblox client at the same time.

> **Status:** this has **not been tested on Windows yet**. GitHub builds the exe, so it compiles,
> but nobody has run it on a real PC. Expect small rough edges on the first run.

## Download

Get **MultiRoblox.exe** from the [Releases page](../../releases/latest). It's a single file: no
installer, and the PC doesn't need .NET. Windows 10/11, 64-bit.

## How it works

When the Roblox client (`RobloxPlayerBeta.exe`) starts, it creates a named mutex called
`ROBLOX_singletonMutex` (newer builds also use an event called `ROBLOX_singletonEvent`). If another
client already has it, the new client quits. That's the whole "one Roblox at a time" rule.

MultiRoblox creates and holds both of those objects **before** any client starts. After that,
each new client can't find the lock it expects and keeps running. This is the same approach used
by open-source tools like [MultiBloxy](https://github.com/Zgoly/MultiBloxy) and Fishstrap's
multi-instance option.

What it does **not** do: it doesn't inject into, patch, or read the memory of the Roblox process,
doesn't change any Roblox files, and doesn't touch Roblox's anti-cheat (Hyperion). It only holds
two named objects in your own Windows session. Turn it off or exit the app and those objects are
released, so Roblox goes back to normal single-instance behaviour.

## Use

1. **Open MultiRoblox first, before any Roblox window is open.** The lock has to exist before the
   first client starts. If Roblox is already running, the status says
   *"Roblox already running — close it first"*. Close every Roblox window (or use
   **Close all Roblox**) and multi-instance turns on by itself.
2. Check the big button says **Multi-instance: ON** and the status says *"Multi-instance active"*.
3. Join games the usual way, from roblox.com or Bloxstrap. Each join opens a new client.
4. To use **different accounts**, sign in to each one in a separate browser profile (or a separate
   browser) and join from there. Each client keeps the account it was launched with.

Other controls:

- **Clients running** shows how many `RobloxPlayerBeta.exe` processes are open.
- **Close all Roblox** asks each client to close, then ends any that don't. Unsaved progress in
  those games may be lost.
- **Start with Windows** adds a per-user startup entry (`HKCU\...\Run`) that starts the app hidden
  in the tray.
- Closing the window keeps the app running in the tray. To quit, right-click the tray icon and
  choose **Exit**.
- Opening `MultiRoblox.exe` a second time just brings the running copy's window to the front.

Tray icon colours: green = on, amber = waiting (Roblox already open, or another program holds the
lock), grey = off.

### If it doesn't work

- **"Another program is holding the Roblox lock"**: another multi-instance tool, or Bloxstrap's
  own multi-instance setting, already holds it. Use one tool, not both.
- **Second client still closes the first**: Roblox changes how the client checks this from time to
  time, and has blocked multi-instance tools before. If an update breaks it, this app can't fix
  that without doing things it deliberately doesn't do (see above).
- **Microsoft Store version of Roblox** runs under a different process and isn't supported.
  Use the version from roblox.com or Bloxstrap.
- Turning multi-instance **off** while several clients are open leaves them running, but the next
  client you launch may close the others.

## Build

You only need this if you're changing the code. Pushing to `main` builds the exe on GitHub
(see `.github/workflows/build.yml`); pushing a tag like `v1.0.1` publishes a new release.

To build it yourself:

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer. Building
produces one self-contained `MultiRoblox.exe` (win-x64): the PC you run it on doesn't need .NET
installed.

### On Windows

```powershell
winget install Microsoft.DotNet.SDK.8
dotnet publish -c Release
```

### On a Mac (cross-compile)

```bash
brew install --cask dotnet-sdk
```

```bash
dotnet publish -c Release
```

The project file already sets `win-x64`, self-contained, single-file, and
`EnableWindowsTargeting=true` (that last one lets the Windows-only WinForms project build on
macOS). The full command, if you want it spelled out:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableWindowsTargeting=true
```

The exe ends up in `bin/Release/net8.0-windows/win-x64/publish/MultiRoblox.exe`. Copy that one
file to the Windows PC.

Because the exe isn't code-signed, Windows SmartScreen may warn the first time you run it
("Windows protected your PC"). Click **More info → Run anyway** if you built it yourself.

## Project layout

| File | What it does |
| --- | --- |
| `src/RobloxSingletonLock.cs` | Creates and holds `ROBLOX_singletonMutex` and `ROBLOX_singletonEvent`; releases them on turn-off or exit |
| `src/RobloxProcesses.cs` | Counts and closes `RobloxPlayerBeta.exe` processes |
| `src/MainForm.cs` | The window, the tray icon, and the on/off/waiting logic (checks every 1.5 s) |
| `src/StartWithWindows.cs` | The per-user "start with Windows" registry entry |
| `src/AppIcons.cs` | Draws the tray icon at runtime |
| `src/Program.cs` | Single-instance guard for the app itself; `--minimized` starts it in the tray |

## Use at your own risk

Running several clients at once isn't something Roblox officially supports, and Roblox could
change the client so this stops working. Using it is at your own risk under the
[Roblox Terms of Use](https://www.roblox.com/info/terms). Don't use it for
botting, AFK farming, or anything else that breaks Roblox's rules or a game's rules. Accounts that
do can be banned.
