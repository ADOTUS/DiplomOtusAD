using MoexWatchlistsBot.Models;

namespace MoexWatchlistsBot.Services
{
    public interface IMoexService
    {
        Task<SecurityInfo?> GetSecurityByTickerAsync(string ticker, string engine, string market, string board);
        Task<(decimal? lastPrice, DateTime? lastTime)> GetLastPriceAsync(string secId, string engine, string market, string board);
    }
}
