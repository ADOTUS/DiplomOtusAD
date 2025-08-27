using MoexWatchlistsBot.Models;
using MoexWatchlistsBot.Services;
using MoexWatchlistsBot.Ui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Scenarios
{
    public class AnalyticsScenario : IScenario, IScenarioWithCallback
    {
        public string Name => "Analytics";

        public async Task StartAsync(
            ITelegramBotClient bot,
            long chatId,
            Models.User user,
            ScenarioContext context,
            CancellationToken ct)
        {
            // Начинаем с шага 0 — ждём дату начала
            context.Step = 0;

            await bot.SendMessage(chatId,
                "📅 Введите дату начала периода (ГГГГ-ММ-ДД):",
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

            if (context.Step == 0)
            {
                if (!DateTime.TryParse(text, out var startDate))
                {
                    await bot.SendMessage(chatId,
                        "❌ Неверный формат даты. Введите снова (ГГГГ-ММ-ДД):",
                        cancellationToken: ct);
                    return;
                }

                context.Data["startDate"] = startDate.ToString("yyyy-MM-dd");
                context.Step = 1;

                await bot.SendMessage(chatId,
                    "📅 Теперь введите дату конца периода (ГГГГ-ММ-ДД):",
                    cancellationToken: ct);

                return;
            }

            if (context.Step == 1)
            {
                if (!DateTime.TryParse(text, out var endDate))
                {
                    await bot.SendMessage(chatId,
                        "❌ Неверный формат даты. Введите снова (ГГГГ-ММ-ДД):",
                        cancellationToken: ct);
                    return;
                }

                context.Data["endDate"] = endDate.ToString("yyyy-MM-dd");

                var startDate = DateTime.Parse(context.Data["startDate"]);
                var daysDiff = (endDate - startDate).TotalDays;

                var buttons = new List<InlineKeyboardButton[]>
                    {
                        new [] { InlineKeyboardButton.WithCallbackData("1 мин", "interval_1") },
                        new [] { InlineKeyboardButton.WithCallbackData("10 мин", "interval_10") },
                        new [] { InlineKeyboardButton.WithCallbackData("1 час", "interval_60") },
                        new [] { InlineKeyboardButton.WithCallbackData("1 день", "interval_24") }
                    };

                if (daysDiff >= 7)
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("1 неделя", "interval_7") });
                                       
                }

                if (daysDiff >= 31)
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("1 месяц", "interval_31") });
                }

                var kb = new InlineKeyboardMarkup(buttons);

                context.Step = 2; 

                await bot.SendMessage(chatId,
                    "⏱ Выберите интервал свечей:",
                    replyMarkup: kb,
                    cancellationToken: ct);
            }
        }

        public async Task HandleCallbackAsync(
            ITelegramBotClient bot,
            CallbackQuery callbackQuery,
            ScenarioContext context,
            Storage storage,
            CancellationToken ct)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var data = callbackQuery.Data ?? "";

            Console.WriteLine($"{context.Step} мы тут");

            if (context.Step == 2 && data.StartsWith("interval_"))
            {
                var intervalStr = data.Substring("interval_".Length);
                if (!int.TryParse(intervalStr, out var interval))
                {
                    await bot.SendMessage(chatId,
                        "❌ Ошибка: неверный интервал.",
                        cancellationToken: ct);
                    return;
                }

                context.Data["interval"] = intervalStr;

                Console.WriteLine("Debug contex.data потом удалить");
                foreach (var kvp in context.Data)
                {
                    Console.WriteLine($"{kvp.Key} = {kvp.Value}");
                }

                var startDate = DateTime.Parse(context.Data["startDate"]);
                var endDate = DateTime.Parse(context.Data["endDate"]);

                var ticker  = context.Data["Ticker"]; 
                var engine  = context.Data["Engine"];
                var market  = context.Data["Market"];
                var board   = context.Data["Board"];

                var service = new MoexService();

                var reportmsg = await service.GetCandleAnalyticsAsync(
                    ticker, engine, market, interval, startDate, endDate);

                var msg = $"📊 Анализ {reportmsg.SecId} за {reportmsg.PeriodDescription} за интервал {interval}:\n" +
                          $"- Текущее закрытие: {reportmsg.CurrentClose}\n" +
                          $"- Рост/падение за последнюю свечу: {reportmsg.ChangeDay:F2}%\n" +
                          $"- Общий рост/падение за весь период: {reportmsg.ChangePeriod:F2}%\n" +
                          $"- Диапазон: {reportmsg.Min} – {reportmsg.Max}\n" +
                          $"- Общий объём: {reportmsg.TotalVolume:N0}";

                await bot.SendMessage(chatId, msg, cancellationToken: ct);

                context.IsCompleted = true;
            }
        }
    }
}
