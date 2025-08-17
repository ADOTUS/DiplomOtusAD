namespace MoexWatchlistsBot.Models;

public class WatchItem
{
    public string Ticker { get; set; } = string.Empty;
    public decimal? TargetPrice { get; set; }  // для уведомлений
}