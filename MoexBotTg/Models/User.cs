namespace MoexWatchlistsBot.Models;

public class User
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public List<BrokerList> Lists { get; set; } = new();
    public PendingAction PendingAction { get; set; } = PendingAction.None;
    public string? PendingDeleteList { get; set; }
}