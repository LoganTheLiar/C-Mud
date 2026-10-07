using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MudShared.Models;
using MudShared.Protocol;

namespace MudServer;

public class ClientSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string? PlayerName { get; private set; }
    public bool IsLoggedIn => PlayerName != null;
    public bool IsAdmin { get; private set; }
    public string CurrentRoomId { get; set; } = "";

    private readonly TcpClient _client;
    private readonly GameServer _server;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _cts = new();

    public ClientSession(TcpClient client, GameServer server)
    {
        _client = client;
        _server = server;
        var stream = client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
    }

    public async Task RunAsync()
    {
        try
        {
            SendText("Welcome to the MUD. Type 'register <name> <password>' or 'login <name> <password>'.");
            while (!_cts.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_cts.Token);
                if (line == null) break;
                NetMessage? msg;
                try { msg = NetMessage.Parse(line); }
                catch { SendText("[malformed packet]"); continue; }
                if (msg == null) continue;
                HandleMessage(msg);
            }
        }
        catch { /* client dropped */ }
        finally { Close(); }
    }

    public void Close()
    {
        try { _cts.Cancel(); } catch { }
        try { _client.Close(); } catch { }
        _server.OnSessionClosed(this);
    }

    public void Send(string type, object? payload)
    {
        try { _writer.WriteLine(NetMessage.Serialize(type, payload)); } catch { }
    }

    public void SendText(string line) => Send(MessageType.Text, new { line });

    // --------------- Message dispatch ---------------
    private void HandleMessage(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.Register: HandleRegister(msg); break;
            case MessageType.Login: HandleLogin(msg); break;
            case MessageType.Command:
                if (!IsLoggedIn) { SendText("You must log in first."); return; }
                HandleCommand(msg.GetPayload<CommandPayload>()?.Text ?? "");
                break;
            case MessageType.RequestWorld: RequireAdmin(() => Send(MessageType.WorldSnapshot, _server.World)); break;
            case MessageType.UpsertRoom: RequireAdmin(() => EditUpsertRoom(msg)); break;
            case MessageType.DeleteRoom: RequireAdmin(() => EditDeleteRoom(msg)); break;
            case MessageType.UpsertItem: RequireAdmin(() => EditUpsert<Item>(msg, w => w.Items)); break;
            case MessageType.DeleteItem: RequireAdmin(() => EditDelete(msg, w => w.Items, "Item")); break;
            case MessageType.UpsertNpc: RequireAdmin(() => EditUpsert<Npc>(msg, w => w.Npcs)); break;
            case MessageType.DeleteNpc: RequireAdmin(() => EditDelete(msg, w => w.Npcs, "Npc")); break;
            case MessageType.SetStartingRoom: RequireAdmin(() => EditSetStartingRoom(msg)); break;
            case MessageType.PlaceItemInRoom: RequireAdmin(() => EditPlaceInRoom(msg, isItem: true, add: true)); break;
            case MessageType.PlaceNpcInRoom: RequireAdmin(() => EditPlaceInRoom(msg, isItem: false, add: true)); break;
            case MessageType.RemoveItemFromRoom: RequireAdmin(() => EditPlaceInRoom(msg, isItem: true, add: false)); break;
            case MessageType.RemoveNpcFromRoom: RequireAdmin(() => EditPlaceInRoom(msg, isItem: false, add: false)); break;
        }
    }

    private void RequireAdmin(Action a)
    {
        if (!IsAdmin) { Send(MessageType.EditorAck, new { ok = false, message = "Admin only." }); return; }
        a();
    }

    // --------------- Auth ---------------
    private record AuthPayload(string Name, string Password);
    private record CommandPayload(string Text);
    private record IdPayload(string Id);
    private record PlaceInRoomPayload(string RoomId, string EntityId);

    private static string Hash(string pw)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("mud-salt::" + pw)));
    }

    private void HandleRegister(NetMessage msg)
    {
        var p = msg.GetPayload<AuthPayload>();
        if (p == null || string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Password))
        { Send(MessageType.LoginFail, new { reason = "Invalid name or password." }); return; }
        if (p.Name.Length < 2 || p.Name.Length > 20 || !p.Name.All(char.IsLetterOrDigit))
        { Send(MessageType.LoginFail, new { reason = "Name must be 2-20 alphanumeric characters." }); return; }

        bool created = false;
        _server.MutateWorld(w =>
        {
            if (w.Players.ContainsKey(p.Name)) return;
            var isFirst = w.Players.Count == 0; // first ever user becomes admin
            w.Players[p.Name] = new Player
            {
                Name = p.Name,
                PasswordHash = Hash(p.Password),
                IsAdmin = isFirst,
                CurrentRoomId = w.StartingRoomId,
                Inventory = new List<string>()
            };
            created = true;
        });
        if (!created) { Send(MessageType.LoginFail, new { reason = "Name already taken." }); return; }
        _server.Log($"Registered new player: {p.Name}");
        Login(p.Name);
    }

    private void HandleLogin(NetMessage msg)
    {
        var p = msg.GetPayload<AuthPayload>();
        if (p == null) { Send(MessageType.LoginFail, new { reason = "Invalid request." }); return; }
        Player? pl = (Player?)_server.WithWorld(w => w.Players.TryGetValue(p.Name, out var x) ? x : null);
        if (pl == null || pl.PasswordHash != Hash(p.Password))
        { Send(MessageType.LoginFail, new { reason = "Unknown name or wrong password." }); return; }
        if (_server.Sessions.Values.Any(s => s.PlayerName?.Equals(p.Name, StringComparison.OrdinalIgnoreCase) == true && s != this))
        { Send(MessageType.LoginFail, new { reason = "That character is already online." }); return; }
        Login(p.Name);
    }

    private void Login(string name)
    {
        var player = (Player)_server.WithWorld(w => w.Players[name])!;
        PlayerName = player.Name;
        IsAdmin = player.IsAdmin;
        CurrentRoomId = string.IsNullOrEmpty(player.CurrentRoomId) ||
                        !_server.World.Rooms.ContainsKey(player.CurrentRoomId)
            ? _server.World.StartingRoomId
            : player.CurrentRoomId;
        Send(MessageType.LoginOk, new { name = PlayerName, isAdmin = IsAdmin });
        _server.BroadcastRoom(CurrentRoomId, $"{PlayerName} appears.", exceptSessionId: Id);
        _server.Log($"{PlayerName} logged in (admin={IsAdmin}).");
        SendRoomView();
    }

    // --------------- Gameplay commands ---------------
    private void HandleCommand(string raw)
    {
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var parts = text.Split(' ', 2);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : "";

        switch (cmd)
        {
            case "look": case "l": SendRoomView(); break;
            case "say": CmdSay(arg); break;
            case "shout": CmdShout(arg); break;
            case "who": CmdWho(); break;
            case "inv": case "inventory": case "i": CmdInventory(); break;
            case "take": case "get": CmdTake(arg); break;
            case "drop": CmdDrop(arg); break;
            case "help": CmdHelp(); break;
            case "quit": SendText("Goodbye."); Close(); break;
            case "n": case "north": Move("north"); break;
            case "s": case "south": Move("south"); break;
            case "e": case "east": Move("east"); break;
            case "w": case "west": Move("west"); break;
            case "u": case "up": Move("up"); break;
            case "d": case "down": Move("down"); break;
            case "go": Move(arg.Trim().ToLowerInvariant()); break;
            default: SendText($"Unknown command: {cmd}. Try 'help'."); break;
        }
    }

    private void CmdHelp()
    {
        SendText("Commands: look, go <dir>, n/s/e/w/u/d, say <msg>, shout <msg>, who, inv, take <item>, drop <item>, quit");
        if (IsAdmin) SendText("You are an admin. Use the World Editor tab to build the world.");
    }

    private void CmdSay(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) { SendText("Say what?"); return; }
        SendText($"You say, \"{msg}\"");
        _server.BroadcastRoom(CurrentRoomId, $"{PlayerName} says, \"{msg}\"", exceptSessionId: Id);
    }

    private void CmdShout(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) { SendText("Shout what?"); return; }
        _server.BroadcastAll($"{PlayerName} shouts, \"{msg}\"");
    }

    private void CmdWho()
    {
        var names = _server.Sessions.Values.Where(s => s.IsLoggedIn).Select(s => s.PlayerName + (s.IsAdmin ? " [admin]" : ""));
        SendText("Online: " + string.Join(", ", names));
    }

    private void CmdInventory()
    {
        var inv = (List<string>)_server.WithWorld(w => w.Players[PlayerName!].Inventory.ToList())!;
        if (inv.Count == 0) { SendText("You are carrying nothing."); return; }
        var names = inv.Select(id => _server.World.Items.TryGetValue(id, out var it) ? it.Name : "something");
        SendText("You are carrying: " + string.Join(", ", names));
    }

    private void CmdTake(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) { SendText("Take what?"); return; }
        string? takenName = null;
        _server.MutateWorld(w =>
        {
            if (!w.Rooms.TryGetValue(CurrentRoomId, out var room)) return;
            var itemId = room.ItemIds.FirstOrDefault(id =>
                w.Items.TryGetValue(id, out var it) && it.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));
            if (itemId == null) return;
            var item = w.Items[itemId];
            if (!item.Takeable) return;
            room.ItemIds.Remove(itemId);
            w.Players[PlayerName!].Inventory.Add(itemId);
            takenName = item.Name;
        });
        if (takenName == null) SendText("You don't see that here.");
        else
        {
            SendText($"You take {takenName}.");
            _server.BroadcastRoom(CurrentRoomId, $"{PlayerName} picks up {takenName}.", exceptSessionId: Id);
        }
    }

    private void CmdDrop(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) { SendText("Drop what?"); return; }
        string? droppedName = null;
        _server.MutateWorld(w =>
        {
            var inv = w.Players[PlayerName!].Inventory;
            var itemId = inv.FirstOrDefault(id =>
                w.Items.TryGetValue(id, out var it) && it.Name.Contains(arg, StringComparison.OrdinalIgnoreCase));
            if (itemId == null) return;
            inv.Remove(itemId);
            if (w.Rooms.TryGetValue(CurrentRoomId, out var room))
                room.ItemIds.Add(itemId);
            droppedName = w.Items[itemId].Name;
        });
        if (droppedName == null) SendText("You don't have that.");
        else
        {
            SendText($"You drop {droppedName}.");
            _server.BroadcastRoom(CurrentRoomId, $"{PlayerName} drops {droppedName}.", exceptSessionId: Id);
        }
    }

    private void Move(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) { SendText("Go where?"); return; }
        string? nextRoomId = null;
        _server.WithWorld(w =>
        {
            if (w.Rooms.TryGetValue(CurrentRoomId, out var room) && room.Exits.TryGetValue(dir, out var id))
                nextRoomId = id;
            return 0;
        });
        if (nextRoomId == null || !_server.World.Rooms.ContainsKey(nextRoomId))
        { SendText("You can't go that way."); return; }

        var oldRoom = CurrentRoomId;
        _server.BroadcastRoom(oldRoom, $"{PlayerName} leaves {dir}.", exceptSessionId: Id);
        CurrentRoomId = nextRoomId;
        _server.MutateWorld(w => w.Players[PlayerName!].CurrentRoomId = nextRoomId);
        _server.BroadcastRoom(nextRoomId, $"{PlayerName} arrives.", exceptSessionId: Id);
        SendRoomView();
    }

    private void SendRoomView()
    {
        var room = _server.World.Rooms.TryGetValue(CurrentRoomId, out var r) ? r : null;
        if (room == null) { SendText("You float in nothingness."); return; }
        var itemNames = room.ItemIds.Where(id => _server.World.Items.ContainsKey(id)).Select(id => _server.World.Items[id].Name).ToList();
        var npcNames = room.NpcIds.Where(id => _server.World.Npcs.ContainsKey(id)).Select(id => _server.World.Npcs[id].Name).ToList();
        var others = _server.PlayersInRoom(CurrentRoomId, except: PlayerName);
        Send(MessageType.RoomView, new
        {
            id = room.Id,
            name = room.Name,
            description = room.Description,
            exits = room.Exits.Keys.ToList(),
            items = itemNames,
            npcs = npcNames,
            players = others
        });
    }

    // --------------- Editor handlers ---------------
    private void EditUpsertRoom(NetMessage msg)
    {
        var room = msg.GetPayload<Room>();
        if (room == null || string.IsNullOrEmpty(room.Id)) { EditFail("Invalid room."); return; }
        _server.MutateWorld(w =>
        {
            w.Rooms[room.Id] = room;
            if (string.IsNullOrEmpty(w.StartingRoomId)) w.StartingRoomId = room.Id;
        });
        EditOk($"Room '{room.Name}' saved.");
    }

    private void EditDeleteRoom(NetMessage msg)
    {
        var p = msg.GetPayload<IdPayload>();
        if (p == null) { EditFail("Invalid."); return; }
        _server.MutateWorld(w =>
        {
            w.Rooms.Remove(p.Id);
            foreach (var r in w.Rooms.Values)
            {
                var keys = r.Exits.Where(kv => kv.Value == p.Id).Select(kv => kv.Key).ToList();
                foreach (var k in keys) r.Exits.Remove(k);
            }
            if (w.StartingRoomId == p.Id)
                w.StartingRoomId = w.Rooms.Keys.FirstOrDefault() ?? "";
        });
        EditOk("Room deleted.");
    }

    private void EditUpsert<T>(NetMessage msg, Func<World, Dictionary<string, T>> dictFn) where T : class
    {
        var entity = msg.GetPayload<T>();
        if (entity == null) { EditFail("Invalid payload."); return; }
        var idProp = typeof(T).GetProperty("Id")!;
        var id = (string)idProp.GetValue(entity)!;
        _server.MutateWorld(w => dictFn(w)[id] = entity);
        EditOk($"{typeof(T).Name} saved.");
    }

    private void EditDelete<T>(NetMessage msg, Func<World, Dictionary<string, T>> dictFn, string label)
    {
        var p = msg.GetPayload<IdPayload>();
        if (p == null) { EditFail("Invalid."); return; }
        _server.MutateWorld(w =>
        {
            dictFn(w).Remove(p.Id);
            foreach (var r in w.Rooms.Values)
            {
                r.ItemIds.RemoveAll(x => x == p.Id);
                r.NpcIds.RemoveAll(x => x == p.Id);
            }
        });
        EditOk($"{label} deleted.");
    }

    private void EditSetStartingRoom(NetMessage msg)
    {
        var p = msg.GetPayload<IdPayload>();
        if (p == null) { EditFail("Invalid."); return; }
        _server.MutateWorld(w => { if (w.Rooms.ContainsKey(p.Id)) w.StartingRoomId = p.Id; });
        EditOk("Starting room updated.");
    }

    private void EditPlaceInRoom(NetMessage msg, bool isItem, bool add)
    {
        var p = msg.GetPayload<PlaceInRoomPayload>();
        if (p == null) { EditFail("Invalid."); return; }
        _server.MutateWorld(w =>
        {
            if (!w.Rooms.TryGetValue(p.RoomId, out var r)) return;
            var list = isItem ? r.ItemIds : r.NpcIds;
            if (add) { if (!list.Contains(p.EntityId)) list.Add(p.EntityId); }
            else { list.Remove(p.EntityId); }
        });
        EditOk(add ? "Placed." : "Removed.");
    }

    private void EditOk(string message) => Send(MessageType.EditorAck, new { ok = true, message });
    private void EditFail(string message) => Send(MessageType.EditorAck, new { ok = false, message });
}
