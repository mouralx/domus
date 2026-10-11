using Domus.Ai;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

public static class SqlStoreChecks
{
    public static async Task Run(string connectionString)
    {
        var schema = "ai_test_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), schema); Directory.CreateDirectory(directory);
        await using var admin = new NpgsqlConnection(connectionString); await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin)) await command.ExecuteNonQueryAsync();
        var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
        var protection = DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(directory, "keys")), options => options.SetApplicationName("Domus.AI"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI_DATA_DIR"] = directory }).Build();
        try
        {
            var actor = Guid.NewGuid(); var workspace = Guid.NewGuid();
            var credential = new ChatGptConnection("test-client", "test", "synthetic@example.test", "synthetic-access", "synthetic-refresh", "synthetic-id", DateTimeOffset.UtcNow.AddHours(1), [], "test-model");
            var legacy = new AiStore(protection, config); await legacy.SaveConnection(actor, credential, Guid.Empty); await legacy.SaveWorkspace(workspace, new(true, "openai")); var host = await legacy.HostId();
            using var sql = new SqlAiStore(scoped, protection); await sql.Initialize(directory);
            if ((await sql.Connection(actor)).Connection?.AccessToken != "synthetic-access" || await sql.HostId() != host || !(await sql.Workspace(workspace)).Enabled) throw new Exception("Legacy SQL import lost credentials/configuration/host ID.");
            var original = await sql.Connection(actor); await sql.SaveConnection(actor, null, original.Revision);
            await sql.Initialize(directory);
            if ((await sql.Connection(actor)).Connection is not null) throw new Exception("Legacy import resurrected disconnected credentials.");
            try { await sql.SaveConnection(actor, credential, original.Revision); throw new Exception("SQL CAS did not reject stale credentials."); } catch (AiError error) when (error.Status == 409) { }
            var disconnected = await sql.Connection(actor); await sql.SaveConnection(actor, credential, disconnected.Revision);
            var current = await sql.Connection(actor);
            var race = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { try { await sql.SaveConnection(actor, credential, current.Revision); return true; } catch (AiError error) when (error.Status == 409) { return false; } }));
            if (race.Count(x => x) != 1) throw new Exception("SQL concurrent refresh CAS is not atomic.");
            var reservations = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => { try { return await sql.Reserve(new(actor, workspace, true, false), "test-model", "test"); } catch (AiError error) when (error.Status == 429) { return null; } }));
            if (reservations.Count(x => x is not null) != 3) throw new Exception("SQL concurrency guard is not atomic.");
            foreach (var row in reservations.Where(x => x is not null)) await sql.Finish(row!.Id, "completed", 2, 3);
            for (var member = 0; member < 11; member++)
            {
                var id = Guid.NewGuid(); await sql.SaveConnection(id, credential, Guid.Empty);
                for (var request = 0; request < 10; request++) { var row = await sql.Reserve(new(id, workspace, true, false), "test-model", "test"); await sql.Finish(row.Id, "completed", 1, 1); }
            }
            if (JsonSerializer.Serialize(await sql.Usage(workspace)).Contains("monthlyRequestLimit")) throw new Exception("SQL introduced a monthly cap.");
            using var restarted = new SqlAiStore(scoped, protection); await restarted.Initialize(directory);
            if ((await restarted.Connection(actor)).Connection is null) throw new Exception("SQL credentials did not survive restart.");
            await using var read = new NpgsqlCommand($"SELECT encrypted FROM {schema}.ai_connections WHERE actor=@actor", admin); read.Parameters.AddWithValue("actor", actor);
            if (((string)(await read.ExecuteScalarAsync())!).Contains("synthetic-access")) throw new Exception("SQL credentials stored in plaintext.");
            Console.WriteLine("PASS: real PostgreSQL AI migration, encryption, restart, CAS, concurrency reservations and >100 requests without monthly caps.");
        }
        finally { NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin); await drop.ExecuteNonQueryAsync(); Directory.Delete(directory, true); }
    }
}
