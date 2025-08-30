using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using MoexWatchlistsBot.Services;

namespace MoexWatchlistsBot.Scenarios
{
    public interface IScenario
    {
        string Name { get; } 

        Task StartAsync(
            ITelegramBotClient bot,
            long chatId,
            Models.User user,
            ScenarioContext context,
            CancellationToken ct);
        Task HandleMessageAsync(
            ITelegramBotClient bot,
            Message message,
            ScenarioContext context,
            Storage storage,
            CancellationToken ct);
    }
}