# MUD Game — PRD

## Problem statement
> I want a pure C# MUD built for visual studio to run. A GUI based app that can be
> edited from the client and multiplayer.

## User choices
- Project type: Console App with GUI window (minimal GUI wrapper)
- Networking: Dedicated server executable + separate client
- "Edited from the client": In-game world editor (admin creates/edits rooms, items,
  NPCs live from the GUI)
- Seeded content: Empty world — build it yourself
- Runtime: Local Windows machine via Visual Studio (.NET 8)

## Architecture
- Visual Studio solution `MudGame.sln` with three projects:
  - `MudShared` — world models + JSON-over-TCP protocol (`net8.0`)
  - `MudServer` — TCP server + minimal WinForms status window (`net8.0-windows`)
  - `MudClient` — WinForms console-style player view + in-game world editor
    (`net8.0-windows`)
- Transport: newline-delimited UTF-8 JSON over TCP, port `4000`.
- Persistence: `world.json` beside the server exe, saved on every mutation.
- Admin: the first registered account is automatically granted admin rights.

## What's implemented (2026-01)
- Dedicated TCP server with GUI showing live log + session list + stats.
- Multi-user login/registration with SHA-256 password hashing.
- Core MUD gameplay: look, movement (n/s/e/w/u/d/go), say, shout, who, inv,
  take, drop, help, quit; command history (Up/Down).
- Live multiplayer: enter/exit notifications per room, chat broadcast.
- In-game world editor (admin only) with three tabs:
  - Rooms: name, description, exits grid (direction → target room), items/NPCs
    checklist for placement, set-as-start button, delete.
  - Items: name, description, takeable toggle, delete.
  - NPCs: name, description, greeting, delete.
- Automatic world refresh after every editor action.
- Clean build (`dotnet build MudGame.sln`) with 0 warnings / 0 errors.
- JSON protocol validated with a round-trip serialization test.

## Backlog (P1)
- NPC behaviours (greet on arrival, wander, dialog trees).
- Combat system (stats, HP, attack/defend).
- Zones / room grouping and macro import/export.
- Teleport-to-room admin command.
- Visual map / minimap panel.
- Colour profiles / theme selection in client.
- Multi-admin promotion UI (currently only first-registered is admin).

## Backlog (P2)
- Persistence to SQLite instead of flat JSON.
- Scripting hooks (Lua / C# scripting) for NPCs and items.
- Account recovery, admin audit log.
