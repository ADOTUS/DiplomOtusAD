using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using MoexWatchlistsBot.Services;

namespace MoexWatchlistsBot;

public class Program
{
    private static readonly CancellationTokenSource _cts = new();

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Загружаем данные
        var storage = new Storage("data.json");
        await storage.LoadAsync();

        // Токен
        var token = Environment.GetEnvironmentVariable("TG_MOEX_TOKEN") ?? "YOUR_BOT_TOKEN_HERE";
        if (string.IsNullOrWhiteSpace(token) || token == "YOUR_BOT_TOKEN_HERE")
        {
            Console.WriteLine("⚠️ Установите TG_MOEX_TOKEN в переменных окружения.");
        }

        var bot = new TelegramBotClient(token);
        var me = await bot.GetMe();
        Console.WriteLine($"🤖 Bot @{me.Username} is starting...");

        var handler = new UpdateHandler(bot, storage);

        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

        bot.StartReceiving(
            async (botClient, update, ct) =>
            {
                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    Console.WriteLine($"💬 CallbackQuery from chat {update.CallbackQuery.Message.Chat.Id}: {update.CallbackQuery.Data}");
                    await UpdateHandler.HandleCallbackQueryAsync(botClient, update.CallbackQuery, storage, ct);
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