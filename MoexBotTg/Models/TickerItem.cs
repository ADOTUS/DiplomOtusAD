using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoexWatchlistsBot.Models
{
    public class TickerItem
    {
        public string Ticker { get; set; } = null!;
        public string Engine { get; set; } = null!;
        public string Market { get; set; } = null!;
        public string Board { get; set; } = null!;
        public int? BuyAmount { get; set; }
        public decimal? BuyRate { get; set; }
    }
}
