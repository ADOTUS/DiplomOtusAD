namespace MoexWatchlistsBot.Models;

public class AppUser
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public List<WatchList> Lists { get; set; } = new();
}