namespace MudShared.Models;

public class Npc
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "a stranger";
    public string Description { get; set; } = "";
    public string Greeting { get; set; } = "";
}
