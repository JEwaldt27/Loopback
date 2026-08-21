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
│   │   ├── BoxNodeWidget.razor      ← Rectangle annotation renderer
│   │   ├── LineNodeWidget.razor     ← Freeform line annotation renderer
│   │   ├── LegendNodeWidget.razor   ← Cable-type legend renderer
│   │   ├── ConnectionLabelWidget.razor ← Connection label renderer (plain div, not SVG — see Connection Labels below)
│   │   ├── BreakTagWidget.razor     ← Label block shown at each end of a broken connection
│   │   └── Users.razor              ← Admin-only "Manage Users" page (/users)
│   ├── Layout/
│   │   ├── MainLayout.razor        ← Simple layout wrapper, no sidebar
│   │   └── NavMenu.razor           ← Minimal nav header
│   ├── wwwroot/
│   │   ├── index.html              ← Entry point, loads JS libs (jsPDF, html2canvas)
│   │   └── css/app.css             ← All styles
│   ├── Diagrams/                   ← Diagram model types (no UI, no component state)
│   │   ├── Devices.cs              ← DeviceDefinition/PortDefinition + LineFlowNode/LineFlowPort
│   │   ├── Connections.cs          ← ElbowLinkModel, ElbowRouter, label + break-tag nodes
│   │   ├── Annotations.cs          ← Box/Line/Text, Legend, Schedule nodes
│   │   └── Document.cs             ← CableType, TitleBlockInfo, Sheet
│   ├── Export/                     ← Pure output generation, unit-testable without a browser
│   │   ├── DxfWriter.cs            ← DXF R12 generation over a finished diagram
│   │   └── ScheduleSource.cs       ← The rows behind both CSVs, the Cable Count dialog, and placed tables
│   ├── AppVersion.cs               ← Displayed build version — bump before each deploy
│   ├── _Imports.razor              ← Global using statements
│   ├── App.razor                   ← Root component
│   └── Program.cs                  ← Service registration
└── Server/                         ← ASP.NET Core host
    ├── Controllers/
    │   ├── DevicesController.cs    ← GET/POST /api/devices
    │   ├── AuthController.cs       ← GET /api/auth/status, POST setup/login/logout/change-password
    │   ├── UsersController.cs      ← Admin-only CRUD for accounts, GET/POST/DELETE /api/users
    │   ├── LogoController.cs       ← Title-block images (?slot=logo|stamp), GET/POST/DELETE /api/logo
    │   └── FeatureRequestsController.cs ← Shared feature-request list, GET/POST/PUT/DELETE /api/featurerequests
    ├── Models/
    │   ├── AppUser.cs               ← Username, PasswordHash, Role
    │   └── FeatureRequest.cs        ← Id, Title, Description, Status, submitter + timestamps
    ├── Services/
    │   ├── UserStore.cs             ← JSON-file-backed user store (Server/users.json)
    │   └── FeatureRequestStore.cs   ← Same pattern for Server/feature-requests.json
    ├── LoginPage.cs                 ← Self-contained inline HTML for the /login screen
    ├── devices.seed.json           ← Stock device library shipped with the build (tracked)
    ├── devices.json                ← Live device library, seeded from the above on first run (gitignored)
    ├── users.json                  ← User accounts + password hashes (auto-created, gitignored)
    ├── feature-requests.json       ← Submitted feature requests (auto-created, gitignored)
    ├── logo.txt / logo-stamp.txt   ← Title-block logo + stamp as data URIs (auto-created, gitignored)
    └── Program.cs                  ← Server setup, cookie auth, auth gate middleware, serves Blazor WASM
Desktop/                             ← .NET MAUI Windows desktop shell (see Desktop Wrapper below)
├── MainPage.xaml(.cs)               ← Top bar (Settings/Reload) + WebView pointed at the server
├── SettingsPage.xaml(.cs)           ← Server URL entry, persisted via Preferences
└── Desktop.csproj                  ← Windows-only target (net10.0-windows10.0.19041.0)
deploy/                              ← Deployment scripts + TLS material (see Deploying to Ubuntu Linux)
samples/                             ← Example .lf files (Riverfront-HQ-Sample.lf — 4 sheets, 46 devices)
UserGuide.md                         ← End-user documentation, linked from the app's Help menu
```

> The four server-owned data files above (`devices.json`, `users.json`, `feature-requests.json`, `logo*.txt`) are **gitignored and `<Content Remove>`d from publish** — see the deploy warning further down before adding another.

## Tech Stack
- **.NET 10** (not 8 or 9)
- **Blazor WebAssembly** (standalone, hosted by ASP.NET Core)
- **Z.Blazor.Diagrams 3.0.4.1** — diagramming library
- **jsPDF 2.5.1** + **html2canvas 1.4.1** — PDF export (CDN, loaded in index.html)
- No database — device list stored in `Server/devices.json` (seeded from the tracked `devices.seed.json`)

## Running the App
```bash
cd LineFlowAppHosted
dotnet run --project Server
# Opens at http://localhost:5052
```

## Key Architecture Decisions

### File Format (.lf)
Diagrams save as `.lf` files — JSON under the hood. A document is a set of **sheets** (one canvas each — tabs under the canvas in the app) plus document-wide shared data: `cableTypes`, `meta`, and `activeSheet` (which tab was open at save time):
```json
{
  "meta": {
    "createdBy": "jdoe",
    "createdAt": "2026-07-06T18:12:00Z",
    "modifiedBy": "asmith",
    "modifiedAt": "2026-07-06T19:05:00Z"
  },
  "activeSheet": 0,
  "cableTypes": [ ... ],
  "titleBlock": {
    "Enabled": true, "SheetSize": "11x17",
    "CompanyName": "…", "CompanyAddress": "…", "CompanyPhone": "…", "CompanyWeb": "…",
    "Client": "…", "ProjectName": "…", "Location": "…", "Discipline": "…",
    "ProjectNumber": "…", "DrawnBy": "…", "CheckedBy": "…", "Date": "…",
    "Revisions": [{ "Number": "1", "Date": "8/20/23", "Description": "ISSUED FOR CONSTRUCTION" }]
  },
  "sheets": [
    { "name": "Room 101", "drawingTitle": "AV SIGNAL FLOW", "drawingNumber": "AV-101",
      "nodes": [ ... ], "links": [ ... ], "boxes": [ ... ], "lines": [ ... ], "texts": [ ... ],
      "schedules": [{ "x": 40, "y": 40, "kind": "Cable", "allSheets": true }], "legend": { ... } }
  ]
}
```
`titleBlock` is document-wide and absent on older files (which simply get an empty, disabled one). `drawingTitle`/`drawingNumber` are per sheet — the rest of the title block is shared.
Files saved before multi-sheet existed have no `sheets` property — the root itself is the sheet content (including its own `cableTypes`); they open as a single "Sheet 1" untouched. Sheet content looks like:
```json
{
  "nodes": [
    {
      "id": "guid",
      "label": "SW-1",
      "manufacturer": "Crestron",
      "model": "DM-NVX-350",
      "category": "AV over IP",
      "ports": [{ "name": "HDMI Out", "type": "HDMI", "direction": "Out" }],
      "x": 100,
      "y": 200,
      "backgroundColor": "#16213e",
      "borderColor": "#e94560",
      "textColor": "#ffffff"
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
      "labelY": 220.0,
      "labelRotation": 90,
      "cableType": "HDMI",
      "broken": false,
      "srcTagX": 460.0, "srcTagY": 210.0, "tgtTagX": 610.0, "tgtTagY": 290.0
    }
  ]
}
```
The shared `cableTypes` array (document root) looks like:
```json
"cableTypes": [
  { "name": "HDMI", "color": "#22cc44", "prefix": "VID", "partNumber": "Belden 1694A", "cableColor": "Black" },
  { "name": "Audio", "color": "#ff8800", "prefix": "AUD", "partNumber": "Belden 8451", "cableColor": "Blue" }
]
```
`vertices` holds every bend point along the connection, in order from source to target. Older files saved a single `midX` value instead — these still load fine (see Backward Compatibility below).

`label`/`labelX`/`labelY` are omitted (or `label` is `""`) for connections with no label. `labelX`/`labelY` record the label's actual on-canvas position (which the user can drag independently — see Connection Labels below); if they're missing on an older file that only has `label`, the position is recomputed from the connection's route midpoint on open. `labelRotation` is the label's clockwise angle in degrees (0/90/180/270); it defaults to 0 when absent. Each device node also carries a `backgroundColor` (a hex string or `"transparent"`) for its block fill, a `borderColor` (hex) for its outline, and a `textColor` (hex) for its title + port labels; they default to navy `#16213e` / coral `#e94560` / white `#ffffff` when absent.

`cableTypes` is the **document-wide** (shared across all sheets) palette of user-defined cabling types (`name` + on-screen `color` + optional `prefix`, `partNumber`, and `cableColor` — the physical jacket color as free text), managed in the right-side **Cable Types** panel. The part number and cable color appear in each **legend** row (after the name) and as columns in the **Cable Schedule** CSV. The `prefix` feeds auto-labeling: **Tools → Auto-Label Connections** numbers every connection per its cable type's prefix, starting at 1 (e.g. `VID-1`, `VID-2`), in cable-schedule order (by source device/port); connections with no cable type or no prefix are left untouched, and the generated labels are ordinary labels you can still edit or move. Each link's `cableType` names the type it's assigned (omitted/`null` = unassigned → neutral gray). `broken: true` hides the connection's long middle line and instead shows two identical **label blocks** — one per end, carrying the connection's label — each tied to its port by a short stub connection (a readability aid for busy diagrams; it's purely visual — still one logical connection in the schedule/legend). The blocks are ordinary diagram nodes: drag them anywhere and the stub follows; right-click one for the connection's menu (Rejoin etc.). New blocks start just outside their port, stepped outward when others already sit on the same device side. `srcTagX/Y` + `tgtTagX/Y` record each block's canvas position (absent = default placement on load); the blocks and stubs themselves are derived state, rebuilt from these fields rather than saved as first-class nodes. A connection's on-screen color comes from its cable type's color; port dots are always black. This replaces the old auto-coloring by signal type — the port `Type` field is retained as data (shown in the Cable Schedule "Signal" column) but no longer drives any color, and the legend now lists cable types.

`meta` is stamped automatically on Save: the first save of a new diagram sets `createdBy`/`createdAt` to the signed-in user and current time; every save (including the first) updates `modifiedBy`/`modifiedAt`. It's read back on Open and shown in a thin info bar under the toolbar (e.g. "Created by jdoe on Jul 6, 2026 3:12 PM · Last modified by asmith on Jul 6, 2026 4:05 PM"). Files saved before this feature existed simply have no `meta` block — they open fine, the info bar just stays hidden until the next save.

### Device Library
- Stored server-side in `Server/devices.json`, which is **gitignored and never published** — it belongs to the server, so deploys can't overwrite devices your users added. On first run `DevicesController` copies the tracked `Server/devices.seed.json` into place, so a fresh install still gets the full stock library. Editing the seed in git changes what NEW installs start with; it deliberately does not push devices onto running servers
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
- **Forced change off the placeholder** — new accounts are handed out with `AuthController.TemporaryPassword` (`"TempPassword"`). `/api/auth/status` reports `usingTemporaryPassword` by verifying that string against the **stored hash** rather than remembering it from login, so it stays correct for someone already holding a 30-day cookie and for an admin resetting an account back to the placeholder. While it's true the app shows a blocking modal (`.lf-modal-overlay-blocking`) that can't be dismissed until the password is changed — the drawing is unreachable behind it. Costs one hash verification per page load.
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

**The rest of the API** (everything below is behind the same auth gate — an unauthenticated `/api/*` request gets a 401):
| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `/api/devices` | GET/POST | signed-in | Read / replace the shared device library (`Server/devices.json`) |
| `/api/logo?slot=logo\|stamp` | GET/POST/DELETE | signed-in | The title block's company logo and engineer's stamp, stored as data URIs. `slot` resolves against a fixed whitelist since it names a file |
| `/api/featurerequests` | GET/POST | signed-in | List / submit feature requests |
| `/api/featurerequests/{id}` | PUT | signed-in | Edit the text; changing the **status** is Admin-only (403 otherwise) |
| `/api/featurerequests/{id}` | DELETE | Admin | Remove a request |

### Code layout
`Home.razor` still holds the page: markup, event handling, undo, sheet switching, and `.lf` serialization. What it no longer holds is anything that doesn't need the component — those moved to plain `.cs` files so they can be read, changed, and eventually tested on their own:

- **`Client/Diagrams/`** — the model types (`namespace Client.Diagrams`). They were nested inside `Home`, so widgets had to say `Home.BoxNode`; they're now top-level.
- **`Client/Export/DxfWriter.cs`** — `DxfWriter.Build(diagram, cableTypes, titleBlock, sheet, title, scheduleTable)` returns the DXF as a string. No component state, no JS interop; `Home.ExportDxf` is now four lines that hand the result to `saveAsFile`.
- **`Client/Export/ScheduleSource.cs`** — a record over `(diagram, sheets, activeIndex, cableTypes, neutralColor)` with `Conns`, `DeviceCounts`, and `CableCounts` on it. Home exposes it as the `Schedules` property; every schedule in the app reads through it.

### Custom Classes (defined in `Client/Diagrams/`)
- **`LineFlowNode`** — extends `NodeModel`, holds `DeviceDefinition`, creates `LineFlowPort` instances. Also carries a per-**instance** `Label` (the device tag, e.g. `SW-1`), serialized as `label` — without it two of the same model are indistinguishable on the drawing and in the cable schedule
- **`LineFlowPort`** — extends `PortModel`, holds `PortDefinition` with name/type/direction
- **`ElbowLinkModel`** — extends `LinkModel`; routing is driven by its `Vertices` collection (one or more draggable bend points). `MidX` is kept only as a legacy fallback for old saves with no vertices. `CableTypeName` names its assigned cable type; `Color`/`SelectedColor` are set from that type's color (neutral gray when unassigned) via `ApplyCableColor`. `LabelText`/`LabelNode` hold its optional connection label (see Connection Labels below). (`ColorForType()` — the old signal-type coloring — is retained but no longer used.)
- **`ConnectionLabelNode`** — extends `NodeModel`, a draggable text bubble for a connection label; rendered by `ConnectionLabelWidget.razor`. `OwnerLink` points back at its `ElbowLinkModel` so deleting either side cleans up the other. `Rotation` holds a clockwise angle in 90° increments (0/90/180/270), applied as a CSS transform (see Connection Labels below).
- **`ElbowRouter`** — extends `Router`, generates an orthogonal H-V-H-...-H path through all of the link's vertices, in order
- **`LegendNode`** — extends `NodeModel`, holds a list of `(Name, Color, PartNumber, CableColor)` entries; rendered by `LegendNodeWidget.razor`. Created/updated by **Tools → Legend** from the **cable types** actually used by current connections (it used to key off port signal types — that changed when cable types took over connection coloring).
- **`DeviceDefinition`** — manufacturer, model, category, list of ports
- **`PortDefinition`** — name, type, direction (In/Out/Universal). The **type is free text**, not an enum: the Add/Edit Device form is an `<input list>` whose suggestions are the stock types (`BuiltInPortTypes`) unioned with every type already used anywhere in the library, so a new one (Dante, AES67, 12G-SDI…) becomes a suggestion for everyone once saved — nothing to keep in sync and no orphaned entries
- **`BoxNode`** — extends `NodeModel`, resizable rectangle with a border color and no fill; rendered by `BoxNodeWidget.razor`, corner handles drag-resize via `Home.StartResize`
- **`LineNode`** — extends `NodeModel`, freeform 2-point line (`Start`/`End`) not attached to any port; rendered by `LineNodeWidget.razor`, endpoint handles drag via `Home.StartResize`
- **`TextNode`** — extends `NodeModel`, editable text label with `FontSize`/`Color`; rendered by `TextNodeWidget.razor`, double-click to edit, style row appears when selected
- **`ScheduleNode`** — extends `NodeModel`, a Cable Schedule / Device Schedule / Cable Count table placed on a sheet; rendered by `ScheduleNodeWidget.razor`. Holds `Kind` and `AllSheets` and **no rows** — the widget asks `Home.BuildScheduleTable` on every render, so the table always reflects the diagram under it
- **`BreakTagNode`** — extends `NodeModel`, one of the two identical label blocks shown at the ends of a **broken** connection (`Text`, `Color`, `OwnerLink`, `IsSource`); rendered by `BreakTagWidget.razor`. Derived state — rebuilt from the link's `broken` + `srcTagX/Y`/`tgtTagX/Y` fields rather than saved as first-class nodes
- **`BreakStubLink`** — extends `LinkModel`, the short straight run from a port to its `BreakTagNode`. `ElbowRouter` ignores links whose target isn't a `PortModel`, so these render straight with no bends for free. Locked — the tag block is what users grab, not the stub
- **`CableType`** — name, on-screen `Color`, label `Prefix`, `PartNumber`, physical `CableColor`. Document-wide (shared by every sheet), managed in the right-hand panel
- **`TitleBlockInfo`** / **`TitleBlockRevision`** — the document-wide title block: company details, client/project/location/discipline, issue fields, `SheetSize` (`11x17` default / `Letter` / `A4`), and the revision rows. The per-sheet drawing title and number live on `Sheet`, not here
- **`Sheet`** — one tab: `Name`, `DrawingTitle`, `DrawingNumber`, and `Json` — the parked serialization of that sheet's canvas. Only the **active** sheet is live in `_diagram`; the others sit as JSON and round-trip through the same `BuildDiagramData`/`LoadDiagramJson` path undo and `.lf` saving use

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
- **Rotatable in 90° steps.** Right-click a label → **↻ Rotate Label 90°** cycles it clockwise 0 → 90 → 180 → 270 → 0. Stored as `ConnectionLabelNode.Rotation` and applied by the widget as `transform: translate(-50%, -50%) rotate(Ndeg)` — the translate keeps the bubble centered on its anchor point while it spins in place. Because it's a plain CSS transform on a real DOM node, PDF export (html2canvas) captures the rotated label automatically, no export-side work needed.
- Persisted in `.lf` files as `label`, `labelX`, `labelY`, `labelRotation` on each link (see File Format below) so a manually-repositioned and/or rotated label survives save/reload exactly where it was left. `labelRotation` is re-normalized to 0–359 on load, and defaults to 0 for older files that predate the field.
- Included in DXF export as a centered `TEXT` entity on the `CONNECTIONS` layer, positioned at the label node's actual (possibly user-dragged) position — not recomputed — for consistency with what's on screen. Its rotation is emitted via DXF group code `50`; the screen's clockwise angle maps directly to DXF's counterclockwise-positive convention because the export flips the Y axis.

### Undo/Redo
Snapshot-based, not command-based: every mutation pushes a full serialized-diagram JSON snapshot (via `SerializeDiagramState()`, which shares `BuildDiagramData()` with `.lf` saves) onto `_undoStack` (capped at 50); undo/redo restore by rebuilding the whole canvas through `LoadDiagramJson()` — the same code path as file open, so restore fidelity is guaranteed to match save/open fidelity. Snapshots exclude the `meta` block so undo never rewrites the created-by/modified-by info bar. History is **per sheet** (each sheet parks its own stacks when inactive); New and Open replace the whole document, so they confirm when there are unsaved changes rather than going on the stack.

- **Discrete operations** (place device/annotation, draw connection, add/remove bend, label changes, deletes, align/distribute, legend) call `PushUndoSnapshot()` *before* mutating.
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
- `window.setUnsavedChanges(bool)` — arms/disarms the `beforeunload` guard so closing the tab with unsaved work prompts (see Unsaved-Changes Warning)
- `window.rasterizeSvg(dataUri, maxPx)` — renders an uploaded SVG (logo or stamp) to a PNG data URI, because jsPDF's `addImage` takes raster input only. Returns a rejected promise with a readable message for a malformed SVG or one that links to an external image (tainted canvas)
- `window.getCanvasAreaSize()` — returns the canvas viewport `[width, height]` in px (used by the middle-click zoom-to-fit to compute the framing transform; not used by current `ExportPdf`)
- `window.registerMiddleDblClickZoom(dotNetRef)` — called once on first render with a `DotNetObjectReference<Home>`; binds a capture-phase `mousedown` listener on `.canvas-area` that suppresses the middle button's default (autoscroll/paste) and, on a double middle-press within 400 ms, invokes `[JSInvokable] ZoomToFitContent()` to frame the whole diagram
- `window.exportToPdf(title, version, bx, by, bw, bh, titleBlock)` — captures the diagram with html2canvas and generates a PDF with jsPDF (content fit to the page preserving aspect ratio, version stamped in the header). When content bounds are provided it captures the **full diagram** via an `onclone`-restyled copy of the page (see the PDF export feature bullet); with no bounds it falls back to capturing the visible view and trimming it with `cropToContent`. `titleBlock` carries the whole title-block payload — including `sheetSize`, which picks the paper (`tabloid`/`letter`/`a4`, always landscape) **whether or not the block itself is enabled** — plus the logo and stamp data URIs

Two module-level helpers are not on `window` (called only from `exportToPdf`):
- `drawTitleBlock(pdf, tb, pageW, pageH, version)` — draws the border and the right-edge title-block strip, and **returns the rectangle left over** for the diagram to be fitted into
- `cropToContent(canvas)` — trims surrounding whitespace off a capture so the drawing fills the page

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
- `.lf-node-label` — the amber per-device tag (`SW-1`) drawn above the node title
- `.lf-break-tag` — the label block at each end of a broken connection
- `.lf-coord-chip` — the selected device's X/Y readout, bottom-left of the canvas. `pointer-events: none` so it can't intercept a click
- `.lf-schedule*` — a schedule table placed on a sheet (same visual family as the legend, sized for more rows)
- `.lf-cc-*` — the Cable Count dialog (`-table`, `-row-on`, `-qty`, `-total`, `-actions`)
- `.lf-tb-*` / `.lf-fr-*` — the Title Block editor and the Feature Requests dialog
- **Stacking order** — `.diagram-node:has(…)` rules give annotations `z-index: 1` and devices/labels `z-index: 2`, so boxes and lines sit behind the blocks. `.lf-links-on-top .diagram-svg-layer` (Tools → Connections On Top) raises the whole connection layer above them so a bend handle hidden under a device can be grabbed; it needs `!important` because the library writes that layer's `z-index` as an **inline** style

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

### Quick path — `deploy/deploy.ps1`
If you have a systemd service already set up (see the manual steps below for the first-time setup), the bundled script does the whole publish → copy → install → restart cycle from your Windows dev box:

```powershell
./deploy/deploy.ps1 -Deploy
```

It prompts for your server's IP/hostname and SSH username (or pass `-Server user@host`, or set `$env:LOOPBACK_SERVER`), publishes the Server build, copies it up, and runs the server-side installer over SSH — which drops the build into the app folder and restarts the service. The server-side half is `deploy/deploy.sh`, configurable via `APP_DIR` / `STAGING` / `SERVICE` env vars (defaults: `$HOME/lineflowapp`, `$HOME/loopback-staging`, `lineflow`). Omit `-Deploy` to copy only and finish the install by hand.

> ⚠️ **Server-owned data and the publish output.** The install step is a plain overwrite (`cp -r staging/* APP_DIR/`), so anything present in the publish output replaces the server's copy. `users.json`, `devices.json`, `logo.txt`, `logo-stamp.txt`, and `feature-requests.json` survive **only** because `Server/Server.csproj` explicitly `<Content Remove>`s them — the Web SDK publishes `**/*.json` by default, and before that exclusion existed a deploy carried the dev machine's `users.json` up and wiped a production user list. **If you add another server-owned data file, add a matching `<Content Remove>` in the csproj** rather than relying on a deploy script to exclude it.

### Manual steps
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

## Serving over HTTPS on a fully-local network (no public domain / no admin)

Browsers **block downloads** (PDF/DXF/CSV/.lf exports) initiated from an insecure `http://` origin — you get "Insecure download blocked". The fix is to serve the app over `https://`, which needs a certificate the client browsers trust. With no public domain and no company CA, you run your **own** tiny CA: issue a cert for the server's IP and have each user trust the root once. No admin rights are needed on either side.

1. **Generate a CA + server cert** (on the server, or in Git Bash on your dev box — both have `openssl`):
   ```bash
   ./deploy/gen-cert.sh 192.168.1.200        # your server's LAN IP
   ```
   This writes `deploy/certs/{rootCA.crt,rootCA.key,server.crt,server.key}` and stages the public `rootCA.crt` into `deploy/client-cert/`. The server cert carries the IP as a SAN, so clients use `https://<ip>:5052` with no DNS/hosts changes. **Keep `rootCA.key` private; never distribute it.**
2. **Install the cert on the server:** copy `server.crt` + `server.key` into `<app-dir>/certs/` (e.g. `/home/jewaldt/lineflowapp/certs/`). At startup `Program.cs` checks for `certs/server.crt` + `certs/server.key` (relative to the app dir) and, **if both exist**, uses them as Kestrel's default HTTPS certificate. The check is optional by design — a server with no cert (like the Cloudflare-fronted dev box) simply serves HTTP, so the same build runs on both without crashing. Like `users.json`, this folder lives only on the server and deploys never overwrite it.
3. **Switch the service to HTTPS:** in the systemd unit change the URL scheme —
   ```ini
   Environment=ASPNETCORE_URLS=https://0.0.0.0:5052
   ```
   then `sudo systemctl daemon-reload && sudo systemctl restart lineflow`. (Kestrel picks up the cert that `Program.cs` auto-detected in `certs/`.)
4. **Each user trusts the root once (no admin):** hand out the `deploy/client-cert/` folder (`install-cert.bat` + `LoopbackRootCA.crt`). They double-click `install-cert.bat` — it imports the root into their **current-user** trust store via `certutil -user -addstore Root`, which Chrome, Edge, and the desktop app's WebView2 all honor. Firefox users additionally set `security.enterprise_roots.enabled` to `true` in `about:config`.
5. Everyone browses **`https://192.168.1.200:5052`**, and point the desktop app's Settings → server URL at the same. Downloads now work with no warnings.

Verified: with the CA trusted, `curl --cacert rootCA.crt https://<ip>:5052/` returns `302` with `ssl_verify_result=0`; without it the TLS handshake is rejected.

### Confirming a deploy landed
The build version is shown in the toolbar (next to the "Loopback" title) and in the PDF export header, sourced from `Client/AppVersion.cs`. **Bump `AppVersion.Version` before each deploy** — then after copying the new build over and restarting the service, load the app and check the toolbar shows the new number. If it doesn't, the new build didn't actually land (wrong folder, service not restarted, or a browser cache holding the old page — hard-refresh with Ctrl+F5). Because the app is a Blazor SPA, a browser holding a **stale `index.html`** alongside a new WASM DLL can misbehave (e.g. PDF export silently falling back to capturing only the visible view); a hard refresh after every deploy avoids this.

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
- ✅ Server-side device library with add, edit, delete, and **duplicate** devices, via a centered modal dialog (not inline in the side panel); duplicating opens the Add-Device modal pre-filled with a copy of the device (ports included, model suffixed "Copy") so a near-identical device is a couple of edits away
- ✅ Device list sorting by Type or Manufacturer, case-insensitive grouping
- ✅ Device position readout — clicking a device shows a chip in the canvas's bottom-left with its tag/model and top-left **X / Y** (the coordinates the `.lf` stores, and what align/nudge operate on). Multi-select reads the first plus a `+N more` count; the chip is `pointer-events: none` so it can't intercept a click
- ✅ Deploys land without a hard refresh — `/_framework` entry scripts (`blazor.webassembly.js`, `dotnet.js`, and any `.json`/`.map`) are served `no-cache` so browsers revalidate them each load, while everything else there is fingerprinted and served `immutable` for a year. Those two fixed-name files carry the map of the fingerprinted ones, so caching them kept clients on the *entire* previous build no matter how fresh `index.html` was — which is why deploys used to need a Ctrl+F5 and the toolbar kept showing the old version. `UseBlazorFrameworkFiles` takes no `StaticFileOptions`, so the header is stamped by a small middleware in front of it via `OnStarting`
- ✅ Schedules placed on the drawing — **Tools → 📌 Place Cable / Device Schedule / Cable Count** drops the CSV contents onto the sheet as a table. `ScheduleNode` stores only position, `Kind`, and `AllSheets`; the rows are re-read from the diagram by `BuildScheduleTable` on every render, so a placed table can't go stale (there is nothing to refresh and nothing to keep in sync). With multiple sheets you're asked the scope on placement — per-sheet pull list vs. master list — and right-click flips it later. Renders in the PDF capture like any other node, and in the DXF as a real CAD table on a `SCHEDULES` layer, drawn from the same rows the widget shows
- ✅ One schedule data path — `ScheduleConns`/`ScheduleDevices` back the CSV exports, the Cable Count dialog, and the placed tables alike, so a schedule printed on a drawing cannot disagree with the spreadsheet. The **active** sheet is read from the live diagram (a placed table renders continuously, and `AllSheetRoots` parks the active sheet — a side effect that must not happen mid-render); inactive sheets can't have changed since they were parked, so their JSON is authoritative
- ✅ Cable count (takeoff) — **Tools → 🧮 Cable Count…** tallies connections per cable type across the whole document, one column per sheet when there's more than one, so a rack's patch-cord count reads straight off a row. Untyped connections get their own row; tick/untick types for a running subtotal, and ⬇ CSV writes the selection to `cable-count.csv` with a TOTAL row. A snapshot taken when the dialog opens
- ✅ Clear connection labels — **Tools → 🧹 Clear Connection Labels** strips every cable tag on the active sheet, the counterpart to Auto-Label. Confirms with a count first (a label can be hand-typed work, not just auto-generated) and warns when any of them belong to broken connections, whose label blocks are left blank. One undo step. Device tags are untouched
- ✅ Canvas stacking order — the diagramming library renders two absolutely-positioned sibling layers in `.diagram-canvas` (an SVG layer for connections, an HTML layer for nodes), both at `z-index: 0`, with the HTML layer later in the DOM — so nodes always paint over connections, and node-vs-node order is plain DOM order (`.diagram-node` is `z-index: auto`). Handled with CSS only, via `:has()` on the wrappers: **boxes and lines are pinned behind** everything (`z-index: 1` vs `2`) so a box drawn around a rack no longer swallows clicks on the devices inside it — text is deliberately left with the devices, since a callout is usually placed over a block on purpose. **Tools → 🔀 Connections On Top** adds `.lf-links-on-top` to the canvas, raising the whole SVG layer to `z-index: 3` so a bend handle sitting underneath a device can be grabbed; it needs `!important` because the library writes that layer's z-index as an inline style. It's a toggle rather than the default because while on, connections also intercept clicks on the blocks. View-only — not saved in `.lf`
- ✅ Per-device labels — right-click a device → 🏷 **Device Label** tags that specific placed block (`SW-1`, `AMP-2`…). The tag belongs to the instance, not the library device, and is saved as `label` on the node in `.lf`. It renders above the model name on the block, prefixes the DXF node text as `[SW-1] Model`, and populates **From Tag** / **To Tag** in the Cable Schedule. Without it, a run between two units of the same model exports as `Model Port 26 → Model Port 26`, which reads as a port patched back into itself rather than an interconnect
- ✅ Extensible port signal types — the port **type** field is free text backed by a `<datalist>` whose suggestions are the built-in set (`HDMI`/`SDI`/`Audio`/`Network`/`USB`/`IR`/`COM`/`Other`) unioned with every type already used across the shared library, de-duplicated case-insensitively. A new type (Dante, AES67, 12G-SDI…) is added simply by typing it once; it becomes a suggestion for everyone when the device is saved. Deliberately derived from usage rather than kept in a separate managed list — the types already live on every port in `devices.json`, so a second authoritative list would only create a sync problem (orphan entries, used-but-unlisted types) and another server-owned file to protect on deploy
- ✅ Cable types — define your own cabling types (name + on-screen color + optional label prefix, part number, and physical cable/jacket color) in the right-side **Cable Types** panel; drawing a connection prompts you to pick one (required, with an unassigned/neutral fallback if none exist yet), and you can reassign via right-click → 🔌 Cable Type. Editing a type's color live-recolors every connection using it. Ports render black; connection color comes from the assigned cable type. Per-diagram, saved in `.lf`.
- ✅ Auto-label connections — **Tools → 🔢 Auto-Label Connections** labels every connection by its cable type's prefix, numbered from 1 (e.g. `VID-1`, `VID-2`), in cable-schedule order; regenerates on each run and leaves the manual right-click → Add/Edit Label available for tweaks
- ✅ Break connections — right-click a connection → 🔗 **Break/Rejoin**, or **Tools → Break All / Rejoin All**. A broken connection hides its long line and shows two identical label blocks (one per port), each tied to its port by a short stub connection — a reader matches the blocks by label, decluttering busy flows. Purely visual (schedule/legend/color unchanged), persisted in `.lf`, and mirrored in DXF. Breaking one that has no label prompts for a tag first; Break All auto-labels first. The blocks are ordinary draggable nodes — the stub follows wherever you put them — and right-clicking a block (or its stub) opens the connection's menu since the full line is invisible. Deleting a block rejoins its connection.
- ✅ Legend node — click "Legend" to add a draggable canvas node with a column-headed **table** of the cable types actually used in the current diagram (swatch · Cable Type · Part # · Color; the Part #/Color columns appear only when set)
- ✅ PDF export (white background, title + date + version header, direct download) — captures the **entire** diagram regardless of size or where the user has panned/zoomed, without ever touching the live view. How: `ExportPdf` computes the diagram's raw-coordinate bounds (node boxes **plus connection routing vertices** — elbow bends can extend far beyond the nodes they join) and passes them to `exportToPdf`, which restyles the **clone** html2canvas renders (`onclone`): the canvas area and layers are resized to the full bounds, the SVG layer gets a matching `viewBox`, and the HTML layer a matching translate. The `viewBox` is the load-bearing part: **html2canvas rasterizes inline SVGs clipped to the SVG element's own box**, and the connection lines live in an SVG layer sized to the visible viewport while using raw diagram coordinates — so any line geometry beyond the viewport's pixel size was silently cut from captures (while rendering fine live via `overflow: visible`). No zoom-the-diagram-first approach can fix that, which is why earlier attempts kept clipping lines; remapping coordinates with a `viewBox` in the rasterized clone does. Output is capped at ~8000px per side and placed on the page preserving aspect ratio.
- ✅ DXF export (AutoCAD compatible), generating the same multi-segment path as the live app's routing. Layers: `NODES`, `ANNOTATIONS`, a **`CABLE-<TYPE>` layer per cabling type** used on the sheet (carrying that type's connections, labels, and break tags, and colored with the nearest ACI match to its on-screen color, via `DxfLayerForCable`/`DxfAciColor`), plus `CONNECTIONS` for connections with no type and `LEGEND` for the legend. The legend is redrawn as a real CAD table (frame, header rule, column headers, content-measured column widths, and a filled `SOLID` swatch per row); each swatch is placed on its cable type's layer so recoloring that layer updates the key too. Layer names are sanitized to R12 rules (uppercase, ≤31 chars, no spaces or `< > / \ " : ; ? * | = '`) and de-duplicated when two type names collapse to the same name. Text uses a `LOOPBACK` style defined in the file's STYLE table with a TrueType font (`arial.ttf`, set via `DxfTextStyle`/`DxfTextFont` in `Home.razor`) and referenced by group code 7 on every TEXT entity — without that, CAD falls back to the `Standard` style and renders everything in the `txt.shx` stick font
- ✅ Cable schedule export (Export → Cable Schedule) — CSV pull sheet with one row per connection **across every sheet** (Sheet column included): cable label, cable type, part number, cable color, signal type, source device + port, destination device + port; sheets in tab order, sorted by source device within a sheet, CSV-escaped, UTF-8 BOM so Excel opens it cleanly
- ✅ Zoom and pan on canvas
- ✅ Freeform annotations — Box (resizable rectangle, no fill), Line (2-point freeform line with draggable endpoints, not attached to ports), and Text (click-to-place, editable, with font size/color controls); all three are selectable/deletable, saved in `.lf` files, and included in PDF and DXF exports (DXF `ANNOTATIONS` layer)
- ✅ Per-user authentication — cookie-based login gating the entire app, first-run admin setup, Admin/User roles, in-app "Manage Users" page, account menu with Logout (see Authentication & User Management above)
- ✅ Password management — self-service "Change Password" for any user (verifies current password) and admin password reset per user on the Manage Users page
- ✅ Forced password change on first sign-in — accounts are created with a placeholder (`AuthController.TemporaryPassword` = `TempPassword`); while an account still uses it, `/api/auth/status` reports `usingTemporaryPassword` and the app is **blocked** by a non-dismissible dialog (no ✕, no "later", and the backdrop swallows clicks) until the user sets their own. The change-password dialog is locked open too while the flag is set — otherwise closing *it* would be a way straight past the gate. Checked against the **stored hash** rather than remembered from sign-in, so it's correct for users already holding a 30-day cookie and re-arms if an admin resets someone back to the placeholder. Costs one hash verification per page load; admins are not exempt
- ✅ File authorship tracking — `.lf` files record who created and who last modified them, and when, shown in an info bar under the toolbar
- ✅ Connection labels — right-click a connection to add/edit a text label (e.g. "VID-005"); draggable, rotatable in 90° increments (right-click the label → ↻ Rotate Label 90°), saved in `.lf` files, included in DXF export, and PDF-export-safe (see Connection Labels above for why that needed a custom rendering path)
- ✅ Windows desktop wrapper (`Desktop/`, .NET MAUI) — native window shell with a configurable server address (Settings page, persisted via `Preferences`), not tied to a hardcoded URL (see Desktop Wrapper above)
- ✅ Zoom-to-fit — double-click the middle mouse button (mouse wheel) anywhere on the canvas to frame the entire diagram in the viewport; a recovery gesture for when you've zoomed/panned the content out of view (`ZoomToFitContent` in `Home.razor`, wired via `registerMiddleDblClickZoom` in `index.html`)
- ✅ Undo/redo — Ctrl+Z / Ctrl+Y (canvas focused) or toolbar ↩/↪ buttons; covers placements, connections, bends, labels, moves, resizes, text edits, and deletes. History is **per sheet**; New and Open are guarded by an unsaved-changes confirm instead of undo (see Undo/Redo above)
- ✅ Legend persistence — legends are now saved in `.lf` files and restored on open
- ✅ Unsaved-changes warning — browser prompt before closing the tab with unsaved work, plus an amber "● Unsaved" toolbar badge (see Unsaved-Changes Warning above)
- ✅ Copy/paste & duplicate — Ctrl+C / Ctrl+V or right-click → Duplicate; copies selected nodes plus the connections (and labels) between them (see Copy / Paste above)
- ✅ Per-block fill, outline & text color — select a device block to reveal a small style bar with **Fill** (+ a transparent toggle), **Line** (outline), and **Text** color pickers; all are saved in `.lf` files and captured in PDF export. Default to navy fill / coral outline / white text.
- ✅ Bulk device recolor — **Tools → 🎨 Device Colors** opens Fill/Line/Text pickers (with a transparent-fill option) that apply to **every** device block on the canvas at once; each change is a single undo step (`OpenDeviceColors`/`ApplyToAllDevices` in `Home.razor`)
- ✅ Build version display — shown in the toolbar next to the app title and stamped into the PDF export header, sourced from `Client/AppVersion.cs`, so you can always confirm which build a server is running (see Confirming a deploy landed above)
- ✅ Device library search — a filter-as-you-type box above the palette matching manufacturer, model, or category (case-insensitive), with a one-click clear
- ✅ Align & distribute — **Tools → Align Left/Right/Top/Bottom** (2+ selected blocks) and **Distribute Horizontally/Vertically** (3+ selected; outermost blocks stay put, the middle ones spread to even center spacing); works on any selectable blocks, one undo step per action
- ✅ Keyboard nudge — arrow keys move the selected blocks 1px, **Shift+arrow** 10px (canvas focused); a burst of presses coalesces into a single undo step
- ✅ Device schedule export (Export → Device Schedule) — BOM-style CSV of the devices **across every sheet**, one row per manufacturer+model with quantity and category; sorted by category then manufacturer, UTF-8 BOM for Excel
- ✅ Multi-sheet diagrams — Excel-style tabs under the canvas: click to switch, ＋ to add, double-click to rename, ✕ (with confirm) to delete. Each sheet is an independent canvas with its own undo history; **cable types, title, and file meta are shared document-wide**. Only the active sheet is live — inactive sheets are parked as serialized JSON (the same round-trip format undo and `.lf` files use) and swapped in on switch. Saved as a `sheets` array in `.lf` (older single-sheet files open as "Sheet 1"); both schedule CSVs span all sheets, PDF/DXF export the active sheet (the PDF header names the sheet when there's more than one), and copy/paste works across sheets. Since New/Open replace the whole document, they now **confirm when there are unsaved changes** instead of relying on undo (undo history is per sheet and never spans documents)

- ✅ Signal path trace — right-click a device or connection → 🔦 **Trace Signal Path**: walks the connection graph and lights everything electrically reachable (traced connections go full-color + thick, traced devices get an amber glow; everything else fades to ~15%). Broken connections are still single logical links, so a trace flows straight through them and lights their tag blocks. Pure view state — never saved, never on the undo stack, cleared by Esc, clicking empty canvas, the floating "✕ clear" chip, or any sheet switch/load


- ✅ Drawing title block — **Tools → 📋 Title Block** turns the exports into proper drawing sheets: a double border with a vertical title-block strip down the right edge holding an uploadable company logo, right-aligned company details, a 90°-rotated client/project/location/discipline stack, a stamp area, a revision grid, project-number/drawn-by/checked-by/date pairs, and the drawing title + number. Sheet size is selectable per diagram (11×17 / Letter / A4, landscape). Everything is document-wide except the **drawing title and number, which are per sheet**, so each tab prints its own. Rendered in the PDF (`drawTitleBlock` in `index.html`, which returns the leftover rectangle the diagram is fitted into) and in the DXF on a `TITLEBLOCK` layer — DXF is model space with no notion of paper, so the sheet is sized to the chosen aspect ratio and scaled up until the tracked drawing extents fit inside its drawing area, then centered

- ✅ Title block logo + stamp — upload a PNG/JPEG/SVG in the Title Block editor for either slot; stored **server-side** as data URIs via `LogoController` (`Server/logo.txt` and `Server/logo-stamp.txt`, both gitignored and both `<Content Remove>`d from publish), so one set is shared by every diagram and every user rather than being re-uploaded per project and carried as base64 inside each `.lf`. The slot is a `?slot=` query param resolved against a fixed whitelist — never a caller-supplied path fragment, since the value names a file. Each is scaled into its reserved box preserving aspect ratio (the stamp centers under the STAMP label); with nothing uploaded — or an image jsPDF can't decode — the box falls back to an empty outline. **PDF only** — DXF R12 predates embedded raster images
- ✅ SVG uploads — `rasterizeSvg` in `index.html` renders an uploaded SVG to a 1400px PNG at upload time, because jsPDF's `addImage` takes raster input only; converting once keeps a single stored format and leaves every export path untouched. Intrinsic size comes from the `viewBox` (unitless) rather than `width`/`height`, which are often percentages or carry units, and the output size is pinned on the root element before handing it to an `<img>` — Chrome won't rasterize an SVG with no concrete size. An SVG referencing an external image taints the canvas and is rejected with an explanation instead of silently producing a blank box

- ✅ Feature requests — a **💡 Feature Requests** toolbar button opens a shared list anyone signed in can read and add to. New requests start at `Received`; statuses are `Received` / `WIP` / `Done` / `Declined`. Permissions: **anyone** submits and views; **the author** may edit their own wording *only while it's still `Received`*, so the text can't shift once work has started; **admins** edit anything, set any status, and delete. The author check is evaluated against the *stored* status inside `FeatureRequestStore`'s lock, not against anything the client sends, so a stale page can't be used to edit an in-progress request. Backed by `Server/feature-requests.json` (gitignored), same no-database pattern as `users.json`

## Features Planned / Not Yet Implemented
- ⬜ Nothing outstanding — see the git history for what shipped most recently

## License
[PolyForm Noncommercial 1.0.0](LICENSE) — free to use, modify, and share for any noncommercial purpose. Commercial use requires separate permission from the copyright holder. Note this is a "source-available" license, not an OSI-approved open source license — the Open Source Definition explicitly prohibits restricting commercial use, which is exactly the restriction this project needs.
