using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MudShared.Models;
using MudShared.Protocol;

namespace MudServer;

public class GameServer
{
    public int Port { get; }
    public World World { get; private set; } = new();
    public ConcurrentDictionary<string, ClientSession> Sessions { get; } = new(); // key: session id

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly object _worldLock = new();
    private readonly string _worldPath;

    public event Action<string>? LogMessage;
    public event Action? SessionsChanged;

    public GameServer(int port)
    {
        Port = port;
        _worldPath = Path.Combine(AppContext.BaseDirectory, "world.json");
        LoadWorld();
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogMessage?.Invoke(line);
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start();
        Log($"Server listening on port {Port}");

        return Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    var session = new ClientSession(client, this);
                    Sessions[session.Id] = session;
                    SessionsChanged?.Invoke();
                    Log($"Client connected: {session.Id} from {client.Client.RemoteEndPoint}");
                    _ = session.RunAsync();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Log("Accept error: " + ex.Message); }
            }
        });
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        foreach (var s in Sessions.Values) s.Close();
        SaveWorld();
    }

    public void OnSessionClosed(ClientSession s)
    {
        Sessions.TryRemove(s.Id, out _);
        SessionsChanged?.Invoke();
        if (!string.IsNullOrEmpty(s.PlayerName))
        {
            BroadcastRoom(s.CurrentRoomId, $"{s.PlayerName} has disconnected.", exceptSessionId: s.Id);
            Log($"{s.PlayerName} disconnected.");
        }
    }

    public void BroadcastRoom(string roomId, string line, string? exceptSessionId = null)
    {
        foreach (var s in Sessions.Values)
        {
            if (s.CurrentRoomId == roomId && s.Id != exceptSessionId && s.IsLoggedIn)
                s.SendText(line);
        }
    }

    public void BroadcastAll(string line)
    {
        foreach (var s in Sessions.Values)
            if (s.IsLoggedIn) s.SendText(line);
    }

    public List<string> PlayersInRoom(string roomId, string? except = null)
    {
        return Sessions.Values
            .Where(s => s.IsLoggedIn && s.CurrentRoomId == roomId && s.PlayerName != except)
            .Select(s => s.PlayerName!).ToList();
    }

    // ------------ World persistence ------------
    public void LoadWorld()
    {
        lock (_worldLock)
        {
            if (File.Exists(_worldPath))
            {
                try
                {
                    var json = File.ReadAllText(_worldPath);
                    var loaded = JsonSerializer.Deserialize<World>(json, NetMessage.Options);
                    if (loaded != null) World = loaded;
                }
                catch { World = new World(); }
            }
            if (World.Rooms.Count == 0)
            {
                var start = new Room { Name = "The Void", Description = "A featureless void. Build something here!" };
                World.Rooms[start.Id] = start;
                World.StartingRoomId = start.Id;
            }
        }
    }

    public void SaveWorld()
    {
        lock (_worldLock)
        {
            var json = JsonSerializer.Serialize(World, new JsonSerializerOptions(NetMessage.Options) { WriteIndented = true });
            File.WriteAllText(_worldPath, json);
        }
    }

    public object WithWorld<T>(Func<World, T> fn)
    {
        lock (_worldLock) return fn(World)!;
    }

    public void MutateWorld(Action<World> fn)
    {
        lock (_worldLock) { fn(World); }
        SaveWorld();
    }
}
