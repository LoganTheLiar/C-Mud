using System.Text.Json;
using System.Text.Json.Serialization;

namespace MudShared.Protocol;

/// <summary>
/// Simple JSON-over-TCP message, newline-delimited.
/// </summary>
public class NetMessage
{
    public string Type { get; set; } = "";
    public JsonElement? Payload { get; set; }

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(string type, object? payload)
    {
        var msg = new
        {
            type,
            payload
        };
        return JsonSerializer.Serialize(msg, Options);
    }

    public static NetMessage? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<NetMessage>(json, Options);
    }

    public T? GetPayload<T>()
    {
        if (Payload == null) return default;
        return JsonSerializer.Deserialize<T>(Payload.Value.GetRawText(), Options);
    }
}

public static class MessageType
{
    // Client -> Server
    public const string Login = "login";          // { name, password }
    public const string Register = "register";    // { name, password }
    public const string Command = "command";      // { text }

    // Server -> Client
    public const string LoginOk = "loginOk";      // { name, isAdmin, startingRoom }
    public const string LoginFail = "loginFail";  // { reason }
    public const string Text = "text";            // { line, color? }
    public const string RoomView = "roomView";    // { name, description, exits[], items[], npcs[], players[] }
    public const string WorldSnapshot = "worldSnapshot"; // full world (admin editor)
    public const string EditorAck = "editorAck";  // { ok, message }

    // Admin editor (client -> server, requires IsAdmin)
    public const string RequestWorld = "requestWorld";
    public const string UpsertRoom = "upsertRoom";
    public const string DeleteRoom = "deleteRoom";
    public const string UpsertItem = "upsertItem";
    public const string DeleteItem = "deleteItem";
    public const string UpsertNpc = "upsertNpc";
    public const string DeleteNpc = "deleteNpc";
    public const string SetStartingRoom = "setStartingRoom";
    public const string PlaceItemInRoom = "placeItemInRoom";
    public const string PlaceNpcInRoom = "placeNpcInRoom";
    public const string RemoveItemFromRoom = "removeItemFromRoom";
    public const string RemoveNpcFromRoom = "removeNpcFromRoom";
}
