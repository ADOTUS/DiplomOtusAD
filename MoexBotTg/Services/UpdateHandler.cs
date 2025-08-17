using MoexWatchlistsBot.Models;
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

    public UpdateHandler(ITelegramBotClient bot, Storage storage)
    {
        _bot = bot;
        _storage = storage;
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

            if (!_sessions.ContainsKey(chatId))
                _sessions[chatId] = new UserSession();

            var session = _sessions[chatId];
            var user = _storage.TryGetUser(chatId);

            if (user is null)
            {
                await bot.SendMessage(chatId,
                    "ℹ️ Вы ещё не зарегистрированы. Нажмите /start",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);
                return;
            }

            // Обработка команд /start
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            {
                await OnStartCommand(msg, ct);
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

            // Если пользователь добавляет новый список
            if (session.PendingAction == PendingAction.WaitingListName)
            {
                var listName = text.Trim();
                if (string.IsNullOrWhiteSpace(listName))
                {
                    await bot.SendMessage(chatId, "❗ Название списка не может быть пустым. Введите другое или /cancel", cancellationToken: ct);
                    return;
                }

                if (user.Lists.Any(l => string.Equals(l.Name, listName, StringComparison.OrdinalIgnoreCase)))
                {
                    await bot.SendMessage(chatId, "⚠️ Список с таким именем уже существует.", cancellationToken: ct);
                    return;
                }

                user.Lists.Add(new WatchList { Name = listName });
                await _storage.SaveAsync();

                session.PendingAction = PendingAction.None;
                await bot.SendMessage(chatId, $"✅ Список \"{listName}\" создан.", replyMarkup: Keyboards.BuildUserListsKeyboard(user), cancellationToken: ct);
                return;
            }

            // Кнопка добавления списка
            if (text == UiTexts.AddList)
            {
                session.PendingAction = PendingAction.WaitingListName;
                await bot.SendMessage(chatId, "📝 Введите название нового списка:", cancellationToken: ct);
                return;
            }

            // Кнопка отмены действия
            if (text.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                session.PendingAction = PendingAction.None;
                await bot.SendMessage(chatId, "❎ Действие отменено.", cancellationToken: ct);
                return;
            }

            // Кнопка удалить список
            if (text == "🗑 Удалить список")
            {
                await HandleDeleteList(bot, user, ct);
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
    public static async Task HandleCallbackQueryAsync(
    ITelegramBotClient bot,
    CallbackQuery callbackQuery,
    Storage storage,
    CancellationToken ct)
    {
        if (callbackQuery.Data == null)
            return;

        Console.WriteLine($"💬 CallbackQuery {callbackQuery.Message.Chat.Id}: {callbackQuery.Data}");

        var data = callbackQuery.Data;
        var chatId = callbackQuery.Message.Chat.Id;

        var user = storage.TryGetUser(chatId);
        if (user == null) return;

        if (data.StartsWith("delete_"))
        {
            var listName = data.Substring("delete_".Length);

            var confirmKeyboard = new InlineKeyboardMarkup(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", $"confirmdel_{listName}"),
                InlineKeyboardButton.WithCallbackData("❌ Нет", "cancel_delete")
            }
        });

            await bot.SendMessage(
                chatId,
                $"Вы уверены, что хотите удалить список \"{listName}\"?",
                replyMarkup: confirmKeyboard,
                cancellationToken: ct
            );
        }
        else if (data.StartsWith("confirmdel_"))
        {
            var listName = data.Substring("confirmdel_".Length);

            if (storage.DeleteWatchlist(chatId, listName))
            {
                await storage.SaveAsync();
                await bot.SendMessage(chatId, $"Список \"{listName}\" удалён.", cancellationToken: ct);
            }
            else
            {
                await bot.SendMessage(chatId, "Ошибка: список не найден или его нельзя удалить.", cancellationToken: ct);
            }
        }
        else if (data == "cancel_delete")
        {
            await bot.SendMessage(chatId, "Удаление отменено.", cancellationToken: ct);
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

    //private async Task ShowLists(long chatId, CancellationToken ct)
    //{
    //    var user = _storage.TryGetUser(chatId);
    //    if (user is null)
    //    {
    //        await _bot.SendMessage(
    //            chatId,
    //            "ℹ️ Вы ещё не зарегистрированы. Нажмите /start",
    //            cancellationToken: ct);
    //        return;
    //    }

    //    var listsText = user.Lists.Count == 0
    //        ? "(пока нет списков)"
    //        : string.Join("\n", user.Lists.Select(l => $"• {l.Name}"));

    //    await _bot.SendMessage(
    //        chatId,
    //        $"📋 Ваши списки:\n{listsText}",
    //        replyMarkup: Keyboards.BuildUserListsKeyboard(user), // отдельная клавиатура для списков
    //        cancellationToken: ct);
    //}

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"❌ Update error: {ex.Message}\n{ex}");
        return Task.CompletedTask;
    }

    private static async Task HandleDeleteList(ITelegramBotClient bot, MoexWatchlistsBot.Models.User user, CancellationToken ct)
    {
        var lists = user.Lists.Where(w => w.Name != "MyFavorites").ToList();

        if (lists.Count == 0)
        {
            await bot.SendMessage(user.ChatId, "У вас нет списков для удаления.", cancellationToken: ct);
            return;
        }

        // Строим вертикальное меню
        var buttons = lists.Select(w => new[]
        {
        InlineKeyboardButton.WithCallbackData($"🗑 {w.Name}", $"delete_{w.Name}")
    }).ToArray();

        var keyboard = new InlineKeyboardMarkup(buttons);

        await bot.SendMessage(
            user.ChatId,
            "Выберите список для удаления:",
            replyMarkup: keyboard,
            cancellationToken: ct
        );
    }
    private async Task SendProgramInfo(long chatId, CancellationToken ct)
    {
        string info = "📝 MoexWatchlistsBot\nВерсия: 1.0\nАвтор: Anton\n\nС помощью бота вы можете создавать и управлять списками бумаг на MOEX.";
        await _bot.SendMessage(chatId, info, cancellationToken: ct);
    }
}