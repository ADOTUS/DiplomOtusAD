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
    public class DeleteListScenario : IScenario
    {
        public string Name => "DeleteList";

        public async Task StartAsync(ITelegramBotClient bot, long chatId, Models.User user, CancellationToken ct)
        {
            var deletable = user.Lists
                .Where(l => !string.Equals(l.Name, "MyFavorites", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (deletable.Count == 0)
            {
                await bot.SendMessage(
                    chatId,
                    "У вас нет списков для удаления.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct
                );
                return;
            }

            // Inline-кнопки со списками для удаления
            var inline = new InlineKeyboardMarkup(
                deletable.Select(l => new[]
                {
                    InlineKeyboardButton.WithCallbackData($"🗑 {l.Name}", $"delete_{l.Name}")
                }).ToArray()
            );

            // Reply-клавиатура с единственной кнопкой "Отменить"
            var cancelKb = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "❌ Отменить" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            // Отправляем списки + выводим снизу клавиатуру отмены
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
            var user = storage.GetOrCreateUser(chatId, message.From?.Username);

            // Нажали "Отменить" (reply-кнопка)
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

            // Разрешаем вводить название списка текстом
            var deletable = user.Lists
                .Where(l => !string.Equals(l.Name, "MyFavorites", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var target = deletable.FirstOrDefault(l => string.Equals(l.Name, text, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                var confirmKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("✅ Да", $"confirmdel_{target.Name}"),
                        InlineKeyboardButton.WithCallbackData("❌ Нет", "cancel_delete")
                    }
                });

                await bot.SendMessage(
                    chatId,
                    $"Вы уверены, что хотите удалить список \"{target.Name}\"?",
                    replyMarkup: confirmKeyboard,
                    cancellationToken: ct
                );
            }
        }

        public async Task HandleCallbackAsync(
            ITelegramBotClient bot,
            CallbackQuery callbackQuery,
            Storage storage,
            ScenarioContext context,
            CancellationToken ct)
        {
            if (callbackQuery.Data == null || callbackQuery.Message == null)
                return;

            var chatId = callbackQuery.Message.Chat.Id;
            var user = storage.GetOrCreateUser(chatId, callbackQuery.From?.Username);
            var data = callbackQuery.Data;

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

            if (data.StartsWith("confirmdel_"))
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

                    context.IsCompleted = true;
                }
                else
                {
                    await bot.SendMessage(
                        chatId,
                        "Ошибка: список не найден или его нельзя удалить.",
                        cancellationToken: ct
                    );
                }

                return;
            }

            if (data == "cancel_delete")
            {
                await bot.SendMessage(
                    chatId,
                    "❎ Удаление отменено.",
                    replyMarkup: Keyboards.BuildUserListsKeyboard(user),
                    cancellationToken: ct
                );

                context.IsCompleted = true;
            }
        }
    }
}