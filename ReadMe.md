# LineFlow

## What This Project Is
LineFlow is a web-based AV/IT signal flow diagram application built with Blazor WebAssembly (.NET 10) and ASP.NET Core. It allows users to create, save, and open line flow diagrams (like those used in AV system design) with drag-and-drop devices, labeled ports, and smart elbow-routed connections.

## Project Structure
```
LineFlowAppHosted/
├── Client/                         ← Blazor WASM app (runs in browser)
│   ├── Pages/
│   │   ├── Home.razor              ← Main diagram page (ALL core diagram logic lives here)
│   │   ├── LineFlowNodeWidget.razor ← Custom node renderer with port labels
│   │   ├── TextNodeWidget.razor     ← Freeform text annotation renderer
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
      "vertices": [{ "x": 450.5, "y": 220.0 }, { "x": 600.0, "y": 300.0 }]
    }
  ]
}
```
`vertices` holds every bend point along the connection, in order from source to target. Older files saved a single `midX` value instead — these still load fine (see Backward Compatibility below).

`meta` is stamped automatically on Save: the first save of a new diagram sets `createdBy`/`createdAt` to the signed-in user and current time; every save (including the first) updates `modifiedBy`/`modifiedAt`. It's read back on Open and shown in a thin info bar under the toolbar (e.g. "Created by jdoe on Jul 6, 2026 3:12 PM · Last modified by asmith on Jul 6, 2026 4:05 PM"). Files saved before this feature existed simply have no `meta` block — they open fine, the info bar just stays hidden until the next save.

### Device Library
- Stored server-side in `Server/devices.json`
- Loaded via `GET /api/devices`
- New devices added via `POST /api/devices`
- Default device: Crestron DM-NVX-350 with 8 ports

### Authentication & User Management
The whole app sits behind a login gate — added so it can be safely exposed to the internet (e.g. via a Cloudflare Tunnel) while still letting individual users' access be revoked (e.g. when someone leaves the job) without touching anyone else's account.

- **Cookie-based auth** — `Microsoft.AspNetCore.Authentication.Cookies` (ships with the ASP.NET Core shared framework, no extra NuGet package needed). 30-day sliding-expiration cookie.
- **No database** — accounts live in `Server/users.json` (same file-backed pattern as `devices.json`), hashed with `Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>` (also part of the shared framework). This file is **gitignored** since it contains password hashes.
- **Two roles**: `Admin` (can manage users) and `User` (diagram access only).
- **First-run setup**: if `users.json` has no accounts yet, hitting the site shows a "Create admin account" screen instead of a normal login form. The first account created becomes the Admin.
- **Whole-app gate**: custom middleware in `Server/Program.cs` blocks every request — including the WASM framework files themselves — except `/login` and `/api/auth/*`, unless the request carries a valid auth cookie. Unauthenticated browser requests get redirected to `/login`; unauthenticated `/api/*` requests get a 401.
- **Login page** (`Server/LoginPage.cs`) is a small self-contained inline HTML/CSS/JS page served via a minimal API endpoint (`GET /login`) — there's no `Server/wwwroot`, so a static file wasn't worth adding. It calls `/api/auth/status` on load to decide whether to show setup mode or a normal login form.
- **Manage Users page** (`Client/Pages/Users.razor`, route `/users`) — a real Blazor page, admin-only (both server-enforced via `[Authorize(Roles = "Admin")]` on `UsersController` and gated client-side by checking `/api/auth/status`). Lists accounts, adds new ones with a chosen role, and removes accounts (blocked from deleting your own account or the last remaining Admin).
- **Account menu** — top-right of the toolbar in `Home.razor`, shows the current username, a "Manage Users" link for Admins, and Logout.

**Endpoints:**
| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `/api/auth/status` | GET | none | Whether any users exist, whether the caller is signed in, and their username/role |
| `/api/auth/setup` | POST | none (only works once, before any users exist) | Create the first (Admin) account and sign in |
| `/api/auth/login` | POST | none | Sign in with username/password |
| `/api/auth/logout` | POST | any | Clear the auth cookie |
| `/api/users` | GET/POST | Admin | List / create accounts |
| `/api/users/{username}` | DELETE | Admin | Remove an account |

### Custom Classes (all defined inside Home.razor @code block)
- **`LineFlowNode`** — extends `NodeModel`, holds `DeviceDefinition`, creates `LineFlowPort` instances
- **`LineFlowPort`** — extends `PortModel`, holds `PortDefinition` with name/type/direction
- **`ElbowLinkModel`** — extends `LinkModel`; routing is driven by its `Vertices` collection (one or more draggable bend points). `MidX` is kept only as a legacy fallback for old saves with no vertices. `Color`/`SelectedColor` are set automatically from the source port's signal type via `ColorForType()`.
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

### Backward Compatibility
Files saved before multi-bend support only stored a single `midX` value (no `vertices` array). On open, if `vertices` is missing/empty, the loader falls back to `ElbowLinkModel.MidX`, which `ElbowRouter` still understands as a single-bend midpoint.

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

## Known Issues / Work in Progress

- The `OnLinkAdded` flow casts a new connection's `BaseLinkModel` to `LinkModel` before promoting it to an `ElbowLinkModel` — if the library changes this internal type, this will break.

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

## Features Implemented
- ✅ Drag-and-drop device nodes from side panel onto canvas
- ✅ Labeled ports on left (inputs) and right (outputs/universal) edges
- ✅ Elbow-routed connections with a draggable, vertically-centered handle on the first segment
- ✅ Multi-bend connection routing — add/remove additional bend points via right-click ("➕ Add Bend" / "➖ Remove Last Bend") to route around blocking nodes
- ✅ Connection direction rules enforced (no input→input etc.)
- ✅ Right-click context menu: delete node, delete connection, add/remove bends
- ✅ Delete key removes selected nodes/links
- ✅ New / Save (.lf) / Open (.lf) diagram files, with multi-vertex routing persisted and legacy single-`midX` files still loading correctly
- ✅ Server-side device library with add, edit, and delete devices
- ✅ Color-coded connections and port dots by signal type (HDMI, SDI, Audio, Network, USB, IR, COM)
- ✅ Legend node — click "Legend" to add a draggable canvas node showing only the signal types actually connected in the current diagram
- ✅ PDF export (white background, title + date header, direct download) — resets zoom/pan before capture so connections render correctly at any zoom level
- ✅ DXF export (AutoCAD compatible, NODES + CONNECTIONS layers), generating the same multi-segment path as the live app's routing
- ✅ Zoom and pan on canvas
- ✅ Freeform annotations — Box (resizable rectangle, no fill), Line (2-point freeform line with draggable endpoints, not attached to ports), and Text (click-to-place, editable, with font size/color controls); all three are selectable/deletable, saved in `.lf` files, and included in PDF and DXF exports (DXF `ANNOTATIONS` layer)
- ✅ Per-user authentication — cookie-based login gating the entire app, first-run admin setup, Admin/User roles, in-app "Manage Users" page, account menu with Logout (see Authentication & User Management above)
- ✅ File authorship tracking — `.lf` files record who created and who last modified them, and when, shown in an info bar under the toolbar

## Features Planned / Not Yet Implemented
- ⬜ Connection labels (e.g. "VID-005" like reference diagram)
- ⬜ MAUI desktop wrapper
- ⬜ Cloudflare Tunnel setup for public internet access from a home-hosted server (auth system above was built specifically to make this safe)
- ⬜ Ubuntu deployment tested end-to-end with the new auth system
