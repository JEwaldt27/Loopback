# Loopback

*(In-app product name. The underlying code, project folders, and file names still say "LineFlow" — see Project Structure below — and haven't been renamed yet. The browser tab title, toolbar title, login page, and `Client/wwwroot/favicon.png` — a coral "LB" monogram on navy — all say Loopback.)*

## What This Project Is
Loopback (codebase name: LineFlow) is a web-based AV/IT signal flow diagram application built with Blazor WebAssembly (.NET 10) and ASP.NET Core. It allows users to create, save, and open line flow diagrams (like those used in AV system design) with drag-and-drop devices, labeled ports, and smart elbow-routed connections.

**📖 End-user documentation lives in the [User Guide](UserGuide.md)** — this README covers architecture, development, and deployment.

## Project Structure
```
LineFlowAppHosted/
├── Client/                         ← Blazor WASM app (runs in browser)
│   ├── Pages/
│   │   ├── Home.razor              ← Main diagram page (ALL core diagram logic lives here)
│   │   ├── LineFlowNodeWidget.razor ← Custom node renderer with port labels
│   │   ├── TextNodeWidget.razor     ← Freeform text annotation renderer
│   │   ├── ConnectionLabelWidget.razor ← Connection label renderer (plain div, not SVG — see Connection Labels below)
│   │   └── Users.razor              ← Admin-only "Manage Users" page (/users)
│   ├── Layout/
│   │   ├── MainLayout.razor        ← Simple layout wrapper, no sidebar
│   │   └── NavMenu.razor           ← Minimal nav header
│   ├── wwwroot/
│   │   ├── index.html              ← Entry point, loads JS libs (jsPDF, html2canvas)
│   │   └── css/app.css             ← All styles
│   ├── _Imports.razor              ← Global using statements
│   ├── App.razor                   ← Root component
│   └── Program.cs                  ← Service registration
└── Server/                         ← ASP.NET Core host
    ├── Controllers/
    │   ├── DevicesController.cs    ← GET/POST /api/devices
    │   ├── AuthController.cs       ← GET /api/auth/status, POST setup/login/logout
    │   └── UsersController.cs      ← Admin-only CRUD for accounts, GET/POST/DELETE /api/users
    ├── Models/
    │   └── AppUser.cs               ← Username, PasswordHash, Role
    ├── Services/
    │   └── UserStore.cs             ← JSON-file-backed user store (Server/users.json)
    ├── LoginPage.cs                 ← Self-contained inline HTML for the /login screen
    ├── devices.json                ← Device library (auto-created if missing, tracked in git)
    ├── users.json                  ← User accounts + password hashes (auto-created, gitignored)
    └── Program.cs                  ← Server setup, cookie auth, auth gate middleware, serves Blazor WASM
Desktop/                             ← .NET MAUI Windows desktop shell (see Desktop Wrapper below)
├── MainPage.xaml(.cs)               ← Top bar (Settings/Reload) + WebView pointed at the server
├── SettingsPage.xaml(.cs)           ← Server URL entry, persisted via Preferences
└── Desktop.csproj                  ← Windows-only target (net10.0-windows10.0.19041.0)
```

## Tech Stack
- **.NET 10** (not 8 or 9)
- **Blazor WebAssembly** (standalone, hosted by ASP.NET Core)
- **Z.Blazor.Diagrams 3.0.4.1** — diagramming library
- **jsPDF 2.5.1** + **html2canvas 1.4.1** — PDF export (CDN, loaded in index.html)
- No database — device list stored in `Server/devices.json`

## Running the App
```bash
cd LineFlowAppHosted
dotnet run --project Server
# Opens at http://localhost:5052
```

## Key Architecture Decisions

### File Format (.lf)
Diagrams save as `.lf` files — JSON under the hood with this structure:
```json
{
  "meta": {
    "createdBy": "jdoe",
    "createdAt": "2026-07-06T18:12:00Z",
    "modifiedBy": "asmith",
    "modifiedAt": "2026-07-06T19:05:00Z"
  },
  "nodes": [
    {
      "id": "guid",
      "manufacturer": "Crestron",
      "model": "DM-NVX-350",
      "category": "AV over IP",
      "ports": [{ "name": "HDMI Out", "type": "HDMI", "direction": "Out" }],
      "x": 100,
      "y": 200
    }
  ],
  "links": [
    {
      "sourceNodeId": "guid",
      "sourcePortIndex": 0,
      "targetNodeId": "guid",
      "targetPortIndex": 1,
      "vertices": [{ "x": 450.5, "y": 220.0 }, { "x": 600.0, "y": 300.0 }],
      "label": "VID-005",
      "labelX": 525.0,
      "labelY": 220.0
    }
  ]
}
```
`vertices` holds every bend point along the connection, in order from source to target. Older files saved a single `midX` value instead — these still load fine (see Backward Compatibility below).

`label`/`labelX`/`labelY` are omitted (or `label` is `""`) for connections with no label. `labelX`/`labelY` record the label's actual on-canvas position (which the user can drag independently — see Connection Labels below); if they're missing on an older file that only has `label`, the position is recomputed from the connection's route midpoint on open.

`meta` is stamped automatically on Save: the first save of a new diagram sets `createdBy`/`createdAt` to the signed-in user and current time; every save (including the first) updates `modifiedBy`/`modifiedAt`. It's read back on Open and shown in a thin info bar under the toolbar (e.g. "Created by jdoe on Jul 6, 2026 3:12 PM · Last modified by asmith on Jul 6, 2026 4:05 PM"). Files saved before this feature existed simply have no `meta` block — they open fine, the info bar just stays hidden until the next save.

### Device Library
- Stored server-side in `Server/devices.json`
- Loaded via `GET /api/devices`
- New devices added via `POST /api/devices`
- Default device: Crestron DM-NVX-350 with 8 ports
- **Add/Edit Device UI**: adding or editing a device opens a centered modal dialog (`_deviceModalOpen` in `Home.razor`) over a dimmed backdrop, rather than an inline form in the side panel. Clicking the ✕, or clicking the backdrop outside the modal, cancels without saving. The Category field has no default value — it shows a `"Category..."` placeholder like Manufacturer and Model, so leaving it blank saves an empty category (shows as a blank group header when sorted by Type) rather than a misleading pre-filled `"Custom"`.
- **Sorting**: the side panel has a "Sort by" control (`_deviceSortMode`, `GroupedDevices` computed property) toggling between grouping by Type (category) or Manufacturer. Grouping is case-insensitive (`StringComparer.OrdinalIgnoreCase`) so inconsistently-cased category/manufacturer values (e.g. `"Generic"` vs `"GENERIC"`) still merge into one group instead of splitting.

### Placing Devices
Two ways to get a device from the side panel onto the canvas — both work everywhere:

- **Drag-and-drop**: drag a `.device-item` onto `.canvas-area` (`OnDeviceDragStart` / `OnCanvasDrop`). Doesn't work in the desktop app (see Desktop Wrapper below) due to a WebView2 platform bug, but works fine in any regular browser.
- **Click-to-place**: click a device in the side panel to arm it (highlighted coral, same visual treatment as an active Box/Line/Text toolbar button), then click the canvas to place it there — click the same device again to cancel. Reuses the existing annotation placement machinery: `_placementMode` gains a `"device"` value alongside `"box"`/`"line"`/`"text"`, and `_placingDevice` tracks which one; `ToggleDevicePlacement`/`OnCanvasClick` mirror the pattern `TogglePlacement` already uses for annotations. Added specifically so the desktop app has a working placement method, but it's available in the browser too.

### Authentication & User Management
The whole app sits behind a login gate — added so it can be safely exposed to the internet (e.g. via a Cloudflare Tunnel) while still letting individual users' access be revoked (e.g. when someone leaves the job) without touching anyone else's account.

- **Cookie-based auth** — `Microsoft.AspNetCore.Authentication.Cookies` (ships with the ASP.NET Core shared framework, no extra NuGet package needed). 30-day sliding-expiration cookie.
- **No database** — accounts live in `Server/users.json` (same file-backed pattern as `devices.json`), hashed with `Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>` (also part of the shared framework). This file is **gitignored** since it contains password hashes.
- **Two roles**: `Admin` (can manage users) and `User` (diagram access only).
- **First-run setup**: if `users.json` has no accounts yet, hitting the site shows a "Create admin account" screen instead of a normal login form. The first account created becomes the Admin.
- **Whole-app gate**: custom middleware in `Server/Program.cs` blocks every request — including the WASM framework files themselves — except `/login` and `/api/auth/*`, unless the request carries a valid auth cookie. Unauthenticated browser requests get redirected to `/login`; unauthenticated `/api/*` requests get a 401.
- **Login page** (`Server/LoginPage.cs`) is a small self-contained inline HTML/CSS/JS page served via a minimal API endpoint (`GET /login`) — there's no `Server/wwwroot`, so a static file wasn't worth adding. It calls `/api/auth/status` on load to decide whether to show setup mode or a normal login form.
- **Manage Users page** (`Client/Pages/Users.razor`, route `/users`) — a real Blazor page, admin-only (both server-enforced via `[Authorize(Roles = "Admin")]` on `UsersController` and gated client-side by checking `/api/auth/status`). Lists accounts, adds new ones with a chosen role, removes accounts (blocked from deleting your own account or the last remaining Admin), and **resets any user's password** (an inline panel per row — for forgotten passwords, so you no longer have to delete-and-recreate the account).
- **Password management**: any signed-in user can change **their own** password from the account menu (**🔑 Change Password** → modal requiring the current password), and admins can reset **anyone's** from the Manage Users page. Both enforce the 8-character minimum; self-service verifies the current password first, admin reset does not (that's the point of a reset).
- **Account menu** — top-right of the toolbar in `Home.razor`, shows the current username, a "Manage Users" link for Admins, Change Password, and Logout.

**Endpoints:**
| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `/api/auth/status` | GET | none | Whether any users exist, whether the caller is signed in, and their username/role |
| `/api/auth/setup` | POST | none (only works once, before any users exist) | Create the first (Admin) account and sign in |
| `/api/auth/login` | POST | none | Sign in with username/password |
| `/api/auth/logout` | POST | any | Clear the auth cookie |
| `/api/auth/change-password` | POST | signed-in | Change your own password (verifies current password) |
| `/api/users` | GET/POST | Admin | List / create accounts |
| `/api/users/{username}` | DELETE | Admin | Remove an account |
| `/api/users/{username}/password` | PUT | Admin | Reset another user's password (no current password needed) |

### Custom Classes (all defined inside Home.razor @code block)
- **`LineFlowNode`** — extends `NodeModel`, holds `DeviceDefinition`, creates `LineFlowPort` instances
- **`LineFlowPort`** — extends `PortModel`, holds `PortDefinition` with name/type/direction
- **`ElbowLinkModel`** — extends `LinkModel`; routing is driven by its `Vertices` collection (one or more draggable bend points). `MidX` is kept only as a legacy fallback for old saves with no vertices. `Color`/`SelectedColor` are set automatically from the source port's signal type via `ColorForType()`. `LabelText`/`LabelNode` hold its optional connection label (see Connection Labels below).
- **`ConnectionLabelNode`** — extends `NodeModel`, a draggable text bubble for a connection label; rendered by `ConnectionLabelWidget.razor`. `OwnerLink` points back at its `ElbowLinkModel` so deleting either side cleans up the other.
- **`ElbowRouter`** — extends `Router`, generates an orthogonal H-V-H-...-H path through all of the link's vertices, in order
- **`LegendNode`** — extends `NodeModel`, holds a list of `(Type, Color)` entries; rendered by `LegendNodeWidget.razor`. Created/updated by the "Legend" toolbar button using only the signal types present in current connections.
- **`DeviceDefinition`** — manufacturer, model, category, list of ports
- **`PortDefinition`** — name, type (HDMI/SDI/Audio/Network/USB/IR/COM/Other), direction (In/Out/Universal)
- **`BoxNode`** — extends `NodeModel`, resizable rectangle with a border color and no fill; rendered by `BoxNodeWidget.razor`, corner handles drag-resize via `Home.StartResize`
- **`LineNode`** — extends `NodeModel`, freeform 2-point line (`Start`/`End`) not attached to any port; rendered by `LineNodeWidget.razor`, endpoint handles drag via `Home.StartResize`
- **`TextNode`** — extends `NodeModel`, editable text label with `FontSize`/`Color`; rendered by `TextNodeWidget.razor`, double-click to edit, style row appears when selected

### Connection Rules
- Input → Output ✅
- Input → Universal ✅
- Output → Input ✅
- Output → Universal ✅
- Universal → Universal ✅
- Input → Input ❌ blocked
- Output → Output ❌ blocked

### Elbow Routing
Lines always make 90 degree bends, and now support multiple bends per connection:
- When a connection is first drawn, one `LinkVertexModel` is added automatically, vertically centered between the source and target ports (its handle is shown as a white dot with a red outline, `.diagram-link-vertex` in app.css)
- Dragging the first handle moves it horizontally only — its Y stays locked to the vertical midpoint of the segment so the handle always sits centered on the vertical line, even as the connection's endpoints move
- Right-click a connection to open its context menu:
  - **➕ Add Bend** — appends a new vertex between the last bend and the target port, letting the line route around a node it would otherwise cross
  - **➖ Remove Last Bend** — removes the most recently added vertex (only shown when more than one vertex exists)
  - Additional vertices beyond the first can be dragged freely in both X and Y
- `ElbowRouter.GetRoute()` walks the link's `Vertices` in order and produces the full H-V-H-...-H path through all of them
- All vertex positions are saved in the `.lf` file's `vertices` array and restored on open

### Connection Labels
Right-click a connection → **🏷 Add Label** / **Edit Label** opens a small modal to set a text label (e.g. `VID-005`) on that connection.

- **Not rendered via the diagramming library's built-in link labels.** `Z.Blazor.Diagrams` has a native `BaseLinkModel.Labels`/`AddLabel(...)` API, but it renders each label inside an SVG `<foreignObject>` — and `html2canvas` (used for PDF export) doesn't support `<foreignObject>` content, so labels added that way silently vanish from exported PDFs (confirmed by capturing what `html2canvas` actually produces). Worked around by **not** using that API at all.
- Instead, a label is a real `ConnectionLabelNode : NodeModel` (`Home.razor`), rendered by `ConnectionLabelWidget.razor` as a plain `<div class="lf-connection-label">` — the same rendering path used by every other node (`LineFlowNode`, `BoxNode`, `TextNode`, etc.), which is already known to capture correctly in PDF export.
- `ElbowLinkModel.LabelText` (string) is the source of truth for the label's text; `ElbowLinkModel.LabelNode` points at its visual node (or `null` if unset).
- Created at the connection's route midpoint (`ComputeLinkMidpoint`, a cumulative-path-length interpolation over `ElbowRouter.GetRoute()`), but **not** auto-tracked afterward — like `TextNode`/`BoxNode`/`LineNode`, the user can freely drag it if the layout changes later.
- `ComputeLinkMidpoint` has a fallback: right after a `.lf` file is opened, `PortModel.Position` hasn't been measured by the browser yet — it defaults to `Point(0,0)` rather than `null`, so `ElbowRouter.GetRoute()` silently returns a degenerate zero-length "route" instead of an empty one. `ComputeLinkMidpoint` detects that (`totalLength <= 0.01`) and falls back to averaging the two endpoint *nodes'* positions instead, which are reliable immediately (set directly from the `.lf` file, not measured from rendered DOM).
- `_diagram.Links.Removed`/`_diagram.Nodes.Removed` handlers (wired up once in `OnInitializedAsync`) keep `LabelText`/`LabelNode` in sync no matter how a label or its owning connection gets deleted — via the modal's "Remove Label" button, deleting the label node directly (it's a normal draggable/selectable/deletable node), deleting the connection itself, or "Delete Selected".
- Persisted in `.lf` files as `label`, `labelX`, `labelY` on each link (see File Format below) so a manually-repositioned label survives save/reload exactly where it was left.
- Included in DXF export as a centered `TEXT` entity on the `CONNECTIONS` layer, positioned at the label node's actual (possibly user-dragged) position — not recomputed — for consistency with what's on screen.

### Undo/Redo
Snapshot-based, not command-based: every mutation pushes a full serialized-diagram JSON snapshot (via `SerializeDiagramState()`, which shares `BuildDiagramData()` with `.lf` saves) onto `_undoStack` (capped at 50); undo/redo restore by rebuilding the whole canvas through `LoadDiagramJson()` — the same code path as file open, so restore fidelity is guaranteed to match save/open fidelity. Snapshots exclude the `meta` block so undo never rewrites the created-by/modified-by info bar.

- **Discrete operations** (place device/annotation, draw connection, add/remove bend, label changes, deletes, New, Open, legend) call `PushUndoSnapshot()` *before* mutating.
- **Drag-style edits** (node moves, bend-handle drags, box resizes, line-endpoint drags, text edits, text font/color changes) use a capture/commit pair: `CapturePointerSnapshot()` stores state when the gesture starts (diagram `PointerDown` on a model, `StartResize`, text-edit begin), and `CommitPointerSnapshot()` (canvas `mouseup`, text-edit end) pushes it **only if the serialized state actually changed** — so plain clicks and selections never pollute the stack.
- **Delete key**: the library's default Delete shortcut is re-registered with a wrapper that snapshots first, then calls `KeyboardShortcutsDefaults.DeleteSelection`.
- **Shortcuts**: Ctrl+Z / Ctrl+Y, registered on the library's `KeyboardShortcutsBehavior` — they only fire when the diagram canvas has focus, so they don't hijack text-field undo in inputs. Toolbar ↩/↪ buttons mirror them, disabled when their stack is empty.
- `_restoringState` guards restore so the `Links.Added`/`Nodes.Removed` handlers and snapshot pushes don't re-fire mid-rebuild.

### Copy / Paste (Duplicate)
In-app clipboard for duplicating nodes. Ctrl+C copies the selected nodes, Ctrl+V pastes; right-click a node → **⧉ Duplicate** does copy+paste in one step.

- **Copyable**: device nodes (`LineFlowNode`) and box/line/text annotations. The legend and connection-label nodes are intentionally excluded.
- **Connections come along**: a connection is copied only when **both** its endpoints are among the copied devices, so duplicating a connected group brings the wiring (and each connection's bends + label) with it. On paste, device nodes get fresh ids and the copied links are re-created against the new nodes via an old-id → new-node map.
- **Serialization is shared with save/undo**: `CopySelection()` reuses the same per-item `NodeToJson`/`LinkToJson`/`BoxToJson`/`LineToJson`/`TextToJson` helpers that `BuildDiagramData()` uses, so a copied node round-trips identically to a saved one. The clipboard is a JSON string held in `_clipboardJson` (in-memory, session-only — not the OS clipboard, so it doesn't cross tabs).
- **Paste offset**: pasted items are shifted by `30 × _pasteSequence` px from the originals; the sequence increments per paste (reset on copy) so repeated pastes step diagonally instead of stacking exactly. Vertices and label positions shift by the same offset to keep each connection's shape relative to its moved nodes.
- **Pasted items are re-selected** (`UnselectAll` then `SelectModel(node, false)` per pasted node) so you can immediately drag the copy.
- **Undoable**: paste calls `PushUndoSnapshot()` before adding, so a single Ctrl+Z removes the whole pasted group (nodes + connections + labels) at once and marks the diagram dirty. Copy itself doesn't touch the diagram, so it never marks dirty.
- **Duplicate context item**: `DuplicateContextNode` duplicates the current selection; if you right-click a node that isn't part of the selection, it acts on just that node.

### Unsaved-Changes Warning
Warns before the browser tab closes with unsaved work, using the browser's native `beforeunload` prompt (its text can't be customized — Chrome/Edge/Firefox show their own generic "Leave site? / Changes you made may not be saved" dialog).

- **Dirty tracking** rides on the same mutation funnels as undo/redo: `MarkDirty()` is called from `PushUndoSnapshot()` (all discrete + committed-drag mutations) and `RestoreDiagramState()` (undo/redo). `MarkClean()` is called by Save, Open, and New. A `_isDirty` bool backs it.
- **Conservative by design**: this is a boolean flag, not a state comparison, so editing then undoing back to the exact saved state still reads as "dirty". That's deliberate — a spurious "you have unsaved changes" warning is harmless; failing to warn and losing work is not.
- **JS bridge**: `MarkDirty`/`MarkClean` mirror the flag to `window.setUnsavedChanges(bool)` in `index.html`, which sets `window.__lfUnsavedChanges`. A single `beforeunload` listener (registered once at page load, before Blazor even starts) reads that flag and calls `e.preventDefault()` + sets `e.returnValue` only when dirty. Keeping the check in a plain JS flag is required because `beforeunload` fires synchronously and can't await a round-trip into .NET.
- **Visual cue**: an amber `● Unsaved` badge (`.lf-unsaved-badge`) appears in the toolbar next to the diagram title whenever `_isDirty` is set.
- **Note**: Logout does a full-page navigation (`forceLoad: true`), so logging out with unsaved changes correctly triggers the same prompt.

### Backward Compatibility
Files saved before multi-bend support only stored a single `midX` value (no `vertices` array). On open, if `vertices` is missing/empty, the loader falls back to `ElbowLinkModel.MidX`, which `ElbowRouter` still understands as a single-bend midpoint.

Legends are saved in `.lf` files as a top-level `legend` property (`{x, y, entries: [{type, color}]}`) as of the undo/redo work — files saved before that simply have no `legend` property and open fine (the legend just isn't restored; click Legend to recreate it).

### Port Alignment
- Direction "In" → `PortAlignment.Left`
- Direction "Out" → `PortAlignment.Right`
- Direction "Universal" → `PortAlignment.Right`

## JS Functions (defined in index.html)
All attached to `window` object for Blazor JS interop:
- `window.saveAsFile(filename, content)` — triggers browser download
- `window.exportToPdf(title)` — captures canvas with html2canvas, generates PDF with jsPDF

## CSS Classes of Note
- `.lf-node` — custom node box
- `.lf-node-title` — node title bar
- `.lf-port-row-left` / `.lf-port-row-right` — port rows with labels
- `.lf-port-dot` — the port circle rendered by `PortRenderer`
- `.diagram-node` — has `overflow: visible !important` so port dots show outside node bounds
- `.context-menu` — right-click menu
- `.lf-modal` / `.lf-modal-overlay` — centered popup dialog + dimmed backdrop pattern (used by Add/Edit Device)
- `.lf-connection-label` — the draggable connection label bubble (plain `<div>`, not the diagramming library's SVG-based link labels)
- `.lf-file-meta` — the "Created by / Last modified by" info bar under the toolbar
- `.lf-unsaved-badge` — the amber "● Unsaved" indicator shown in the toolbar when there are unsaved changes

## Known Issues / Work in Progress

- The `OnLinkAdded` flow casts a new connection's `BaseLinkModel` to `LinkModel` before promoting it to an `ElbowLinkModel` — if the library changes this internal type, this will break.

### Fixed: error banner permanently visible
The `#blazor-error-ui` div in `index.html` (Blazor's built-in "An unhandled error has occurred" banner) had no CSS anywhere styling it — the standard Blazor template hides it by default (`display: none`) and only reveals it when a real unhandled exception fires. Without that rule, it just used the browser default (`display: block`) and was visible on every page load regardless of whether anything was actually wrong, with nothing logged to the console since no exception ever occurred. Fixed by adding the standard hidden-by-default rule (themed to the dark palette) to `Client/wwwroot/css/app.css`.

### Fixed: Release publish fingerprint placeholder bug
A Release `dotnet publish` used to ship an `index.html` containing a literal, unresolved `#[.{fingerprint}]` placeholder in the script tag instead of the real hashed filename, breaking the app on first load (worked fine in `dotnet run`/Debug, broke only in published Release output). Fixed by removing `<OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>` from `Client/Client.csproj` and hardcoding the plain `_framework/blazor.webassembly.js` script reference (no fingerprinting) in `Client/wwwroot/index.html`. Verify after any future publish by checking that `publish/wwwroot/index.html` doesn't contain `#[` anywhere.

## NuGet Packages
```xml
<!-- Client/Client.csproj -->
<PackageReference Include="Z.Blazor.Diagrams" Version="3.0.4.1" />

<!-- Server/Server.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="..." />
```
Cookie authentication (`Microsoft.AspNetCore.Authentication.Cookies`) and password hashing (`Microsoft.AspNetCore.Identity.PasswordHasher<T>`) need **no additional NuGet packages** — both ship as part of the ASP.NET Core shared framework that `Microsoft.NET.Sdk.Web` projects already reference.

## _Imports.razor (Client)
```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using Client
@using Client.Layout
@using Blazor.Diagrams
@using Blazor.Diagrams.Components
@using Blazor.Diagrams.Core.Geometry
@using Blazor.Diagrams.Core.Models
@using Blazor.Diagrams.Core.Anchors
@using Blazor.Diagrams.Core.Models.Base
```

## Deploying to Ubuntu Linux
```bash
# On Windows dev machine — framework-dependent publish (runs on any OS with the
# matching ASP.NET Core runtime installed; no -r/--self-contained flag needed)
dotnet publish Server -c Release -o ./publish

# Copy to server
scp -r ./publish user@your-server-ip:/home/user/lineflowapp

# On Ubuntu server — bind to all interfaces so it's reachable from other machines on the LAN
cd /home/user/lineflowapp
ASPNETCORE_URLS=http://0.0.0.0:5052 dotnet Server.dll

# Or as a systemd service:
sudo nano /etc/systemd/system/lineflow.service
```

By default Kestrel only binds to `localhost`, which is unreachable from other machines — `ASPNETCORE_URLS` (or the `--urls` flag) must explicitly bind `0.0.0.0` for LAN access. Also open the port in the firewall if `ufw` is active: `sudo ufw allow 5052/tcp`.

Systemd service file:
```ini
[Unit]
Description=LineFlow App
After=network.target

[Service]
WorkingDirectory=/home/user/lineflowapp
ExecStart=/usr/bin/dotnet Server.dll
Environment=ASPNETCORE_URLS=http://0.0.0.0:5052
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable lineflow
sudo systemctl start lineflow
```

Then from your Windows machine, browse to `http://<linux-server-ip>:5052`.

## Install .NET 10 on Ubuntu
```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
```

## Desktop Wrapper (MAUI)
`Desktop/` is a thin native Windows shell around the hosted app — a window with a `WebView` pointed at wherever your Loopback server is running, not a from-scratch reimplementation. It does **not** host the Blazor components in-process (that would be a true "Blazor Hybrid" app, requiring `Client`'s pages to move into a shared Razor Class Library); it just needs the server to be reachable.

- **Windows-only target** (`net10.0-windows10.0.19041.0`) — the default MAUI template also multi-targets Android/iOS/MacCatalyst, which was trimmed since only a Windows desktop shell was needed. Requires the MAUI workload: `dotnet workload install maui`.
- **Configurable server address**: no hardcoded URL. On first launch (or whenever no address is saved), `SettingsPage` pops up automatically asking for a full URL (e.g. `http://192.168.1.200:5052`), validated via `Uri.TryCreate` and persisted with MAUI's `Preferences` API (`SettingsPage.PrefKeyServerUrl`) — a simple per-user key/value store on the machine, no config file to manage. A **Settings** button in `MainPage`'s top bar reopens it anytime to point at a different server (e.g. switching between a LAN IP and a Cloudflare Tunnel URL later).
- **Reload** button next to Settings just re-sets `WebView.Source` to the saved URL — useful if the server was temporarily unreachable or restarted.
- App icon reuses `Client/wwwroot/favicon.png` (the Loopback "LB" monogram); `ApplicationTitle`/`AppShell` title are both set to "Loopback".
- Run/debug: `dotnet build Desktop -t:Run -f net10.0-windows10.0.19041.0` (or open the `.slnx` in an IDE and set `Desktop` as the startup project).
- **Distributable build**: `dotnet publish Desktop -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained` → outputs to `Desktop/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`. Self-contained bundles the .NET runtime (~230MB, no install needed on the target machine); drop `--self-contained` for a much smaller framework-dependent build that requires the .NET 10 desktop runtime already installed.
- **Known platform bug — HTML5 drag-and-drop doesn't work inside the desktop app.** This is a confirmed, unresolved bug in WinUI's WebView2 hosting (not Loopback's code): `dragover` doesn't fire reliably when WebView2 is embedded in a WinUI/MAUI window, even though the exact same page works fine in a real browser or in WPF's WebView2 hosting ([dotnet/maui#9983](https://github.com/dotnet/maui/issues/9983), [microsoft-ui-xaml#10576](https://github.com/microsoft/microsoft-ui-xaml/issues/10576)). Worked around by adding **click-to-place** for devices (see Placing Devices below) as a platform-independent alternative — drag-and-drop still works fine in a regular browser tab.

## Features Implemented
- ✅ Drag-and-drop device nodes from side panel onto canvas, plus a click-to-place alternative (click a device, then click the canvas) that also works in the desktop app, where drag-and-drop is blocked by a WebView2 platform bug
- ✅ Labeled ports on left (inputs) and right (outputs/universal) edges
- ✅ Elbow-routed connections with a draggable, vertically-centered handle on the first segment
- ✅ Multi-bend connection routing — add/remove additional bend points via right-click ("➕ Add Bend" / "➖ Remove Last Bend") to route around blocking nodes
- ✅ Connection direction rules enforced (no input→input etc.)
- ✅ Right-click context menu: delete node, delete connection, add/remove bends
- ✅ Delete key removes selected nodes/links
- ✅ New / Save (.lf) / Open (.lf) diagram files, with multi-vertex routing persisted and legacy single-`midX` files still loading correctly
- ✅ Server-side device library with add, edit, and delete devices, via a centered modal dialog (not inline in the side panel)
- ✅ Device list sorting by Type or Manufacturer, case-insensitive grouping
- ✅ Color-coded connections and port dots by signal type (HDMI, SDI, Audio, Network, USB, IR, COM)
- ✅ Legend node — click "Legend" to add a draggable canvas node showing only the signal types actually connected in the current diagram
- ✅ PDF export (white background, title + date header, direct download) — resets zoom/pan before capture so connections render correctly at any zoom level
- ✅ DXF export (AutoCAD compatible, NODES + CONNECTIONS layers), generating the same multi-segment path as the live app's routing
- ✅ Cable schedule export (Export → Cable Schedule) — CSV pull sheet with one row per connection: cable label, signal type, source device + port, destination device + port; sorted by source device, CSV-escaped, UTF-8 BOM so Excel opens it cleanly
- ✅ Zoom and pan on canvas
- ✅ Freeform annotations — Box (resizable rectangle, no fill), Line (2-point freeform line with draggable endpoints, not attached to ports), and Text (click-to-place, editable, with font size/color controls); all three are selectable/deletable, saved in `.lf` files, and included in PDF and DXF exports (DXF `ANNOTATIONS` layer)
- ✅ Per-user authentication — cookie-based login gating the entire app, first-run admin setup, Admin/User roles, in-app "Manage Users" page, account menu with Logout (see Authentication & User Management above)
- ✅ Password management — self-service "Change Password" for any user (verifies current password) and admin password reset per user on the Manage Users page
- ✅ File authorship tracking — `.lf` files record who created and who last modified them, and when, shown in an info bar under the toolbar
- ✅ Connection labels — right-click a connection to add/edit a text label (e.g. "VID-005"); draggable, saved in `.lf` files, included in DXF export, and PDF-export-safe (see Connection Labels above for why that needed a custom rendering path)
- ✅ Windows desktop wrapper (`Desktop/`, .NET MAUI) — native window shell with a configurable server address (Settings page, persisted via `Preferences`), not tied to a hardcoded URL (see Desktop Wrapper above)
- ✅ Undo/redo — Ctrl+Z / Ctrl+Y (canvas focused) or toolbar ↩/↪ buttons; covers placements, connections, bends, labels, moves, resizes, text edits, deletes, New, and Open (see Undo/Redo above)
- ✅ Legend persistence — legends are now saved in `.lf` files and restored on open
- ✅ Unsaved-changes warning — browser prompt before closing the tab with unsaved work, plus an amber "● Unsaved" toolbar badge (see Unsaved-Changes Warning above)
- ✅ Copy/paste & duplicate — Ctrl+C / Ctrl+V or right-click → Duplicate; copies selected nodes plus the connections (and labels) between them (see Copy / Paste above)

## Features Planned / Not Yet Implemented
- ⬜ Auto-numbered connection labels — "Add Label" pre-fills the next number per signal type (VID-001, VID-002, AUD-001, …)
- ⬜ Duplicate device in the library (new devices are often one port different from an existing one)
- ⬜ Docker image published to GitHub Container Registry for one-command self-hosting (needs volume-mount planning for `devices.json`/`users.json`)

## License
[PolyForm Noncommercial 1.0.0](LICENSE) — free to use, modify, and share for any noncommercial purpose. Commercial use requires separate permission from the copyright holder. Note this is a "source-available" license, not an OSI-approved open source license — the Open Source Definition explicitly prohibits restricting commercial use, which is exactly the restriction this project needs.
