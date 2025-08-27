
namespace MoexWatchlistsBot.Models
{
    public class CandleAnalytics
    {
        public string SecId { get; set; } = null!;
        public string PeriodDescription { get; set; } = null!;
        public decimal CurrentClose { get; set; }
        public decimal? ChangeDay { get; set; }
        public decimal? ChangePeriod { get; set; }
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public decimal TotalVolume { get; set; }
    }
}
