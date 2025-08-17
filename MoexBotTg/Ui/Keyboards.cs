using MoexWatchlistsBot.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace MoexWatchlistsBot.Ui;

public static class Keyboards
{
    public static ReplyKeyboardMarkup BuildMainMenuKeyboard()
    {
        var rows = new List<KeyboardButton[]>
        {
            new[] { new KeyboardButton("🔍 Поиск бумаги") },
            new[] { new KeyboardButton("📋 Мои списки") },
            new[] { new KeyboardButton("ℹ️ Информация о программе") }
        };

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }

    // Старая клавиатура со списками
    public static ReplyKeyboardMarkup BuildUserListsKeyboard(MoexWatchlistsBot.Models.User user)
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
        rows.Add(new[] { new KeyboardButton("🗑 Удалить список") });
        rows.Add(new[] { new KeyboardButton("Вернуться") });

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };
    }
}