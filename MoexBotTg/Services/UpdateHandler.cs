using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Scenarios;
using MoexWatchlistsBot.Ui;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Services;

public class UpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly Storage _storage;

    private readonly Dictionary<long, UserSession> _sessions = new();
    private readonly Dictionary<string, IScenario> _scenariosByName;
    private readonly Dictionary<long, ScenarioContext> _scenarioContexts = new();

    public UpdateHandler(ITelegramBotClient bot, Storage storage, IEnumerable<IScenario> scenarios)
    {
        _bot = bot;
        _storage = storage;
        _scenariosByName = scenarios.ToDictionary(s => s.Name);
    }
    private async Task StartScenarioAsync(string name, long chatId, Models.User user, CancellationToken ct)
    {
        if (!_scenariosByName.TryGetValue(name, out var scenario))
        {
            await _bot.SendMessage(chatId, "⚠️ Сценарий не найден.", cancellationToken: ct);
            return;
        }

        var ctx = new ScenarioContext { Name = name };
        _scenarioContexts[chatId] = ctx;
        await scenario.StartAsync(_bot, chatId, user, ct);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != Telegram.Bot.Types.Enums.UpdateType.Message &&
            update.Type != Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            return;

        if (update.Message is { } msg)
        {
            var chatId = msg.Chat.Id;
            var text = msg.Text ?? "";

           

            if (_scenarioContexts.TryGetValue(chatId, out var ctx) && _scenariosByName.TryGetValue(ctx.Name, out var activeScenario))
            {
                Console.WriteLine("debug");
                await activeScenario.HandleMessageAsync(_bot, msg, ctx, _storage, ct);

                if (ctx.IsCompleted)
                    _scenarioContexts.Remove(chatId);

                return;
            }

            if (!_sessions.ContainsKey(chatId))
                _sessions[chatId] = new UserSession();

            var session = _sessions[chatId];
            var user = _storage.TryGetUser(chatId);

            // Обработка команд /start
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await OnStartCommand(msg, ct);
                return;
            }

            if (user is null)
            {
                await bot.SendMessage(chatId,
                    "ℹ️ Вы ещё не зарегистрированы. Нажмите /start",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }
            
            // Главные кнопки главного меню
            if (text == "🔍 Поиск бумаги")
            {
                await bot.SendMessage(chatId, "Функция поиска пока не реализована.", cancellationToken: ct);
                return;
            }

            if (text == "📋 Мои списки")
            {
                await bot.SendMessage(chatId, "Ваши списки:", replyMarkup: Keyboards.BuildUserListsKeyboard(user), cancellationToken: ct);
                return;
            }

            if (text == "ℹ️ Информация о программе")
            {
                await SendProgramInfo(chatId, ct);
                return;
            }

            // Кнопка добавления списка
            if (text == UiTexts.AddList)
            {
                await StartScenarioAsync("AddList", chatId, user, ct);
                return;
            }

            if (text == "🗑 Удалить список")
            {
                await StartScenarioAsync("DeleteList", chatId, user, ct);
                return;
            }

            // Кнопка отмены действия
            if (text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                session.PendingAction = PendingAction.None;
                await bot.SendMessage(chatId, "❎ Действие отменено.", cancellationToken: ct);
                return;
            }

            if (text == "Вернуться")
            {
                await _bot.SendMessage(
                    chatId,
                    "Возвращаемся на главное меню",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }
            // Открытие конкретного списка
            if (user.Lists.Any(l => l.Name == text))
            {
                await bot.SendMessage(chatId, $"📂 Открыт список: {text}", cancellationToken: ct);
                return;
            }
        }
    }
    public async Task HandleCallbackQueryAsync(
        ITelegramBotClient bot,
        CallbackQuery callbackQuery,
        Storage storage,
        CancellationToken ct)
    {
        if (callbackQuery.Message == null)
            return;

        var chatId = callbackQuery.Message.Chat.Id;

        // Если активен сценарий — отдаём ему callback
        if (_scenarioContexts.TryGetValue(chatId, out var ctx))
        {
            if (ctx.Name == "DeleteList" && _scenariosByName.TryGetValue("DeleteList", out var scenario))
            {
                var deleteListScenario = scenario as DeleteListScenario;
                if (deleteListScenario != null)
                {
                    await deleteListScenario.HandleCallbackAsync(bot, callbackQuery, _storage, ctx, ct);

                    if (ctx.IsCompleted)
                        _scenarioContexts.Remove(chatId);

                    return;
                }
            }

        }

        // Глобальные callback’и (если есть)
        var data = callbackQuery.Data ?? string.Empty;
        if (data.StartsWith("open_"))
        {
            var listName = data.Substring("open_".Length);
            await _bot.SendMessage(chatId, $"📂 Открыт список: {listName}", cancellationToken: ct);
        }
    }
    private async Task OnStartCommand(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var user = _storage.TryGetUser(chatId);

        if (user is null)
        {
            // Создаем нового пользователя
            user = _storage.GetOrCreateUser(chatId, msg.From?.Username);

            // Создаем список MyFavorites, если его нет
            if (!user.Lists.Any(l => l.Name.Equals("MyFavorites", StringComparison.OrdinalIgnoreCase)))
                user.Lists.Add(new WatchList { Name = "MyFavorites" });

            await _storage.SaveAsync();

            await _bot.SendMessage(
                chatId,
                "✅ Регистрация выполнена. Добро пожаловать!",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(), // Главное меню с 3 кнопками
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                $"👋 С возвращением, {user.Username ?? user.ChatId.ToString()}!",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(), // Главное меню с 3 кнопками
                cancellationToken: ct);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"❌ Update error: {ex.Message}\n{ex}");
        return Task.CompletedTask;
    }

    private async Task SendProgramInfo(long chatId, CancellationToken ct)
    {
        string info = "📝 MoexWatchlistsBot\nВерсия: 1.0\nАвтор: Anton\n\nС помощью бота вы можете создавать и управлять списками бумаг на MOEX.";
        await _bot.SendMessage(chatId, info, cancellationToken: ct);
    }
}