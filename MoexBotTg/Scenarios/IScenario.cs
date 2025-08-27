using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using MoexWatchlistsBot.Services; // для Storage

namespace MoexWatchlistsBot.Scenarios
{
    public interface IScenario
    {
        string Name { get; }   // например "AddList"

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