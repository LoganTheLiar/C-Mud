namespace MudShared.Models;

public class Room
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "An unnamed room";
    public string Description { get; set; } = "There is nothing to see here yet.";
    public Dictionary<string, string> Exits { get; set; } = new(); // direction -> room id
    public List<string> ItemIds { get; set; } = new();
    public List<string> NpcIds { get; set; } = new();
}
