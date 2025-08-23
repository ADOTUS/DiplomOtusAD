using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoexWatchlistsBot.Models
{
    public class SecurityInfo
    {
        public string SecId { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public List<string> Boards { get; set; } = new();
    }
}
