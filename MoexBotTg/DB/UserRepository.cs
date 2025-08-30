using Npgsql;

public class UserRepository
{
    private readonly string _connString;

    public UserRepository(string connString)
    {
        _connString = connString;
    }

    public async Task UpsertUserAsync(
        long chatId,
        string? username,
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync(ct);

        const string sql = @"
        insert into tg_users (chat_id, username)
        values (@chat_id, @username)
        on conflict (chat_id)
        do update set
            username    = excluded.username,
            last_active = now();";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("chat_id", chatId);
        cmd.Parameters.AddWithValue("username", (object?)username ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}