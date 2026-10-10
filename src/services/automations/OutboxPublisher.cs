using Automations.Core;
using Npgsql;

namespace Automations.Api;

public sealed class OutboxPublisher(Database database, JobQueue queue, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await database.Source.OpenConnectionAsync(stoppingToken);
                await using var transaction = await connection.BeginTransactionAsync(stoppingToken);
                await using var find = new NpgsqlCommand("SELECT run_id FROM execution_outbox WHERE published_at IS NULL ORDER BY run_id FOR UPDATE SKIP LOCKED LIMIT 1", connection, transaction);
                if (await find.ExecuteScalarAsync(stoppingToken) is Guid id)
                {
                    await queue.Publish(id, stoppingToken);
                    await using var update = new NpgsqlCommand("UPDATE execution_outbox SET published_at=now() WHERE run_id=$1", connection, transaction); update.Parameters.AddWithValue(id); await update.ExecuteNonQueryAsync(stoppingToken);
                }
                await transaction.CommitAsync(stoppingToken);
                await using var installationTx = await connection.BeginTransactionAsync(stoppingToken);
                await using var findInstallation = new NpgsqlCommand("SELECT job_id FROM installation_outbox WHERE published_at IS NULL ORDER BY job_id FOR UPDATE SKIP LOCKED LIMIT 1", connection, installationTx);
                if (await findInstallation.ExecuteScalarAsync(stoppingToken) is Guid installationId)
                {
                    await queue.Publish(installationId, stoppingToken, installation: true);
                    await using var mark = new NpgsqlCommand("UPDATE installation_outbox SET published_at=now() WHERE job_id=$1", connection, installationTx); mark.Parameters.AddWithValue(installationId); await mark.ExecuteNonQueryAsync(stoppingToken);
                }
                await installationTx.CommitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Outbox publication will retry: {Type}", error.GetType().Name); }
            await Task.Delay(500, stoppingToken);
        }
    }
}
