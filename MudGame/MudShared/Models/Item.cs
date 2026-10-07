namespace MudShared.Models;

public class Item
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "an item";
    public string Description { get; set; } = "";
    public bool Takeable { get; set; } = true;
}
