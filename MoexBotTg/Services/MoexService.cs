using MoexWatchlistsBot.Models;
using System.Net;
using System.Text.Json;

namespace MoexWatchlistsBot.Services;

public class MoexService : IMoexService
{
    private readonly HttpClient _http;

    public MoexService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _http = httpClient;
            return;
        }

        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "moex-test/1.0 (+github.com/yourname)");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
    }

    public async Task<SecurityInfo?> GetSecurityByTickerAsync(string ticker, string engine, string market, string board)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new ArgumentException(nameof(ticker));
        ticker = ticker.Trim().ToUpperInvariant();

        var url =
            $"https://iss.moex.com/iss/engines/{engine}/markets/{market}/boards/{board}/securities/{ticker}.json" +
            "?iss.only=securities&iss.meta=off&securities.columns=SECID,SHORTNAME,TYPE,GROUP";

        Console.WriteLine($"debug sec {url}");
        return await TryReadSingleSecurity(url);
    }

    public async Task<(decimal? lastPrice, DateTime? lastTime)> GetLastPriceAsync(string secId, string engine, string market, string board)
    {
        var url = $"https://iss.moex.com/iss/engines/{engine}/markets/{market}/boards/{board}/securities/{secId}.json";
        Console.WriteLine($"debug price {url}");
        var response = await _http.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return (null, null);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        if (!root.TryGetProperty("marketdata", out var marketdataElement))
            return (null, null);

        var columns = marketdataElement.GetProperty("columns");
        var data = marketdataElement.GetProperty("data");

        int lastIndex = -1;
        int timeIndex = -1;

        for (int i = 0; i < columns.GetArrayLength(); i++)
        {
            var colName = columns[i].GetString()?.ToUpper();
            if (colName == "LAST") lastIndex = i;
            if (colName == "TIME") timeIndex = i;
        }

        if (data.GetArrayLength() == 0)
            return (null, null);

        var row = data[0];
        decimal? last = lastIndex >= 0 && row[lastIndex].ValueKind == JsonValueKind.Number
            ? row[lastIndex].GetDecimal()
            : (decimal?)null;

        DateTime? lastTime = null;
        if (timeIndex >= 0 && row[timeIndex].ValueKind == JsonValueKind.String)
        {
            var timeStr = row[timeIndex].GetString();
            if (!string.IsNullOrEmpty(timeStr))
                lastTime = DateTime.Parse(timeStr);
        }

        return (last, lastTime);
    }

    private static int Col(JsonElement columns, string name)
    {
        for (int i = 0; i < columns.GetArrayLength(); i++)
        {
            if (string.Equals(columns[i].GetString(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private async Task<SecurityInfo?> TryReadSingleSecurity(string url)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("securities", out var secBlock)) return null;
        var data = secBlock.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;

        var cols = secBlock.GetProperty("columns");
        int iSecId = Col(cols, "secid");
        int iShort = Col(cols, "shortname");
        int iType = Col(cols, "type");
        int iGroup = Col(cols, "group");

        var row = data[0];
        return new SecurityInfo
        {
            SecId = iSecId >= 0 ? row[iSecId].GetString() ?? "" : "",
            ShortName = iShort >= 0 ? row[iShort].GetString() ?? "" : "",
            Type = iType >= 0 ? row[iType].GetString() ?? "" : "",
            Group = iGroup >= 0 ? row[iGroup].GetString() ?? "" : "",
            Boards = new List<string> { }
        };
    }
    public async Task<List<Candle>> GetCandlesAsync(
    string secId, string engine, string market,
    int interval, DateTime from, DateTime till)
    {
        var url = $"https://iss.moex.com/iss/engines/{engine}/markets/{market}/securities/{secId}/candles.json" +
                  $"?interval={interval}&from={from:yyyy-MM-dd}&till={till:yyyy-MM-dd}";
        Console.WriteLine(url);
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return new List<Candle>();

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("candles", out var candlesBlock))
            return new List<Candle>();

        var cols = candlesBlock.GetProperty("columns");
        var data = candlesBlock.GetProperty("data");

        int iBegin = Col(cols, "begin");
        int iEnd = Col(cols, "end");
        int iOpen = Col(cols, "open");
        int iClose = Col(cols, "close");
        int iHigh = Col(cols, "high");
        int iLow = Col(cols, "low");
        int iVol = Col(cols, "volume");

        var list = new List<Candle>();
        foreach (var row in data.EnumerateArray())
        {
            list.Add(new Candle
            {
                Begin = iBegin >= 0 ? DateTime.Parse(row[iBegin].GetString()!) : DateTime.MinValue,
                End = iEnd >= 0 ? DateTime.Parse(row[iEnd].GetString()!) : DateTime.MinValue,
                Open = iOpen >= 0 ? row[iOpen].GetDecimal() : 0,
                Close = iClose >= 0 ? row[iClose].GetDecimal() : 0,
                High = iHigh >= 0 ? row[iHigh].GetDecimal() : 0,
                Low = iLow >= 0 ? row[iLow].GetDecimal() : 0,
                Volume = iVol >= 0 ? row[iVol].GetDecimal() : 0
            });
        }

        return list;
    }
    public async Task<CandleAnalytics?> GetCandleAnalyticsAsync(
    string secId, string engine, string market,
    int interval, DateTime from, DateTime till)
    {
        var candles = await GetCandlesAsync(secId, engine, market, interval, from, till);
        if (candles.Count == 0) return null;

        var last = candles.Last();
        var first = candles.First();

        decimal? changePeriod = (last.Close - first.Open) / first.Open * 100;

        var peakVolumeCandle = candles.OrderByDescending(c => c.Volume).First();
        
        decimal? changeMaxVolumeCandle = peakVolumeCandle.Open != 0
            ? (peakVolumeCandle.Close - peakVolumeCandle.Open) / peakVolumeCandle.Open * 100
            : null;

        return new CandleAnalytics
        {
            SecId = secId,
            PeriodDescription = $"{from:dd.MM.yyyy} – {till:dd.MM.yyyy}",
            CurrentClose = last.Close,
            PeakVolume = peakVolumeCandle.Volume,
            PeakVolumeBegin = peakVolumeCandle.Begin,
            PeakVolumeEnd = peakVolumeCandle.End,
            ChangeMaxVolumeCandle = changeMaxVolumeCandle,
            ChangePeriod = changePeriod,
            Min = candles.Min(c => c.Low),
            Max = candles.Max(c => c.High),
            TotalVolume = candles.Sum(c => c.Volume)
        };
    }
}