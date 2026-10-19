# Graph Report - MultiRoblox  (2026-10-18)

## Corpus Check
- 12 files · ~6,034 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 99 nodes · 144 edges · 9 communities (6 shown, 3 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `991e4467`
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
- .OnFormClosing
- .OnFormClosed

## God Nodes (most connected - your core abstractions)
1. `MainForm` - 25 edges
2. `AccountWindow` - 22 edges
3. `AccountsSection` - 22 edges
4. `SavedAccount` - 8 edges
5. `AppData` - 7 edges
6. `AccountStore` - 6 edges
7. `MultiRoblox` - 5 edges
8. `RobloxWeb` - 5 edges
9. `RobloxUser` - 3 edges
10. `net8.0-windows` - 1 edges

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

## Communities (9 total, 3 thin omitted)

### Community 0 - "AccountsSection"
Cohesion: 0.14
Nodes (12): Dictionary, Func, ListBox, bool, Button, Label, List, Task (+4 more)

### Community 1 - "AccountWindow"
Cohesion: 0.17
Nodes (10): CoreWebView2LaunchingExternalUriSchemeEventArgs, bool, FormClosedEventArgs, Label, string, Task, Timer, AccountWindow (+2 more)

### Community 2 - "MainForm"
Cohesion: 0.11
Nodes (14): CheckBox, Color, Form, Icon, int, NotifyIcon, Result, RobloxSingletonLock (+6 more)

### Community 3 - "SavedAccount"
Cohesion: 0.21
Nodes (8): Data, DateTime, JsonSerializerOptions, ReadCleanly, List, AccountStore, AppData, SavedAccount

### Community 4 - "MultiRoblox"
Cohesion: 0.18
Nodes (6): MultiRoblox, HttpClient, string, Task, RobloxUser, RobloxWeb

### Community 6 - "MultiRoblox.csproj"
Cohesion: 0.50
Nodes (3): net8.0-windows, Microsoft.Web.WebView2 (1.0.2903.40), Microsoft.NET.Sdk

## Knowledge Gaps
- **3 isolated node(s):** `net8.0-windows`, `Microsoft.Web.WebView2 (1.0.2903.40)`, `Microsoft.NET.Sdk`
  These have ≤1 connection - possible missing edges or undocumented components.
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AccountWindow` connect `AccountWindow` to `AccountsSection`, `MainForm`, `SavedAccount`, `MultiRoblox`?**
  _High betweenness centrality (0.507) - this node is a cross-community bridge._
- **Why does `MainForm` connect `MainForm` to `.OnFormClosed`, `MultiRoblox`, `.CloseAllRobloxAsync`, `.OnFormClosing`?**
  _High betweenness centrality (0.441) - this node is a cross-community bridge._
- **Why does `AccountsSection` connect `AccountsSection` to `SavedAccount`, `MultiRoblox`?**
  _High betweenness centrality (0.268) - this node is a cross-community bridge._
- **What connects `net8.0-windows`, `Microsoft.Web.WebView2 (1.0.2903.40)`, `Microsoft.NET.Sdk` to the rest of the system?**
  _3 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AccountsSection` be split into smaller, more focused modules?**
  _Cohesion score 0.13852813852813853 - nodes in this community are weakly interconnected._
- **Should `MainForm` be split into smaller, more focused modules?**
  _Cohesion score 0.10526315789473684 - nodes in this community are weakly interconnected._