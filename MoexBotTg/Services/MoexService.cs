namespace MoexWatchlistsBot.Services;

public class MoexService
{
    private readonly HttpClient _http;
    public MoexService(HttpClient http) => _http = http;

    // Здесь будем интегрировать MOEX API
}