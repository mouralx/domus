using Npgsql;

namespace Automations.Core;

public sealed record PlatformGitHubSettings(string? Repository, string? EncryptedToken);
public sealed record LegacyGitHubSettings(Guid Scope, string? Repository, string? EncryptedToken);
public static class PlatformGitHub
{
    public static async Task<PlatformGitHubSettings?> Read(Database database)
    {
        await using var cmd = database.Source.CreateCommand("SELECT repository,encrypted_token FROM platform_github_settings WHERE singleton=true");
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new(reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)) : null;
    }
    public static async Task Save(Database database, string? repository, string? encrypted)
    {
        await using var cmd = database.Source.CreateCommand("""
            INSERT INTO platform_github_settings(singleton,repository,encrypted_token) VALUES (true,$1,$2)
            ON CONFLICT(singleton) DO UPDATE SET repository=COALESCE(excluded.repository,platform_github_settings.repository),encrypted_token=COALESCE(excluded.encrypted_token,platform_github_settings.encrypted_token)
            """);
        cmd.Parameters.Add(new() { Value = (object?)repository ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
        cmd.Parameters.Add(new() { Value = (object?)encrypted ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
        await cmd.ExecuteNonQueryAsync();
    }
    public static async Task RemoveToken(Database database)
    {
        await using var cmd = database.Source.CreateCommand("UPDATE platform_github_settings SET encrypted_token=NULL WHERE singleton=true"); await cmd.ExecuteNonQueryAsync();
    }
    public static async Task<IReadOnlyList<LegacyGitHubSettings>> Legacy(Database database)
    {
        await using var cmd = database.Source.CreateCommand("SELECT COALESCE(p.user_id,c.user_id),p.repository,c.encrypted_token FROM github_preferences p FULL JOIN github_credentials c ON p.user_id=c.user_id");
        await using var reader = await cmd.ExecuteReaderAsync(); var settings = new List<LegacyGitHubSettings>();
        while (await reader.ReadAsync()) settings.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return settings;
    }
    public static async Task<bool> Migrate(Database database, string? repository, string? token)
    {
        await using var cmd = database.Source.CreateCommand("INSERT INTO platform_github_settings(singleton,repository,encrypted_token) VALUES (true,$1,$2) ON CONFLICT DO NOTHING");
        cmd.Parameters.Add(new() { Value = (object?)repository ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
        cmd.Parameters.Add(new() { Value = (object?)token ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text });
        return await cmd.ExecuteNonQueryAsync() == 1;
    }
}
