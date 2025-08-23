using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Services;
using MoexWatchlistsBot.Ui;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Scenarios
{
    public class FindListScenario : IScenario
    {
        public string Name => "FindList";

        private string? _engine;
        private string? _market;
        private string? _board;
        private string? _lastTicker; // последний найденный тикер

        public async Task StartAsync(ITelegramBotClient bot, long chatId, Models.User user, CancellationToken ct)
        {
            var cancelKb = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "❌ Отменить" }
            })
            {
                ResizeKeyboard = true
            };

            var inline = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("📈 TQBR", "find_TQBR"),
                    InlineKeyboardButton.WithCallbackData("💱 CETS", "find_CETS"),
                    InlineKeyboardButton.WithCallbackData("📊 SPBFUT", "find_SPBFUT")
                }
            });

            await bot.SendMessage(chatId,
                "Выберите тип рынка:",
                replyMarkup: inline,
                cancellationToken: ct);
        }

        public async Task HandleMessageAsync(
            ITelegramBotClient bot,
            Message message,
            ScenarioContext context,
            Storage storage,
            CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            var text = message.Text?.Trim() ?? "";

            if (text == "❌ Отменить")
            {
                await bot.SendMessage(chatId,
                    "❎ Поиск отменён.",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);
                context.IsCompleted = true;
                return;
            }

            if (_engine == null || _market == null || _board == null)
            {
                await bot.SendMessage(chatId, "⚠️ Сначала выберите рынок через кнопки.", cancellationToken: ct);
                return;
            }

            // Пользователь ввёл тикер
            var service = new MoexService();
            _lastTicker = text;

            var sec = await service.GetSecurityByTickerAsync(text, _engine, _market, _board);
            var (price, time) = await service.GetLastPriceAsync(text, _engine, _market, _board);

            if (sec == null)
            {
                await bot.SendMessage(chatId, $"❌ Бумага {text} не найдена.", cancellationToken: ct);
                return;
            }

            // показываем информацию
            await bot.SendMessage(chatId,
                $"📈 {sec.SecId} ({sec.ShortName})\nЦена: {price}\nВремя: {time}",
                cancellationToken: ct);

            // клавиатура "добавить/отменить"
            var actionsKb = new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData("➕ Добавить в список", "add_to_list"),
                    InlineKeyboardButton.WithCallbackData("❌ Отменить", "cancel_find")
                }
            });

            await bot.SendMessage(chatId, "Что дальше?", replyMarkup: actionsKb, cancellationToken: ct);
        }

        public async Task HandleCallbackAsync(
            ITelegramBotClient bot,
            CallbackQuery query,
            ScenarioContext context,
            Storage storage,
            CancellationToken ct)
        {
            var chatId = query.Message!.Chat.Id;
            var data = query.Data ?? "";

            if (data.StartsWith("find_"))
            {
                switch (data)
                {
                    case "find_TQBR":
                        _engine = "stock"; _market = "shares"; _board = "TQBR";
                        break;
                    case "find_CETS":
                        _engine = "currency"; _market = "selt"; _board = "CETS";
                        break;
                    case "find_SPBFUT":
                        _engine = "futures"; _market = "forts"; _board = "SPBFUT";
                        break;
                }

                await bot.SendMessage(chatId, "✍️ Введите тикер бумаги:", cancellationToken: ct);
                return;
            }

            if (data == "cancel_find")
            {
                await bot.SendMessage(chatId,
                    "❎ Поиск отменён.",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);
                context.IsCompleted = true;
                return;
            }

            if (data == "add_to_list")
            {
                var user = storage.TryGetUser(chatId);
                if (user == null)
                {
                    await bot.SendMessage(chatId, "⚠️ Вы не зарегистрированы.", cancellationToken: ct);
                    context.IsCompleted = true;
                    return;
                }

                if (string.IsNullOrEmpty(_lastTicker))
                {
                    await bot.SendMessage(chatId, "⚠️ Сначала найдите бумагу.", cancellationToken: ct);
                    return;
                }

                // показать списки
                var inline = new InlineKeyboardMarkup(
                    user.Lists.Select(l => new[]
                    {
                        InlineKeyboardButton.WithCallbackData(l.Name, $"addtolist_{l.Name}")
                    }).ToArray()
                );

                await bot.SendMessage(chatId, "Выберите список для добавления:", replyMarkup: inline, cancellationToken: ct);
                return;
            }

            if (data.StartsWith("addtolist_"))
            {
                var listName = data.Substring("addtolist_".Length);
                var user = storage.TryGetUser(chatId);

                if (user == null)
                {
                    await bot.SendMessage(chatId, "⚠️ Пользователь не найден.", cancellationToken: ct);
                    return;
                }

                var list = user.Lists.FirstOrDefault(l => l.Name == listName);
                if (list == null)
                {
                    await bot.SendMessage(chatId, $"⚠️ Список {listName} не найден.", cancellationToken: ct);
                    return;
                }

                // создаём тикер
                var item = new TickerItem
                {
                    Ticker = _lastTicker!,
                    Engine = _engine!,
                    Market = _market!,
                    Board = _board!
                };

                list.Items.Add(item);
                await storage.SaveAsync();

                await bot.SendMessage(chatId,
                    $"✅ Бумага {_lastTicker} добавлена в список {list.Name}.",
                    replyMarkup: Keyboards.BuildMainMenuKeyboard(),
                    cancellationToken: ct);

                context.IsCompleted = true;
                return;
            }
        }
    }
}