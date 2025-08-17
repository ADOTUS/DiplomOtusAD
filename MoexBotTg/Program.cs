using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Services;
using MoexWatchlistsBot.Session;
using MoexWatchlistsBot.Startup;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot;

public class Program
{
    private static ITelegramBotClient _bot = null!;
    private static readonly CancellationTokenSource _cts = new();

    // Хранилище пользователей с сохранением в файл
    private static readonly Storage _storage = new("data.json");

    // Сессии пользователей (не сохраняются)
    private static readonly Dictionary<long, UserSession> _sessions = new();

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Загрузка сохранённых пользователей
        await _storage.LoadAsync();

        // Инициализация Telegram Bot
        var token = Environment.GetEnvironmentVariable("TG_MOEX_TOKEN") ?? "YOUR_BOT_TOKEN_HERE";
        if (string.IsNullOrWhiteSpace(token) || token == "YOUR_BOT_TOKEN_HERE")
        {
            Console.WriteLine("⚠️ Set TELEGRAM_BOT_TOKEN environment variable or edit token in code.");
        }

        _bot = new TelegramBotClient(token);
        var me = await _bot.GetMe();
        Console.WriteLine($"🤖 Bot @{me.Username} is starting...");

        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
        _bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, _cts.Token);

        Console.WriteLine("✅ Bot is running. Press Ctrl+C to exit.");
        AppDomain.CurrentDomain.ProcessExit += (_, __) => _cts.Cancel();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        try { await Task.Delay(Timeout.Infinite, _cts.Token); } catch { }

        await _storage.SaveAsync();
        Console.WriteLine("👋 Shutdown complete.");
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != UpdateType.Message && update.Type != UpdateType.CallbackQuery) return;

        if (update.Type == UpdateType.Message && update.Message is { } msg)
        {
            var chatId = msg.Chat.Id;
            var text = msg.Text ?? string.Empty;

            if (!_sessions.ContainsKey(chatId))
                _sessions[chatId] = new UserSession();

            // Команды
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await OnStartCommand(msg, ct);
                return;
            }
            if (text.StartsWith("/lists", StringComparison.OrdinalIgnoreCase))
            {
                await ShowLists(chatId, ct);
                return;
            }

            var session = _sessions[chatId];

            // Ожидание ввода нового списка
            if (session.PendingAction == PendingAction.WaitingListName)
            {
                var listName = text.Trim();
                if (string.IsNullOrWhiteSpace(listName))
                {
                    await bot.SendMessage(chatId, "❗ Название списка не может быть пустым. Введите другое или /cancel", cancellationToken: ct);
                    return;
                }

                var user = _storage.GetOrCreateUser(chatId, msg.From?.Username);
                if (user.Lists.Any(l => string.Equals(l.Name, listName, StringComparison.OrdinalIgnoreCase)))
                {
                    await bot.SendMessage(chatId, "⚠️ Список с таким именем уже существует. Введите другое название.", cancellationToken: ct);
                    return;
                }

                user.Lists.Add(new WatchList { Name = listName });
                await _storage.SaveAsync();

                session.PendingAction = PendingAction.None;
                await bot.SendMessage(chatId, $"✅ Список \"{listName}\" создан.", replyMarkup: BuildMainKeyboard(user), cancellationToken: ct);
                return;
            }

            // Добавление нового списка
            if (text == UiTexts.AddList)
            {
                session.PendingAction = PendingAction.WaitingListName;
                await bot.SendMessage(chatId, "📝 Введите название нового списка:", cancellationToken: ct);
                return;
            }

            // Отмена действия
            if (text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                session.PendingAction = PendingAction.None;
                await bot.SendMessage(chatId, "❎ Действие отменено.", cancellationToken: ct);
                return;
            }

            // Выбор существующего списка
            var maybeUser = _storage.TryGetUser(chatId);
            if (maybeUser is not null && maybeUser.Lists.Any(l => l.Name == text))
            {
                await bot.SendMessage(chatId, $"📂 Открыт список: {text}\n(Позже добавим управление бумагами и MOEX)", cancellationToken: ct);
                return;
            }

            if (maybeUser is null)
            {
                await bot.SendMessage(chatId,
                    "ℹ️ Вы ещё не зарегистрированы. Нажмите /start",
                    replyMarkup: BuildStartKeyboard(),
                    cancellationToken: ct);
                return;
            }
        }

        if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is { } cq)
        {
            await _bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        }
    }

    private static async Task OnStartCommand(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var user = _storage.TryGetUser(chatId);
        if (user is null)
        {
            user = _storage.GetOrCreateUser(chatId, msg.From?.Username);
            if (!user.Lists.Any(l => l.Name.Equals("MyFavorites", StringComparison.OrdinalIgnoreCase)))
                user.Lists.Add(new WatchList { Name = "MyFavorites" });

            await _storage.SaveAsync();

            await _bot.SendMessage(chatId, "✅ Регистрация выполнена.",
                replyMarkup: BuildMainKeyboard(user), cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId, $"👋 С возвращением, {FormatUser(user)}! Ваши списки:",
                replyMarkup: BuildMainKeyboard(user), cancellationToken: ct);
        }
    }

    private static async Task ShowLists(long chatId, CancellationToken ct)
    {
        var user = _storage.TryGetUser(chatId);
        if (user is null)
        {
            await _bot.SendMessage(chatId, "ℹ️ Вы ещё не зарегистрированы. Нажмите /start", cancellationToken: ct);
            return;
        }

        var listsText = user.Lists.Count == 0 ? "(пока нет списков)" : string.Join("\n", user.Lists.Select(l => $"• {l.Name}"));
        await _bot.SendMessage(chatId, $"📋 Ваши списки:\n{listsText}", replyMarkup: BuildMainKeyboard(user), cancellationToken: ct);
    }
    private static ReplyKeyboardMarkup BuildStartKeyboard()
    {
        var rows = new List<KeyboardButton[]> { new[] { new KeyboardButton("/start") } };
        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }
    private static ReplyKeyboardMarkup BuildMainKeyboard(AppUser user)
    {
        var rows = new List<KeyboardButton[]>();
        for (int i = 0; i < user.Lists.Count; i += 2)
        {
            if (i + 1 < user.Lists.Count)
                rows.Add(new[] { new KeyboardButton(user.Lists[i].Name), new KeyboardButton(user.Lists[i + 1].Name) });
            else
                rows.Add(new[] { new KeyboardButton(user.Lists[i].Name) });
        }
        rows.Add(new[] { new KeyboardButton(UiTexts.AddList) });
        return new ReplyKeyboardMarkup(rows) { ResizeKeyboard = true, OneTimeKeyboard = false };
    }

    private static string FormatUser(AppUser user) => string.IsNullOrWhiteSpace(user.Username) ? user.ChatId.ToString() : $"@{user.Username}";

    private static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"❌ Update error: {ex.Message}\n{ex}");
        return Task.CompletedTask;
    }
}