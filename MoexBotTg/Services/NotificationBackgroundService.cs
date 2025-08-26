using MoexWatchlistsBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace MoexWatchlistsBot.Services
{
    public class NotificationBackgroundService
    {
        private readonly ITelegramBotClient _bot;
        private readonly Storage _storage;
        private readonly CancellationToken _ct;

        public NotificationBackgroundService(ITelegramBotClient bot, Storage storage, CancellationToken ct)
        {
            _bot = bot;
            _storage = storage;
            _ct = ct;
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                while (!_ct.IsCancellationRequested)
                {
                    try
                    {
                        var now = DateTime.Now.ToString("HH:mm");
                        var users = _storage.GetAllUsers();

                        foreach (var user in users)
                        {
                            foreach (var list in user.Lists.Where(l => l.Name != "MyFavorites"))
                            {
                                if (list.Name == now)
                                {
                                    foreach (var item in list.Items)
                                    {
                                        await SendTickerInfoAsync(user.ChatId, item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Notification Error] {ex.Message}");
                    }
                    Console.WriteLine($"Degug:Try to find tikers in timeLists in {DateTime.Now}");
                    await Task.Delay(TimeSpan.FromMinutes(1), _ct);
                }
            }, _ct);
        }

        public async Task SendTickerInfoAsync(long chatId, TickerItem item)
        {
            var service = new MoexService();
            var sec = await service.GetSecurityByTickerAsync(item.Ticker, item.Engine, item.Market, item.Board);
            var (price, time) = await service.GetLastPriceAsync(item.Ticker, item.Engine, item.Market, item.Board);

            if (sec == null)
            {
                await _bot.SendMessage(chatId, $"❌ Бумага {item.Ticker} не найдена на MOEX.", cancellationToken: _ct);
                return;
            }

            if (item.BuyAmount.HasValue && item.BuyAmount.Value > 0 && item.BuyRate.HasValue)
            {
                var currentValue = item.BuyAmount.Value * price;
                var pnl = (item.BuyAmount.Value * price) - (item.BuyAmount.Value * item.BuyRate.Value);

                await _bot.SendMessage(chatId,
                    $"📈 {sec.SecId} ({sec.ShortName})\n" +
                    $"Цена: {price}\n" +
                    $"Время: {time}\n" +
                    $"В позиции штук: {item.BuyAmount}\n" +
                    $"Цена покупки: {item.BuyRate}\n" +
                    $"Текущая рыночная стоимость: {currentValue}\n" +
                    $"Примерный PNL: {pnl}\n",
                    cancellationToken: _ct);
            }
            else
            {
                await _bot.SendMessage(chatId,
                    $"📈 {sec.SecId} ({sec.ShortName})\n" +
                    $"Цена: {price}\n" +
                    $"Время: {time}",
                    cancellationToken: _ct);
            }
        }
    }
}
