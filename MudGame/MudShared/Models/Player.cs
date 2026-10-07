namespace MudShared.Models;

public class Player
{
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; } = false;
    public string CurrentRoomId { get; set; } = "";
    public List<string> Inventory { get; set; } = new();
}
