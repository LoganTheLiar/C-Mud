namespace MudShared.Models;

public class World
{
    public string StartingRoomId { get; set; } = "";
    public Dictionary<string, Room> Rooms { get; set; } = new();
    public Dictionary<string, Item> Items { get; set; } = new();
    public Dictionary<string, Npc> Npcs { get; set; } = new();
    public Dictionary<string, Player> Players { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
