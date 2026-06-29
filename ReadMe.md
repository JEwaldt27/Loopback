# LineFlow

## What This Project Is
LineFlow is a web-based AV/IT signal flow diagram application built with Blazor WebAssembly (.NET 10) and ASP.NET Core. It allows users to create, save, and open line flow diagrams (like those used in AV system design) with drag-and-drop devices, labeled ports, and smart elbow-routed connections.

## Project Structure
```
LineFlowAppHosted/
├── Client/                         ← Blazor WASM app (runs in browser)
│   ├── Pages/
│   │   ├── Home.razor              ← Main diagram page (ALL core logic lives here)
│   │   └── LineFlowNodeWidget.razor ← Custom node renderer with port labels
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
    │   └── DevicesController.cs    ← GET/POST /api/devices
    ├── devices.json                ← Device library (auto-created if missing)
    └── Program.cs                  ← Server setup, serves Blazor WASM
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

### Device Library
- Stored server-side in `Server/devices.json`
- Loaded via `GET /api/devices`
- New devices added via `POST /api/devices`
- Default device: Crestron DM-NVX-350 with 8 ports

### Custom Classes (all defined inside Home.razor @code block)
- **`LineFlowNode`** — extends `NodeModel`, holds `DeviceDefinition`, creates `LineFlowPort` instances
- **`LineFlowPort`** — extends `PortModel`, holds `PortDefinition` with name/type/direction
- **`ElbowLinkModel`** — extends `LinkModel`; routing is driven by its `Vertices` collection (one or more draggable bend points). `MidX` is kept only as a legacy fallback for old saves with no vertices.
- **`ElbowRouter`** — extends `Router`, generates an orthogonal H-V-H-...-H path through all of the link's vertices, in order
- **`DeviceDefinition`** — manufacturer, model, category, list of ports
- **`PortDefinition`** — name, type (HDMI/SDI/Audio/Network/USB/IR/COM/Other), direction (In/Out/Universal)

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

- DXF export now converts each vertex from screen/port coordinate space into DXF drawing space (anchored off the source port position, scaled, Y-flipped) and draws the same multi-segment path the live app renders. This has not yet been visually confirmed pixel-perfect against the app for connections with multiple bends — worth a side-by-side check after adding a few bends and exporting.
- The `OnLinkAdded` flow casts a new connection's `BaseLinkModel` to `LinkModel` before promoting it to an `ElbowLinkModel` — if the library changes this internal type, this will break.

## NuGet Packages
```xml
<!-- Client/Client.csproj -->
<PackageReference Include="Z.Blazor.Diagrams" Version="3.0.4.1" />

<!-- Server/Server.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="..." />
```

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
# On Windows dev machine — publish self-contained build
dotnet publish Server -c Release -o ./publish

# Copy to server
scp -r ./publish user@your-server-ip:/home/user/lineflowapp

# On Ubuntu server
cd /home/user/lineflowapp
dotnet LineFlowApp.Server.dll

# Or as a systemd service:
sudo nano /etc/systemd/system/lineflow.service
```

Systemd service file:
```ini
[Unit]
Description=LineFlow App
After=network.target

[Service]
WorkingDirectory=/home/user/lineflowapp
ExecStart=/usr/bin/dotnet Server.dll
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable lineflow
sudo systemctl start lineflow
```

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
- ✅ Server-side device library with add custom device
- ✅ PDF export (white background, title + date header)
- ✅ DXF export (AutoCAD compatible, NODES + CONNECTIONS layers), now generating the same multi-segment path as the live app's routing instead of a single hardcoded midpoint
- ✅ Zoom and pan on canvas

## Features Planned / Not Yet Implemented
- ⬜ Connection labels (e.g. "VID-005" like reference diagram)
- ⬜ Color coding per port/connection type
- ⬜ Rename nodes on canvas
- ⬜ Delete devices from side panel
- ⬜ Visual confirmation that DXF export lines match the app pixel-for-pixel on multi-bend connections
- ⬜ MAUI desktop wrapper
- ⬜ Ubuntu deployment tested end-to-end
