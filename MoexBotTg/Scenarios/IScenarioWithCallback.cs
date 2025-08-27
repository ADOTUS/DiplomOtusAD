using MoexWatchlistsBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace MoexWatchlistsBot.Scenarios
{
    public interface IScenarioWithCallback : IScenario
    {
        Task HandleCallbackAsync(
            ITelegramBotClient bot,
            CallbackQuery callbackQuery,
            ScenarioContext context,
            Storage storage,
            CancellationToken ct);
    }
}
