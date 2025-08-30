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
    private readonly NotificationBackgroundService _notificationService; 

    private readonly Dictionary<string, IScenario> _scenariosByName;
    private readonly Dictionary<long, ScenarioContext> _scenarioContexts = new();

    private readonly UserRepository? _userRepo;

    public UpdateHandler(ITelegramBotClient bot
        , Storage storage
        , IEnumerable<IScenario> scenarios
        , NotificationBackgroundService notificationService
        , UserRepository? userRepo = null)
    {
        _bot = bot;
        _storage = storage;
        _scenariosByName = scenarios.ToDictionary(s => s.Name);
        _notificationService = notificationService;
        _userRepo = userRepo;
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

        await scenario.StartAsync(_bot, chatId, user, ctx, ct);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type != Telegram.Bot.Types.Enums.UpdateType.Message &&
            update.Type != Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
            return;

        if (_userRepo != null && update.Message is { } msgUser)
        {
            var from = msgUser.From;
            if (from != null)
            {
                await _userRepo.UpsertUserAsync(
                    msgUser.Chat.Id,
                    from.Username,
                    ct);
            }
        }

        if (update.Message is { } msg)
        {
            var chatId = msg.Chat.Id;
            var text = msg.Text ?? "";

           if (_scenarioContexts.TryGetValue(chatId, out var ctx) && _scenariosByName.TryGetValue(ctx.Name, out var activeScenario))
            {
                Console.WriteLine(activeScenario);
                await activeScenario.HandleMessageAsync(_bot, msg, ctx, _storage, ct);

                if (ctx.IsCompleted)
                    _scenarioContexts.Remove(chatId);

                return;
            }

            var user = _storage.TryGetUser(chatId);

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

            if (text == "🔍 Поиск бумаги")
            {
                await StartScenarioAsync("FindSec", chatId, user, ct);
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

            if (text == "➕ Добавить список нотификации")
            {
                await StartScenarioAsync("AddList", chatId, user, ct);
                return;
            }

            if (text == "🗑 Удалить список")
            {
                await StartScenarioAsync("DeleteList", chatId, user, ct);
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

            if (user.Lists.Any(l => l.Name == text))
            {
                var list = user.Lists.First(l => l.Name == text);

                if (list.Items.Count == 0)
                {
                    await bot.SendMessage(chatId, $"📂 Список {list.Name} пуст.", cancellationToken: ct);
                }
                else
                {
                    var service = new MoexService();
                    var buttons = new List<InlineKeyboardButton[]>();

                    foreach (var item in list.Items)
                    {
                        var (price, _) = await service.GetLastPriceAsync(item.Ticker, item.Engine, item.Market, item.Board);
                        var buttonText = price > 0
                            ? $"{item.Ticker} ({price})"
                            : item.Ticker;

                        buttons.Add(new[]
                        {
                            InlineKeyboardButton.WithCallbackData(buttonText, $"show_{item.Ticker}"),
                            InlineKeyboardButton.WithCallbackData("📊", $"analys_{item.Ticker}"),
                            InlineKeyboardButton.WithCallbackData("❌", $"del_{item.Ticker}")
                        });
                    }

                    var inline = new InlineKeyboardMarkup(buttons);

                    await bot.SendMessage(chatId,
                        $"📂 Список: {list.Name}",
                        replyMarkup: inline,
                        cancellationToken: ct);
                }

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

        if (_scenarioContexts.TryGetValue(chatId, out var ctx) && _scenariosByName.TryGetValue(ctx.Name, out var activeScenario))
        {
            Console.WriteLine("мы тут");

            if (_userRepo != null && callbackQuery.From != null)
            {
                await _userRepo.UpsertUserAsync(
                    callbackQuery.Message!.Chat.Id,
                    callbackQuery.From.Username,
                    ct);
            }

            if (activeScenario is IScenarioWithCallback scenarioWithCallback)
            {
                await scenarioWithCallback.HandleCallbackAsync(bot, callbackQuery, ctx, _storage, ct);

                if (ctx.IsCompleted)
                    _scenarioContexts.Remove(chatId);

                return;
            }
        }

        var data = callbackQuery.Data ?? string.Empty;

        if (data.StartsWith("show_"))
        {
            var ticker = data.Substring("show_".Length);

            var user = _storage.TryGetUser(chatId);
            if (user == null) return;

            var item = user.Lists
                .SelectMany(l => l.Items)
                .FirstOrDefault(i => i.Ticker == ticker);

            if (item == null)
            {
                await bot.SendMessage(chatId, $"❌ Бумага {ticker} не найдена в ваших списках.");
                return;
            }

            await _notificationService.SendTickerInfoAsync(chatId, item);
            return;
        }

        if (data.StartsWith("del_"))
        {
            var ticker = data.Substring("del_".Length);

            var user = _storage.TryGetUser(chatId);
            if (user == null) return;

            foreach (var list in user.Lists)
            {
                var item = list.Items.FirstOrDefault(i => i.Ticker == ticker);
                if (item != null)
                {
                    list.Items.Remove(item);
                    await _storage.SaveAsync();

                    await bot.SendMessage(chatId, $"❎ Бумага {ticker} удалена из списка {list.Name}.", cancellationToken: ct);
                    return;
                }
            }

            await bot.SendMessage(chatId, $"⚠️ Бумага {ticker} не найдена.", cancellationToken: ct);
            return;
        }
        if (data.StartsWith("analys_"))
        {
            var ticker = data.Substring("analys_".Length);
            var user = _storage.TryGetUser(chatId);

            var item = user?.Lists.SelectMany(l => l.Items).FirstOrDefault(i => i.Ticker == ticker);
            if (item == null)
            {
                await bot.SendMessage(chatId, "❌ Бумага не найдена.", cancellationToken: ct);
                return;
            }

            var cotx = new ScenarioContext { Name = "Analytics" };
            cotx.Data["Ticker"] = item.Ticker;
            cotx.Data["Engine"] = item.Engine;
            cotx.Data["Market"] = item.Market;
            cotx.Data["Board"] = item.Board;

            _scenarioContexts[chatId] = cotx;

            await _scenariosByName["Analytics"].StartAsync(bot, chatId, user, cotx, ct);
            return;
        }
    }
    private async Task OnStartCommand(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var user = _storage.TryGetUser(chatId);

        if (user is null)
        {
            user = _storage.GetOrCreateUser(chatId, msg.From?.Username);

            if (!user.Lists.Any(l => l.Name.Equals("MyFavorites", StringComparison.OrdinalIgnoreCase)))
                user.Lists.Add(new BrokerList { Name = "MyFavorites" });

            await _storage.SaveAsync();

            await _bot.SendMessage(
                chatId,
                "✅ Регистрация выполнена. Добро пожаловать!",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                $"👋 С возвращением, {user.Username ?? user.ChatId.ToString()}!",
                replyMarkup: Keyboards.BuildMainMenuKeyboard(),
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
        string info = "📝 MoexWatchlistsBot\nВерсия: 1.0\nАвтор: Anton D\n\nБот может получать действующие на MOEX бумаги и производить следующие действия:\n" +
            "- Искать любую бумагу на площадках TQBR, SPBFUT, CETS и добавлять ее в свои списки\n" +
            "- Создавать свои списки оповещений о котировках в выбранное время\n" +
            "- Получать краткую информацию о своей позиции (параметры задаются вручную)\n" +
            "- Получать текущие котировки в момент запроса\n" +
            "- Производить аналитику по свечам\n" +
            "⚠️-Это free-API. Данные имеют выдержку в 15 минут-⚠️";
        await _bot.SendMessage(chatId, info, cancellationToken: ct);
    }
}