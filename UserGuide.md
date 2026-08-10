# Loopback User Guide

Loopback is a web-based tool for drawing AV/IT signal-flow diagrams — the kind used to document how devices in a rack or room are wired together. You drag devices onto a canvas, connect their ports, label the cables, and export the result as a PDF or an AutoCAD-compatible DXF file.

This guide covers everything you need as a user. (If you're looking for install/deployment instructions, see the [README](ReadMe.md) instead.)

---

## Table of Contents

1. [Signing In](#1-signing-in)
2. [The Interface at a Glance](#2-the-interface-at-a-glance)
3. [Working with Devices](#3-working-with-devices)
4. [Making Connections](#4-making-connections)
5. [Routing Connections Around Things](#5-routing-connections-around-things)
6. [Connection Labels](#6-connection-labels)
7. [Annotations: Boxes, Lines, and Text](#7-annotations-boxes-lines-and-text)
8. [The Legend](#8-the-legend)
9. [Sheets (Multi-Page Diagrams)](#9-sheets-multi-page-diagrams)
10. [Saving and Opening Diagrams](#10-saving-and-opening-diagrams)
11. [Exporting](#11-exporting)
12. [Managing Users (Admins)](#12-managing-users-admins)
13. [The Desktop App](#13-the-desktop-app)
14. [Tips & Troubleshooting](#14-tips--troubleshooting)

---

## 1. Signing In

Loopback sits entirely behind a login — nothing loads until you sign in.

### First run
The very first time anyone visits a fresh Loopback server, the login page switches to a **"Create admin account"** screen. Whoever creates this first account becomes the administrator. Pick a username and a password (**minimum 8 characters**), confirm it, and you're in.

### Every time after that
Enter your username and password on the sign-in screen. Your session lasts 30 days of activity — you won't be asked to log in again on that browser unless you log out or go idle past that.

### Logging out
Click the **👤 account button** at the top-right of the toolbar, then **🚪 Logout**.

### Changing your password
Any signed-in user can change their own password: click the **👤 account button → 🔑 Change Password**, enter your current password and the new one (at least 8 characters) twice, and click **Update Password**. If you've forgotten your password entirely, an administrator can reset it for you (see [Managing Users](#12-managing-users-admins)).

> **Don't have an account?** Accounts are created by an administrator — there is no self-signup. Ask your admin to add you (see [Managing Users](#12-managing-users-admins)).

---

## 2. The Interface at a Glance

Once signed in, you'll see four main areas:

- **Toolbar (top)** — the app menus and controls (a small version number next to the "Loopback" title shows which build the server is running):
  - **File** → 🆕 New, 💾 Save, 📂 Open
  - **Tools** → 🏷 Legend, ▭ Box, ╱ Line, T Text, 🎨 Device Colors, 🔢 Auto-Label Connections, 🔗 Break All / Rejoin All Connections, ⇤⇥⤒⤓ Align, ↔↕ Distribute
  - **Export** → 📄 Export PDF, 📐 Export DXF, 🧾 Cable Schedule (CSV), 📦 Device Schedule (CSV)
  - **Help** → 📖 User Guide (opens this guide on GitHub in a new tab)
  - **↩ Undo / ↪ Redo** — step backward/forward through your changes (also **Ctrl+Z** / **Ctrl+Y** when the canvas has focus). Covers everything: placing and deleting devices, connections, bends, labels, moves, resizes, and text edits. Up to 50 steps.
  - **Delete Selected** — removes whatever is currently selected on the canvas
  - **Diagram title box** — the name used in the PDF export header
  - **👤 account menu** — your username, Manage Users (admins only), and Logout
- **Devices panel (left)** — the shared device library, grouped by category, with a sort control and an "+ Add Device" button at the bottom.
- **Cable Types panel (right)** — define the cabling types used in this diagram (color, prefix, part number, cable color); connections are colored and labeled from these (see Cable types under Making Connections).
- **Canvas (middle)** — where the diagram lives. Scroll to zoom, drag empty space to pan. **Lost your diagram after zooming too far?** Double-click the middle mouse button (press the scroll wheel twice quickly) anywhere on the canvas to snap the whole diagram back into view. Along the bottom sits the **sheet tab bar** — multi-page diagrams, Excel-style (see [Sheets](#9-sheets-multi-page-diagrams)).

When a diagram that has been saved before is open, a thin info bar under the toolbar shows **who created the file and when, and who last modified it** — e.g. *"Created by jsmith on Jul 6, 2026 3:12 PM · Last modified by mjones on Jul 8, 2026 9:41 AM."*

---

## 3. Working with Devices

### The device library
The left panel lists every device your team has defined. The library is **shared and server-side** — when anyone adds or edits a device, everyone sees it. Devices are grouped under category headers; use the **"Sort by"** dropdown to group by **Type** (category) or **Manufacturer** instead. As the library grows, the **🔍 search box** at the top filters as you type, matching manufacturer, model, or category — click **✕** to clear.

### Placing a device on the canvas
Two ways — both give the same result:

1. **Drag and drop** — drag a device card from the panel onto the canvas.
2. **Click-to-place** — click a device card once (it highlights coral to show it's armed), then click anywhere on the canvas to place it there. Click the highlighted card again to cancel without placing.

> If you're using the **desktop app**, use click-to-place — drag-and-drop doesn't work there due to a Windows platform bug (see [The Desktop App](#13-the-desktop-app)).

A placed device appears as a node with its **input ports on the left edge** and its **output/universal ports on the right edge**, each with a black port dot. (Connection color now comes from the cable type you assign — see Cable types below.)

**Change a block's colors:** click a device block to select it — a small bar appears above it with three controls: **Fill** (the block color, plus a checkerboard button to make it **transparent**, handy for layering blocks over a colored box), **Line** (the outline color), and **Text** (the title and port-label color). All are saved with your diagram and show up in PDF exports.

Colors update **live** as you drag in the picker — no need to click to confirm — and each pick is a single Undo step.

**Recolor every device at once:** use **Tools → 🎨 Device Colors** to open Fill / Line / Text pickers (with the same transparent option) that apply to *all* device blocks on the canvas, live as you drag. A single Undo reverts a bulk change across every block.

### Adding a new device to the library
Click **"+ Add Device"** at the bottom of the panel. A dialog opens:

- **Manufacturer** — e.g. "Crestron"
- **Model number** — e.g. "DM-NVX-350" (required)
- **Category** — the group it appears under, e.g. "AV over IP" (categories are matched case-insensitively, so "Displays" and "DISPLAYS" merge into one group)
- **Ports** — click **"+ Add Port"** for each connector on the device, and give each one:
  - a **name** (what shows next to the dot, e.g. "HDMI Out")
  - a **type** — HDMI, SDI, Audio, Network, USB, IR, COM, or Other (controls the color)
  - a **direction** — **In** (left side), **Out** (right side), or **Universal** (right side, connects to anything)

Click **"+ Add Device"** in the dialog to save it to the shared library.

### Duplicating, editing, or deleting a device
Each device card has three buttons: **⧉ (duplicate)**, **✏ (edit)**, and **🗑 (delete)**.
- **Duplicate** opens the Add-Device dialog pre-filled with a copy of that device — same manufacturer, category, and ports, with "Copy" added to the model name. Great when a new device is just one port different from an existing one: duplicate it, tweak the model name and that one port, and save.
- **Edit** opens the same dialog on the existing device.
- **Delete** removes it from the library — devices already placed on diagrams are not affected.

### Copying and duplicating on the canvas
Once devices (or annotations) are on the canvas, you can duplicate them instead of dragging fresh ones out:

- **Right-click a device → ⧉ Duplicate** makes an instant copy right next to it.
- **Ctrl+C** copies whatever is selected, then **Ctrl+V** pastes a copy (paste again for more — each lands slightly offset so they don't pile up on top of each other). The pasted copies come in already selected, so you can drag them into place immediately.
- To copy several items at once, select multiple first: click one, then hold **Ctrl** and click others (or drag a selection box around them on empty canvas), then Ctrl+C / Ctrl+V.
- **Connections come along** — if you copy two devices that are wired together, the connection between them (and its label) is copied too. This makes it quick to duplicate a whole sub-system (e.g. a repeated rack layout).

> Copies are new independent objects — editing a copy doesn't affect the original. This clipboard only works within this browser tab.

### Lining things up
Hand-aligned blocks are most of what makes a drawing look messy — two tools fix that:

- **Align / distribute** — select 2+ blocks, then **Tools → ⇤ Align Left / ⇥ Align Right / ⤒ Align Top / ⤓ Align Bottom**. With 3+ selected, **↔ Distribute Horizontally / ↕ Distribute Vertically** spreads the middle blocks so everything is evenly spaced (the outermost two stay put). Works on devices, annotations — anything selectable. Each action is one undo step.
- **Keyboard nudge** — with blocks selected and the canvas focused, the **arrow keys** move them 1px per press; hold **Shift** for 10px. A quick burst of presses counts as a single undo step.

---

## 4. Making Connections

To connect two devices, **click and drag from one port dot to another** (the dots are black). The line draws as you drag; release on the target port. When the connection lands, a **Choose Cable Type** prompt appears — pick which cabling type it is, and the connection takes that type's color. (If you haven't defined any cable types yet, the connection stays a neutral gray until you assign one.)

### Connection rules
Loopback enforces signal direction, so you can't wire two outputs together by accident:

| From | To | Allowed? |
|---|---|---|
| Output | Input | ✅ |
| Input | Output | ✅ |
| Output or Input | Universal | ✅ |
| Universal | Universal | ✅ |
| Input | Input | ❌ blocked |
| Output | Output | ❌ blocked |

If a connection isn't allowed, it simply won't attach.

### Cable types (connection colors)
You define your own **cable types** and their colors in the **Cable Types** panel on the right side of the screen:

- Type a name (e.g. "HDMI", "Cat6", "XLR"), pick an on-screen color, and optionally fill in a **label prefix** (e.g. `VID`), a **part number** (e.g. `Belden 1694A`), and the physical **cable color** (e.g. `Blue`), then click **+ Add**. You can edit any of these later in the panel.
- Each connection is colored by the cable type you assign to it. Change a type's color and every connection using it re-colors instantly.
- The **part number** and **cable color** show up in the legend (next to the type name) and in the Cable Schedule CSV — handy for handing a precise materials list to installers.
- To change a connection's type later, **right-click it → 🔌 Cable Type…** and pick a different one.
- Delete a type with the 🗑 button; any connections that used it revert to neutral gray.

**Auto-label every connection:** once your cable types have prefixes, **Tools → 🔢 Auto-Label Connections** labels every connection automatically — `PREFIX-1`, `PREFIX-2`, … per cable type — numbered in the same order as the Cable Schedule (by source device). Run it again anytime to renumber. The labels are ordinary labels, so you can still right-click a connection to edit its label by hand afterward.

Cable types are saved inside each diagram's `.lf` file, so they travel with the drawing. (Port dots are always black — the color now lives on the cables, not the ports.)

### Deleting a connection
Right-click the connection → **🗑 Delete Connection**, or click it to select it and press the **Delete** key (or the **Delete Selected** toolbar button).

### Tracing a signal path
On a busy drawing, "where does this actually go?" gets hard to answer by eye. Right-click any **connection or device** → **🔦 Trace Signal Path**: everything electrically reachable from that point lights up — traced connections draw thick in full color and traced devices get an amber glow, while the rest of the diagram fades into the background. Broken connections trace straight through (they're still one cable), lighting their label blocks along the way.

A "🔦 Tracing signal path" chip appears at the top of the canvas while a trace is live. Clear it with **Esc**, by **clicking empty canvas**, or with the chip's **✕ clear** button. Tracing is a view tool only — it changes nothing about the diagram, isn't saved, and doesn't touch your undo history.

---

## 5. Routing Connections Around Things

Connections always route with clean 90° elbow bends — no diagonal lines.

- When you first draw a connection, it gets **one bend handle** (a small white-ringed dot) centered between the two ports. Drag it **left or right** to move the vertical segment — its height stays locked automatically.
- If the line crosses through a device it shouldn't, **right-click the connection → ➕ Add Bend**. This adds another handle you can drag **freely in any direction**, letting the line route around obstacles. Add as many as you need.
- **Right-click → ➖ Remove Last Bend** removes the most recently added handle (shown only when there's more than one).

All bend positions are saved in the diagram file and restored exactly when reopened.

---

## 6. Connection Labels

Label a cable run the way real drawings do (e.g. **"VID-005"**):

1. **Right-click a connection → 🏷 Add Label**
2. Type the label text and click **✔ Save**

The label appears as a small tag centered on the connection. From there you can:

- **Drag it** anywhere — it's a free-floating tag, so if the layout gets busy you can slide it along or away from the line. It keeps its position when you save/reopen.
- **Rotate it** — right-click the label tag → **↻ Rotate Label 90°**. Each click turns it another quarter-turn clockwise (0° → 90° → 180° → 270° → back to flat), handy for labelling vertical cable runs so the text reads along the line. The rotation is saved with your diagram and shows up in PDF and DXF exports.
- **Edit it** — right-click the connection again → **🏷 Edit Label** (the field comes pre-filled).
- **Remove it** — the edit dialog has a **🗑 Remove Label** button, or just select the label tag on the canvas and delete it like any other object.

Labels are included in both PDF and DXF exports.

### Auto-labeling every connection
Instead of labeling each connection by hand, you can label them all at once from their cable types. First give each cable type a **label prefix** in the Cable Types panel (e.g. `DATA`), then choose **Tools → 🔢 Auto-Label Connections**. Every connection gets a label like `DATA-1`, `DATA-2`, … — numbered per prefix starting at 1, in the same order as the Cable Schedule (by source device). Connections with no cable type (or no prefix) are left alone. Run it again anytime to renumber. The results are ordinary labels, so you can still right-click any connection to tweak its label, and drag/rotate the tags as usual.

### Breaking connections (for readability)
On busy diagrams, a line running all the way across the page can be hard to follow. You can **break** a connection so that, instead of one long line, each end gets a small **label block** (both showing the connection's label) joined to its port by a short stub — the reader knows they're the same cable because the labels match, like signal references on a real schematic.

- **One connection:** right-click it → **🔗 Break Connection** (and **Rejoin Connection** to bring the line back). If the connection has no label yet, you'll be asked for a tag first.
- **All at once:** **Tools → 🔗 Break All Connections** auto-labels everything first, then breaks it all. **Tools → 🔗 Rejoin All Connections** puts every line back.
- **Moving the blocks:** they're ordinary diagram blocks — **drag one anywhere** and its stub line follows. New blocks appear just outside their port, stepping outward when several land on the same side of a device. Positions are saved with the diagram.
- **Getting back to the connection:** the long line is invisible while broken, so right-click a **label block or its stub** — that opens the connection's own menu (Rejoin, Edit Label, Cable Type…). Deleting a block also rejoins the connection.

Breaking is purely visual — the connection still counts as one cable everywhere (Cable Schedule, legend, direction rules, color). The broken view is saved with your diagram and shows in PDF and DXF exports.

---

## 7. Annotations: Boxes, Lines, and Text

The **Tools** menu has three freeform drawing tools for marking up the diagram. Each works the same way: pick the tool (the canvas cursor changes to a crosshair), then **click the canvas** to place it. Picking the same tool again cancels.

- **▭ Box** — an empty rectangle, useful for grouping related equipment visually (e.g. drawing around everything in one rack). Select it to get corner handles for resizing. Drag its edge to move it.
- **╱ Line** — a free 2-point line not attached to any port. Select it to get endpoint handles you can drag anywhere.
- **T Text** — a text label. It starts in edit mode — just type. Press **Enter** or click away when done. **Double-click** any text annotation later to edit it again; while it's selected, a small style bar appears with **font size** and **color** controls.

All annotations can be selected, moved, and deleted like anything else, are saved in the diagram file, and appear in PDF and DXF exports.

---

## 8. The Legend

**Tools → 🏷 Legend** places a color-key node on the canvas — a small **table** with columns for the color swatch, cable type, part number, and cable color — listing only the types **actually used in the current diagram's connections**, so it stays relevant. Click Legend again after adding more connections to refresh it. It's a normal node: drag it wherever it looks best.

---

## 9. Sheets (Multi-Page Diagrams)

Big projects rarely fit on one page. The **tab bar under the canvas** works like sheet tabs in Excel — one canvas per sheet, e.g. a sheet per room or floor:

- **＋** adds a sheet; **click** a tab to switch to it.
- **Double-click** a tab to rename it (Enter saves, Esc cancels).
- **✕** on a tab deletes that sheet — everything on it — after a confirmation. (There's always at least one sheet.)

Each sheet is its own drawing: devices, connections, annotations, legend, and even its **own undo history**. What's shared across the whole document: the **diagram title**, the **Cable Types panel** (define `HDMI` once, use it on every sheet), and the file info bar.

Handy pairings:
- **Copy/paste works across sheets** — Ctrl+C on one sheet, switch tabs, Ctrl+V on another.
- **Broken connections make natural off-page references** — give the run the same label on both sheets and break it on each; the matching label blocks tell the reader where it continues.
- The **cable and device schedules cover every sheet** (see Exporting), while **PDF and DXF export the sheet you're viewing** — export each sheet for a full drawing package (the PDF header shows the sheet name).

Everything saves together in one `.lf` file, including which sheet was active. Files saved before sheets existed open as a single "Sheet 1".

---

## 10. Saving and Opening Diagrams

Diagrams save as **`.lf` files that download to your computer** — they are *not* stored on the server. Treat them like any other document: keep them in your project folders, email them, put them on the network share, etc.

- **File → 💾 Save** downloads the current diagram (**all sheets**) as `diagram.lf`. Rename the file however you like.
- **File → 📂 Open** loads a `.lf` file from your computer. **This replaces the whole document — every sheet** — so you'll be asked to confirm if you have unsaved changes.
- **File → 🆕 New** starts a fresh single-sheet document — also confirmed first if you have unsaved changes. (Undo can't bring back a replaced document: undo history is per sheet and never spans files.)

Every save stamps the file with your username and the current time — the first save records you as the **creator**, and every later save (by you or anyone else) updates the **last modified by** info shown in the bar under the toolbar.

### Unsaved changes
Whenever you have changes that haven't been saved to a `.lf` file, an amber **● Unsaved** badge appears in the toolbar. If you try to close or reload the browser tab while it's showing, your browser will warn you ("Leave site? Changes you made may not be saved") so you get a chance to save first. Saving the diagram clears the badge.

> ⚠️ Loopback has no autosave and no server-side storage of diagrams. The unsaved-changes warning helps, but if you dismiss it and close the tab, unsaved changes are gone.

---

## 11. Exporting

All exports are in the **Export** menu and download directly, like Save does.

### 📄 PDF
Captures the **sheet you're viewing** onto a landscape A4 page with a header showing the **diagram title** (from the toolbar text box — plus the sheet name when the document has more than one sheet), today's date, and the app version. For a full drawing package, export each sheet in turn. The export always includes your **entire** diagram — every device, connection, and label, even parts spread far off-screen — no matter where you're currently panned or zoomed. Your on-screen view isn't affected at all. (Very large diagrams naturally come out at a smaller scale so everything fits on the page.)

### 📐 DXF
Generates an AutoCAD-compatible DXF with content organized on layers:

- **NODES** — device boxes, titles, and port names
- **CONNECTIONS** — every connection's full multi-bend path, plus connection labels
- **ANNOTATIONS** — boxes, lines, and text you added

Useful for dropping the diagram into CAD workflows or as a starting point for formal drawings.

### 🧾 Cable Schedule (CSV)
Generates a **cable schedule / pull sheet** — a spreadsheet listing every connection **across every sheet**, one per row, with these columns:

| # | Sheet | Cable | Cable Type | Part Number | Cable Color | Signal | From Device | From Port | To Device | To Port |
|---|-------|-------|------------|-------------|-------------|--------|-------------|-----------|-----------|---------|

- **Sheet** is which sheet (tab) the connection lives on; sheets appear in tab order.
- **Cable** is the connection's label (e.g. VID-001) — blank if you haven't labelled it.
- **Cable Type**, **Part Number**, and **Cable Color** come from the cabling type you assigned to the connection (from the Cable Types panel).
- **Signal** is the port's signal type (HDMI, Audio, etc.) — still recorded on each device port.
- Within each sheet, rows are grouped/sorted by the source device, so all the cables coming off one box are listed together.

It downloads as `cable-schedule.csv` and opens directly in Excel or Google Sheets — handy for handing a wiring list to installers. Tip: label your connections (right-click → Add Label) before exporting so each cable has an ID in the schedule.

### 📦 Device Schedule (CSV)
The other half of the paperwork: a **BOM-style device list** covering **every sheet**, one row per manufacturer + model with a quantity:

| Qty | Manufacturer | Model | Category |
|-----|--------------|-------|----------|

Rows are sorted by category, then manufacturer. Three of the same speaker on the canvas become one row with Qty 3. Downloads as `device-schedule.csv` — pair it with the cable schedule for quoting and ordering.

---

## 12. Managing Users (Admins)

Only accounts with the **Admin** role see this. Open the **👤 account menu → 👥 Manage Users**.

- **Add a user**: enter a username, a password (min 8 characters), pick a role, and click **Add User**. Give them the credentials — they can sign in immediately. There's no email verification; you set their password for them.
- **Reset a password**: click **Reset Password** next to a user, type a new password (min 8 characters), and click **Set Password**. Use this when someone forgets their password — you don't need to know their old one. Tell them the new password; they can change it themselves afterward.
- **Roles**:
  - **User** — full access to the diagram tool and device library.
  - **Admin** — everything a User can do, plus this Manage Users page.
- **Remove a user**: click **Remove** next to their name. Their access ends the moment their login session expires or they log out — this is how you revoke access when someone leaves.
- **Safety rails**: you can't remove your own account while logged into it, and you can't remove the last remaining Admin (so you can never lock everyone out).

---

## 13. The Desktop App

Loopback also ships as a native **Windows desktop app** — the same interface in its own window, no browser needed.

### First launch
A **Settings** window opens automatically asking for the server address. Enter the full URL including `http://` and the port — for example:

```
http://192.168.1.200:5052
```

Click **Save & Connect**. The address is remembered permanently on that computer.

### The control strip
The desktop app is nearly all app — the controls sit in a thin strip at the very top that **auto-hides**. Move your mouse to the top edge of the window and it reveals two buttons:
- **Settings** — reopens the server-address window anytime (e.g. if the server moves or you want to point at a different one).
- **Reload** — reloads the app from the server; handy if the server restarted or the connection hiccuped.

### Known limitation: drag-and-drop
Dragging devices from the panel onto the canvas **does not work in the desktop app** — this is a bug in the Windows component the app is built on (Microsoft is aware), not something Loopback can fix directly. **Use click-to-place instead**: click the device card, then click the canvas. Everything else works identically to the browser.

---

## 14. Tips & Troubleshooting

**"I just see a login page / 'Create admin account' screen"**
That's normal — the whole app is gated behind login. If you see the admin-creation screen, the server is brand new and no accounts exist yet.

**"My username or password isn't working"**
Ask your admin to check your account on the Manage Users page. If you've forgotten your password, the admin can reset it for you with **Reset Password** — no need to delete and re-create your account.

**"I deleted something by accident"**
Press **Ctrl+Z** (with the canvas focused) or click **↩ Undo** in the toolbar. Deleting a device also removes its connections and their labels — one undo brings all of it back together.

**"I opened a file and my old work disappeared"**
Opening a file replaces the canvas — but as of the undo feature, **Undo** brings the previous diagram right back. It's still only permanently safe if it was saved to a `.lf` file.

**"A connection won't attach"**
Check the directions — Input-to-Input and Output-to-Output are blocked by design. If both ports genuinely should connect, edit the device and set one port's direction to **Universal**.

**"The connection line cuts through a device"**
Right-click the connection and use **➕ Add Bend** to route around it.

**"Drag-and-drop doesn't work"**
If you're in the desktop app, that's the known limitation — use click-to-place. If it's not working in a regular browser either, try refreshing the page.

**"Someone added a device but I don't see it"**
Refresh the page — the device panel loads the library when the page opens.

**"The PDF looks blank or cut off"**
Make sure the diagram content isn't scrolled far off-canvas, and give the export a second — it captures the canvas as an image before building the PDF.
