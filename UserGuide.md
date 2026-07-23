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
9. [Saving and Opening Diagrams](#9-saving-and-opening-diagrams)
10. [Exporting](#10-exporting)
11. [Managing Users (Admins)](#11-managing-users-admins)
12. [The Desktop App](#12-the-desktop-app)
13. [Tips & Troubleshooting](#13-tips--troubleshooting)

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
Any signed-in user can change their own password: click the **👤 account button → 🔑 Change Password**, enter your current password and the new one (at least 8 characters) twice, and click **Update Password**. If you've forgotten your password entirely, an administrator can reset it for you (see [Managing Users](#11-managing-users-admins)).

> **Don't have an account?** Accounts are created by an administrator — there is no self-signup. Ask your admin to add you (see [Managing Users](#11-managing-users-admins)).

---

## 2. The Interface at a Glance

Once signed in, you'll see three main areas:

- **Toolbar (top)** — the app menus and controls:
  - **File** → 🆕 New, 💾 Save, 📂 Open
  - **Tools** → 🏷 Legend, ▭ Box, ╱ Line, T Text
  - **Export** → 📄 Export PDF, 📐 Export DXF
  - **↩ Undo / ↪ Redo** — step backward/forward through your changes (also **Ctrl+Z** / **Ctrl+Y** when the canvas has focus). Covers everything: placing and deleting devices, connections, bends, labels, moves, resizes, and text edits. Up to 50 steps.
  - **Delete Selected** — removes whatever is currently selected on the canvas
  - **Diagram title box** — the name used in the PDF export header
  - **👤 account menu** — your username, Manage Users (admins only), and Logout
- **Devices panel (left)** — the shared device library, grouped by category, with a sort control and an "+ Add Device" button at the bottom.
- **Canvas (everything else)** — where the diagram lives. Scroll to zoom, drag empty space to pan.

When a diagram that has been saved before is open, a thin info bar under the toolbar shows **who created the file and when, and who last modified it** — e.g. *"Created by jsmith on Jul 6, 2026 3:12 PM · Last modified by mjones on Jul 8, 2026 9:41 AM."*

---

## 3. Working with Devices

### The device library
The left panel lists every device your team has defined. The library is **shared and server-side** — when anyone adds or edits a device, everyone sees it. Devices are grouped under category headers; use the **"Sort by"** dropdown to group by **Type** (category) or **Manufacturer** instead.

### Placing a device on the canvas
Two ways — both give the same result:

1. **Drag and drop** — drag a device card from the panel onto the canvas.
2. **Click-to-place** — click a device card once (it highlights coral to show it's armed), then click anywhere on the canvas to place it there. Click the highlighted card again to cancel without placing.

> If you're using the **desktop app**, use click-to-place — drag-and-drop doesn't work there due to a Windows platform bug (see [The Desktop App](#12-the-desktop-app)).

A placed device appears as a node with its **input ports on the left edge** and its **output/universal ports on the right edge**, each with a colored dot showing its signal type.

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

### Editing or deleting a device
Each device card has **✏ (edit)** and **🗑 (delete)** buttons. Editing opens the same dialog pre-filled. Deleting removes it from the library — devices already placed on diagrams are not affected.

### Copying and duplicating on the canvas
Once devices (or annotations) are on the canvas, you can duplicate them instead of dragging fresh ones out:

- **Right-click a device → ⧉ Duplicate** makes an instant copy right next to it.
- **Ctrl+C** copies whatever is selected, then **Ctrl+V** pastes a copy (paste again for more — each lands slightly offset so they don't pile up on top of each other). The pasted copies come in already selected, so you can drag them into place immediately.
- To copy several items at once, select multiple first: click one, then hold **Ctrl** and click others (or drag a selection box around them on empty canvas), then Ctrl+C / Ctrl+V.
- **Connections come along** — if you copy two devices that are wired together, the connection between them (and its label) is copied too. This makes it quick to duplicate a whole sub-system (e.g. a repeated rack layout).

> Copies are new independent objects — editing a copy doesn't affect the original. This clipboard only works within this browser tab.

---

## 4. Making Connections

To connect two devices, **click and drag from one port dot to another**. The line draws as you drag; release on the target port.

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

### Color coding
Connections and port dots are colored automatically by signal type:

| Type | Color |
|---|---|
| HDMI | Light blue |
| SDI | Green |
| Audio | Orange |
| Network | Purple |
| USB | Pink |
| IR | Teal |
| COM | Yellow |
| Other | Coral red |

### Deleting a connection
Right-click the connection → **🗑 Delete Connection**, or click it to select it and press the **Delete** key (or the **Delete Selected** toolbar button).

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
- **Edit it** — right-click the connection again → **🏷 Edit Label** (the field comes pre-filled).
- **Remove it** — the edit dialog has a **🗑 Remove Label** button, or just select the label tag on the canvas and delete it like any other object.

Labels are included in both PDF and DXF exports.

---

## 7. Annotations: Boxes, Lines, and Text

The **Tools** menu has three freeform drawing tools for marking up the diagram. Each works the same way: pick the tool (the canvas cursor changes to a crosshair), then **click the canvas** to place it. Picking the same tool again cancels.

- **▭ Box** — an empty rectangle, useful for grouping related equipment visually (e.g. drawing around everything in one rack). Select it to get corner handles for resizing. Drag its edge to move it.
- **╱ Line** — a free 2-point line not attached to any port. Select it to get endpoint handles you can drag anywhere.
- **T Text** — a text label. It starts in edit mode — just type. Press **Enter** or click away when done. **Double-click** any text annotation later to edit it again; while it's selected, a small style bar appears with **font size** and **color** controls.

All annotations can be selected, moved, and deleted like anything else, are saved in the diagram file, and appear in PDF and DXF exports.

---

## 8. The Legend

**Tools → 🏷 Legend** places a color-key node on the canvas showing which signal-type colors mean what — and it only lists the types **actually used in the current diagram's connections**, so it stays relevant. Click Legend again after adding more connection types to refresh it. It's a normal node: drag it wherever it looks best.

---

## 9. Saving and Opening Diagrams

Diagrams save as **`.lf` files that download to your computer** — they are *not* stored on the server. Treat them like any other document: keep them in your project folders, email them, put them on the network share, etc.

- **File → 💾 Save** downloads the current diagram as `diagram.lf`. Rename the file however you like.
- **File → 📂 Open** loads a `.lf` file from your computer. **This replaces whatever is currently on the canvas**, so save first if you have unsaved work.
- **File → 🆕 New** clears the canvas for a fresh start.

Every save stamps the file with your username and the current time — the first save records you as the **creator**, and every later save (by you or anyone else) updates the **last modified by** info shown in the bar under the toolbar.

### Unsaved changes
Whenever you have changes that haven't been saved to a `.lf` file, an amber **● Unsaved** badge appears in the toolbar. If you try to close or reload the browser tab while it's showing, your browser will warn you ("Leave site? Changes you made may not be saved") so you get a chance to save first. Saving the diagram clears the badge.

> ⚠️ Loopback has no autosave and no server-side storage of diagrams. The unsaved-changes warning helps, but if you dismiss it and close the tab, unsaved changes are gone.

---

## 10. Exporting

All exports are in the **Export** menu and download directly, like Save does.

### 📄 PDF
Captures the diagram onto a landscape A4 page with a header showing the **diagram title** (from the toolbar text box) and today's date. It automatically zooms out to fit your **entire** diagram in the export — even if parts are spread far off-screen — so nothing gets cut off, then puts your view back exactly where it was. (Very large diagrams naturally come out at a smaller scale so everything fits on the page.)

### 📐 DXF
Generates an AutoCAD-compatible DXF with content organized on layers:

- **NODES** — device boxes, titles, and port names
- **CONNECTIONS** — every connection's full multi-bend path, plus connection labels
- **ANNOTATIONS** — boxes, lines, and text you added

Useful for dropping the diagram into CAD workflows or as a starting point for formal drawings.

### 🧾 Cable Schedule (CSV)
Generates a **cable schedule / pull sheet** — a spreadsheet listing every connection in the diagram, one per row, with these columns:

| # | Cable | Signal | From Device | From Port | To Device | To Port |
|---|-------|--------|-------------|-----------|-----------|---------|

- **Cable** is the connection's label (e.g. VID-001) — blank if you haven't labelled it.
- **Signal** is the connection's signal type (HDMI, Audio, etc.).
- Rows are grouped/sorted by the source device, so all the cables coming off one box are listed together.

It downloads as `cable-schedule.csv` and opens directly in Excel or Google Sheets — handy for handing a wiring list to installers. Tip: label your connections (right-click → Add Label) before exporting so each cable has an ID in the schedule.

---

## 11. Managing Users (Admins)

Only accounts with the **Admin** role see this. Open the **👤 account menu → 👥 Manage Users**.

- **Add a user**: enter a username, a password (min 8 characters), pick a role, and click **Add User**. Give them the credentials — they can sign in immediately. There's no email verification; you set their password for them.
- **Reset a password**: click **Reset Password** next to a user, type a new password (min 8 characters), and click **Set Password**. Use this when someone forgets their password — you don't need to know their old one. Tell them the new password; they can change it themselves afterward.
- **Roles**:
  - **User** — full access to the diagram tool and device library.
  - **Admin** — everything a User can do, plus this Manage Users page.
- **Remove a user**: click **Remove** next to their name. Their access ends the moment their login session expires or they log out — this is how you revoke access when someone leaves.
- **Safety rails**: you can't remove your own account while logged into it, and you can't remove the last remaining Admin (so you can never lock everyone out).

---

## 12. The Desktop App

Loopback also ships as a native **Windows desktop app** — the same interface in its own window, no browser needed.

### First launch
A **Settings** window opens automatically asking for the server address. Enter the full URL including `http://` and the port — for example:

```
http://192.168.1.200:5052
```

Click **Save & Connect**. The address is remembered permanently on that computer.

### The top bar
- **Settings** — reopens the server-address window anytime (e.g. if the server moves or you want to point at a different one).
- **Reload** — reloads the app from the server; handy if the server restarted or the connection hiccuped.

### Known limitation: drag-and-drop
Dragging devices from the panel onto the canvas **does not work in the desktop app** — this is a bug in the Windows component the app is built on (Microsoft is aware), not something Loopback can fix directly. **Use click-to-place instead**: click the device card, then click the canvas. Everything else works identically to the browser.

---

## 13. Tips & Troubleshooting

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
