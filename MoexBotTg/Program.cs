using MoexWatchlistsBot.Scenarios;
using MoexWatchlistsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace MoexWatchlistsBot;

public class Program
{
    private static readonly CancellationTokenSource _cts = new();

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var pgConn = Environment.GetEnvironmentVariable("PG_CONN");
        Console.WriteLine($"PG_CONN = {pgConn ?? "null"}");

        try
        {
            using var conn = new Npgsql.NpgsqlConnection(pgConn);
            conn.Open();
            Console.WriteLine("✅ Подключение успешно");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Ошибка подключения: " + ex.Message);
        }

        UserRepository? userRepo = null;

        if (!string.IsNullOrWhiteSpace(pgConn))
        {
            userRepo = new UserRepository(pgConn);
            Console.WriteLine("🗄  UserRepository подключен к PostgreSQL");
        }


        var storage = new Storage("data.json");
        await storage.LoadAsync();

        var token = Environment.GetEnvironmentVariable("TG_MOEX_TOKEN") ?? "YOUR_BOT_TOKEN_HERE";
        if (string.IsNullOrWhiteSpace(token) || token == "YOUR_BOT_TOKEN_HERE")
        {
            Console.WriteLine("⚠️ Установите TG_MOEX_TOKEN в переменных окружения.");
        }

        var bot = new TelegramBotClient(token);
        var me = await bot.GetMe();
        var service = new MoexService();
        var scenarios = new List<IScenario>
        {
            new AddListScenario(),
            new DeleteListScenario(),
            new FindSecScenario(),
            new AnalyticsScenario()
        };

        var notificationService = new NotificationBackgroundService(bot, storage, _cts.Token);
        notificationService.Start();

        var handler = new UpdateHandler(bot, storage, scenarios, notificationService, userRepo);

        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        bot.StartReceiving(
            async (botClient, update, ct) =>
            {
                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    Console.WriteLine($"💬 CallbackQuery from chat {update.CallbackQuery.Message.Chat.Id}: {update.CallbackQuery.Data}");
                    await handler.HandleCallbackQueryAsync(botClient, update.CallbackQuery, storage, ct);
                }
                else
                {
                    await handler.HandleUpdateAsync(botClient, update, ct);
                }
            },
            handler.HandleErrorAsync,
            receiverOptions,
            _cts.Token
        );

        Console.WriteLine("✅ Bot is running. Press Ctrl+C to exit.");
        AppDomain.CurrentDomain.ProcessExit += (_, __) => _cts.Cancel();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        try { await Task.Delay(Timeout.Infinite, _cts.Token); } catch { }

        await storage.SaveAsync();
        Console.WriteLine("👋 Shutdown complete.");
    }
}