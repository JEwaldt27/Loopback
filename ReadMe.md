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
      "midX": 450.5,
      "vertices": [{ "x": 450.5, "y": 220.0 }]
    }
  ]
}
```

### Device Library
- Stored server-side in `Server/devices.json`
- Loaded via `GET /api/devices`
- New devices added via `POST /api/devices`
- Default device: Crestron DM-NVX-350 with 8 ports

### Custom Classes (all defined inside Home.razor @code block)
- **`LineFlowNode`** — extends `NodeModel`, holds `DeviceDefinition`, creates `LineFlowPort` instances
- **`LineFlowPort`** — extends `PortModel`, holds `PortDefinition` with name/type/direction
- **`ElbowLinkModel`** — extends `LinkModel`, stores `MidX` for the draggable vertical segment
- **`ElbowRouter`** — extends `Router`, generates 3-point elbow paths (horizontal → vertical → horizontal)
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
Lines always make 90 degree bends:
- Source port → horizontal to MidX → vertical → horizontal to target port
- `ElbowRouter.GetRoute()` generates the midpoints
- A draggable `LinkVertexModel` is added at the midpoint so users can drag the vertical segment
- `MidX` is saved in the `.lf` file and restored on open

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

### 🔴 ACTIVELY BEING WORKED ON: Draggable Elbow Midpoint (NOT working yet)
The vertical segment of elbow connections should be draggable left/right so users can avoid overlapping lines. Here is the current state and what has been tried:

**How it's supposed to work:**
- `ElbowLinkModel` has a `MidX` property storing the X position of the vertical segment
- A `LinkVertexModel` is added to the link at the midpoint so the library renders a draggable dot
- When the user drags the dot, `Changed` event fires, updates `MidX`, and `ElbowRouter` uses the new `MidX` on next render
- `MidX` is saved in the `.lf` file and restored on open

**Current problem:**
- The vertex dot either doesn't appear, appears at wrong position, or appears but isn't draggable
- The vertex is added in `OnLinkAdded` inside the `TargetChanged` event handler
- Port positions (`srcPort.Position`) may not be calculated yet when `TargetChanged` fires, causing the vertex to be placed at (0,0)

**What has been tried:**
- Adding vertex immediately in `OnLinkAdded` — positions are null at that point
- Adding vertex in `TargetChanged` — sometimes works but position is wrong
- Using `_diagram.GetRelativePoint()` to convert screen coords — didn't match canvas coords
- Manually calculating from node position + port index — close but off by a few pixels
- Using `srcPort.Position` directly — these appear to be screen coordinates not canvas coordinates
- Subtracting toolbar height (48px) and side panel width (200px) — partial fix but still wrong

**Debugging notes:**
- Node `Position` (X,Y) IS in canvas coordinates — confirmed working for save/load
- Port `Position` (X,Y) appears to be in SCREEN coordinates (includes toolbar/panel offsets)
- `_diagram.Pan` is (0,0) and `_diagram.Zoom` is 1.0 at default state
- `ElbowRouter.GetRoute()` receives port positions that include screen offsets
- The `Changed` event on `ElbowLinkModel` fires correctly when vertex is dragged

**Suggested next approach:**
- Try adding the vertex AFTER a short delay using `Task.Delay` so port positions are calculated
- Or listen to `_diagram.Changed` instead and add vertices there
- Or explore if `PortModel.Position` can be converted to canvas coords via `_diagram.GetRelativeMousePoint`

- DXF export connection lines are slightly misaligned with port positions (port Y calculation uses estimated row heights rather than actual rendered positions)
- The `OnLinkAdded` casts `BaseLinkModel` to `LinkModel` — if the library changes this will break

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
- ✅ Elbow-routed connections with draggable midpoint
- ✅ Connection direction rules enforced (no input→input etc.)
- ✅ Right-click context menu: delete node, delete connection
- ✅ Delete key removes selected nodes/links
- ✅ New / Save (.lf) / Open (.lf) diagram files
- ✅ Server-side device library with add custom device
- ✅ PDF export (white background, title + date header)
- ✅ DXF export (AutoCAD compatible, NODES + CONNECTIONS layers)
- ✅ Zoom and pan on canvas

## Features Planned / Not Yet Implemented
- ⬜ Connection labels (e.g. "VID-005" like reference diagram)
- ⬜ Color coding per port/connection type
- ⬜ Rename nodes on canvas
- ⬜ Delete devices from side panel
- ⬜ DXF connection lines perfectly aligned with port positions
- ⬜ MAUI desktop wrapper
- ⬜ Ubuntu deployment tested end-to-end
