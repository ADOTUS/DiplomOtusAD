using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Services;
using MoexWatchlistsBot.Ui;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Scenarios
{
    public class AddListScenario : IScenario
    {
        public string Name => "AddList";

        private static readonly Regex TimeRegex = new(@"^([01]?\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);

        public async Task StartAsync(ITelegramBotClient bot
            , long chatId
            , Models.User user
            , ScenarioContext context
            , CancellationToken ct)
        {
            var cancelKb = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "❌ Отменить" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            await bot.SendMessage(
                            chatId,
                            "📝 Введите название нового списка в формате ЧЧ:ММ:",
                            replyMarkup: cancelKb,
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
            var text = message.Text?.Trim() ?? string.Empty;

            var user = storage.GetOrCreateUser(chatId, message.From?.Username);

            if (text == "❌ Отменить")
            {
                await bot.SendMessage(chatId,
                    "❎ Создание списка отменено.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct);

                context.IsCompleted = true;
                return;
            }

            if (!TimeRegex.IsMatch(text))
            {
                await bot.SendMessage(chatId,
                    "⏰ Неверный формат. Введите время в формате ЧЧ:ММ, например 10:30.",
                    cancellationToken: ct);
                return;
            }

            if (user.Lists.Any(l => string.Equals(l.Name, text, StringComparison.OrdinalIgnoreCase)))
            {
                await bot.SendMessage(
                    chatId,
                    "⚠️ Список с таким временем уже существует. Введите другое время:",
                    cancellationToken: ct);
                return;
            }

            user.Lists.Add(new BrokerList { Name = text });
            await storage.SaveAsync();

            await bot.SendMessage(
                            chatId,
                            $"✅ Список \"{text}\" создан.",
                            replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                            cancellationToken: ct);

            var listsText = user.Lists.Count == 0
                            ? "(пока нет списков)"
                            : string.Join("\n", user.Lists.Select(l => $"• {l.Name}"));

            await bot.SendMessage(
                chatId,
                $"📋 Ваши списки:\n{listsText}",
                cancellationToken: ct);

            context.IsCompleted = true;
        }
    }
}