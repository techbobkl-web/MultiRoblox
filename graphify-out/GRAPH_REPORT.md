# Graph Report - MultiRoblox  (2026-10-18)

## Corpus Check
- 12 files · ~6,426 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 111 nodes · 155 edges · 7 communities
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `dfca9157`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- AccountsSection
- AccountWindow
- MainForm
- SavedAccount
- MultiRoblox
- .CloseAllRobloxAsync
- MultiRoblox.csproj

## God Nodes (most connected - your core abstractions)
1. `MainForm` - 25 edges
2. `AccountWindow` - 22 edges
3. `AccountsSection` - 22 edges
4. `SavedAccount` - 8 edges
5. `MultiRoblox` - 7 edges
6. `AppData` - 7 edges
7. `AccountStore` - 6 edges
8. `MultiRoblox` - 5 edges
9. `RobloxWeb` - 5 edges
10. `Use` - 3 edges

## Surprising Connections (you probably didn't know these)
- `AccountsSection` --references--> `SavedAccount`  [EXTRACTED]
  src/AccountsSection.cs → src/AccountStore.cs
- `AccountWindow` --references--> `SavedAccount`  [EXTRACTED]
  src/AccountWindow.cs → src/AccountStore.cs
- `AccountsSection` --references--> `AppData`  [EXTRACTED]
  src/AccountsSection.cs → src/AccountStore.cs
- `AccountWindow` --references--> `RobloxUser`  [EXTRACTED]
  src/AccountWindow.cs → src/RobloxWeb.cs

## Import Cycles
- None detected.

## Communities (7 total, 0 thin omitted)

### Community 0 - "AccountsSection"
Cohesion: 0.14
Nodes (12): Dictionary, Func, ListBox, bool, Button, Label, List, Task (+4 more)

### Community 1 - "AccountWindow"
Cohesion: 0.16
Nodes (11): CoreWebView2LaunchingExternalUriSchemeEventArgs, Form, bool, FormClosedEventArgs, Label, string, Task, Timer (+3 more)

### Community 2 - "MainForm"
Cohesion: 0.09
Nodes (16): CheckBox, Color, FormClosingEventArgs, Icon, int, NotifyIcon, Result, RobloxSingletonLock (+8 more)

### Community 3 - "SavedAccount"
Cohesion: 0.21
Nodes (8): Data, DateTime, JsonSerializerOptions, ReadCleanly, List, AccountStore, AppData, SavedAccount

### Community 4 - "MultiRoblox"
Cohesion: 0.17
Nodes (6): MultiRoblox, HttpClient, string, Task, RobloxUser, RobloxWeb

### Community 5 - ".CloseAllRobloxAsync"
Cohesion: 0.17
Nodes (11): Build, Download, How it works, If it doesn't work, MultiRoblox, On a Mac (cross-compile), On Windows, Playing on different accounts at the same time (+3 more)

### Community 6 - "MultiRoblox.csproj"
Cohesion: 0.50
Nodes (3): net8.0-windows, Microsoft.Web.WebView2 (1.0.2903.40), Microsoft.NET.Sdk

## Knowledge Gaps
- **11 isolated node(s):** `Download`, `How it works`, `Playing on different accounts at the same time`, `If it doesn't work`, `On Windows` (+6 more)
  These have ≤1 connection - possible missing edges or undocumented components.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AccountWindow` connect `AccountWindow` to `AccountsSection`, `SavedAccount`, `MultiRoblox`?**
  _High betweenness centrality (0.402) - this node is a cross-community bridge._
- **Why does `MainForm` connect `MainForm` to `AccountWindow`, `MultiRoblox`?**
  _High betweenness centrality (0.349) - this node is a cross-community bridge._
- **Why does `AccountsSection` connect `AccountsSection` to `SavedAccount`, `MultiRoblox`?**
  _High betweenness centrality (0.212) - this node is a cross-community bridge._
- **What connects `Download`, `How it works`, `Playing on different accounts at the same time` to the rest of the system?**
  _11 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AccountsSection` be split into smaller, more focused modules?**
  _Cohesion score 0.13852813852813853 - nodes in this community are weakly interconnected._
- **Should `MainForm` be split into smaller, more focused modules?**
  _Cohesion score 0.08547008547008547 - nodes in this community are weakly interconnected._