namespace MoexWatchlistsBot.Models;

public class User
{
    public long ChatId { get; set; }
    public string? Username { get; set; }
    public List<WatchList> Lists { get; set; } = new();
    public PendingAction PendingAction { get; set; } = PendingAction.None;
    // Временное хранение выбранного для удаления списка
    public string? PendingDeleteList { get; set; }
}