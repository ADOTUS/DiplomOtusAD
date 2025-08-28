using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Services;
using MoexWatchlistsBot.Ui;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Scenarios
{
    public class DeleteListScenario : IScenario, IScenarioWithCallback
    {
        public string Name => "DeleteList";

        public async Task StartAsync(ITelegramBotClient bot, long chatId, Models.User user, ScenarioContext context, CancellationToken ct)
        {
            var lists = user.Lists.Where(l => !l.IsDefault).ToList();
            if (lists.Count == 0)
            {
                await bot.SendMessage(chatId,
                    "📭 У вас нет списков для удаления.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct);
                return;
            }

            var inline = new InlineKeyboardMarkup(
                lists.Select(l => new[]
                {
                    InlineKeyboardButton.WithCallbackData($"🗑 {l.Name}", $"delete_{l.Name}")
                }).ToArray()
            );

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
                "Выберите список для удаления (или отмените):",
                replyMarkup: inline,
                cancellationToken: ct
            );

            await bot.SendMessage(
                chatId,
                "Для отмены нажмите кнопку ниже:",
                replyMarkup: cancelKb,
                cancellationToken: ct
            );
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
            var user = storage.TryGetUser(chatId);
            
            if (text == "❌ Отменить")
            {
                await bot.SendMessage(
                    chatId,
                    "❎ Удаление списка отменено.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct
                );

                context.IsCompleted = true;
                return;
            }

        } 

        public async Task HandleCallbackAsync(
            ITelegramBotClient bot,
            CallbackQuery query,
            ScenarioContext ctx,
            Storage storage,
            CancellationToken ct)
        {

            var chatId = query.Message.Chat.Id;
            var user = storage.TryGetUser(chatId);
            var data = query.Data ?? "";
            if (user == null) return;

            if (data.StartsWith("delete_"))
            {
                var listName = data.Substring("delete_".Length);

                var confirmKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
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
                return;
            }

            else if (data.StartsWith("confirmdel_"))
            {
                var listName = data.Substring("confirmdel_".Length);

                if (storage.DeleteWatchlist(chatId, listName))
                {
                    await storage.SaveAsync();

                    await bot.SendMessage(
                        chatId,
                        $"✅ Список \"{listName}\" удалён.",
                        replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                        cancellationToken: ct
                    );

                    ctx.IsCompleted = true;
                }
            }
            else if (data == "cancel_delete")
            {
                await bot.SendMessage(chatId,
                    "❎ Удаление отменено.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct);

                ctx.IsCompleted = true;
            }

        }
    }
}