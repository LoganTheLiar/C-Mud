# MudGame

A pure C# multi-user dungeon (MUD) built as a Visual Studio solution targeting .NET 8.
Includes a dedicated server, a GUI client with console-style play and a built-in world
editor, and a shared library for the networking protocol and world models.

## Projects

| Project       | Type                  | Target           | Purpose                                                  |
| ------------- | --------------------- | ---------------- | -------------------------------------------------------- |
| `MudShared`   | Class library         | `net8.0`         | World models, JSON-over-TCP protocol                      |
| `MudServer`   | Console + minimal GUI | `net8.0-windows` | Dedicated TCP server (WinForms status window)             |
| `MudClient`   | WinForms GUI          | `net8.0-windows` | Console-style player view + in-game world editor         |

## Requirements

- Visual Studio 2022 (17.8+) on Windows, or `dotnet` SDK 8.x
- Windows Desktop workload (`.NET desktop development`)

## Running

### Option A — From Visual Studio
1. Open `MudGame.sln`.
2. Right-click the solution → **Set Startup Projects…** → **Multiple startup projects** →
   set both `MudServer` and `MudClient` to **Start**.
3. Press **F5**. The server window appears (listening on port `4000`) and one client
   window appears next to it. Launch additional client processes to simulate multiple
   players.

### Option B — From the command line (Windows)
```bash
dotnet build MudGame.sln
dotnet run --project MudServer
# in another terminal:
dotnet run --project MudClient
```

## First login — becoming admin

- On a fresh install the world file (`world.json` beside `MudServer.exe`) is empty with a
  single room called **The Void**.
- The **first account registered on the server automatically becomes an admin**. Use the
  client's connect dialog, pick **Register new**, choose a name and password.
- Admins see the **World Editor** tab enabled. All edits are sent to the server and
  persisted to `world.json` immediately.

## Gameplay commands

Typed into the input bar at the bottom of the client:

| Command                | What it does                                       |
| ---------------------- | -------------------------------------------------- |
| `look` / `l`           | Describe the current room                          |
| `n/s/e/w/u/d`          | Move in a direction                                |
| `go <dir>`             | Move (named direction)                             |
| `say <msg>`            | Say something to people in the current room        |
| `shout <msg>`          | Broadcast to the whole MUD                         |
| `who`                  | List online players                                |
| `inv` / `i`            | Show inventory                                     |
| `take <item>` / `get`  | Pick up an item in the room                        |
| `drop <item>`          | Drop an item from inventory                        |
| `help`                 | Help                                               |
| `quit`                 | Disconnect                                         |

Command history is available with the Up/Down arrow keys.

## World Editor (admin only)

Three tabs: **Rooms**, **Items**, **NPCs**.

- **Rooms** tab: name, description, exits (direction → target room), check-list of items
  and NPCs currently placed in the room, and a **Set as Start** button to mark the room
  new players spawn into.
- **Items** tab: name, description, takeable flag.
- **NPCs** tab: name, description, greeting line.

Each save sends an `upsert` message to the server, which persists `world.json` and
broadcasts a fresh world snapshot back to the editor.

## Networking

- Transport: TCP on port **4000** (change in `MudServer/Program.cs`).
- Framing: newline-delimited UTF-8 JSON.
- Message shape: `{ "type": "<kind>", "payload": { ... } }`. See
  `MudShared/Protocol/Message.cs` for the full list of message types.

## File layout

```
MudGame/
├── MudGame.sln
├── MudShared/
│   ├── Models/           (Room, Item, Npc, Player, World)
│   └── Protocol/         (NetMessage + MessageType constants)
├── MudServer/
│   ├── Program.cs        (startup, launches GUI + TCP listener)
│   ├── GameServer.cs     (TCP accept loop, world persistence)
│   ├── ClientSession.cs  (per-connection state, command + editor dispatch)
│   └── ServerForm.cs     (minimal WinForms status window)
└── MudClient/
    ├── Program.cs
    ├── MainForm.cs       (console-style output + input, message routing)
    ├── ConnectDialog.cs  (host/port/name/password/register)
    ├── NetworkClient.cs  (TCP connection + read loop)
    └── EditorPanels/
        └── WorldEditorPanel.cs
```

The server stores its world in `world.json` next to the executable; delete it to reset.
