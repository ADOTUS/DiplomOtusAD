using System.Collections.Generic;

namespace MoexWatchlistsBot.Scenarios
{
    public class ScenarioContext
    {
        public string Name { get; init; } = string.Empty;
        public bool IsCompleted { get; set; }
        public int Step { get; set; } = 0;
        public Dictionary<string, string> Data { get; } = new();
    }
}