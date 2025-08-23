using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoexWatchlistsBot.Models
{
    public class BrokerList
    {
        public string Name { get; set; } = null!; // "MyFavorites" или "HH:mm"
        public List<TickerItem> Items { get; set; } = new();
        public bool IsDefault => Name == "MyFavorites";
    }
}
