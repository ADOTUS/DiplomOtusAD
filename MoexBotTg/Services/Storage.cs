using System.Text.Json;
using System.Text.Json.Serialization;
using MoexWatchlistsBot.Models;

namespace MoexWatchlistsBot.Services;

public class Storage
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<long, AppUser> _users = new();

    public Storage(string path) => _path = Path.GetFullPath(path);

    public async Task LoadAsync()
    {
        if (!File.Exists(_path)) return;
        await using var fs = File.OpenRead(_path);
        var data = await JsonSerializer.DeserializeAsync<List<AppUser>>(fs, _jsonOptions) ?? new List<AppUser>();
        _users.Clear();
        foreach (var u in data) _users[u.ChatId] = u;
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var list = _users.Values.OrderBy(u => u.ChatId).ToList();
        await using var fs = File.Create(_path);
        await JsonSerializer.SerializeAsync(fs, list, _jsonOptions);
    }

    public AppUser GetOrCreateUser(long chatId, string? username)
    {
        if (!_users.TryGetValue(chatId, out var user))
        {
            user = new AppUser { ChatId = chatId, Username = username };
            _users[chatId] = user;
        }
        else
        {
            if (!string.Equals(user.Username, username, StringComparison.Ordinal))
                user.Username = username;
        }

        return user;
    }

    public AppUser? TryGetUser(long chatId) => _users.TryGetValue(chatId, out var user) ? user : null;
}