namespace MoexWatchlistsBot.Models;

public class WatchList
{
    public string Name { get; set; } = string.Empty;
    public List<WatchItem> Items { get; set; } = new();
}