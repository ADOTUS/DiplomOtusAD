namespace MoexWatchlistsBot.Models;

public class UserSession
{
    public PendingAction PendingAction { get; set; } = PendingAction.None;
}