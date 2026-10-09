using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUPPORT.Infrastructure.Persistence;
using SUPPORT.Infrastructure.Settings;

namespace SUPPORT.Infrastructure.Services;

/// <summary>Once a day, deletes conversations inactive for longer than the retention period (messages and feedback cascade).</summary>
/// <param name="contextFactory">Context factory.</param>
/// <param name="settings">Knowledge settings (retention days).</param>
/// <param name="logger">Logger.</param>
internal sealed class ConversationRetentionService(
    IDbContextFactory<SupportDbContext> contextFactory,
    IOptions<KnowledgeSettings> settings,
    ILogger<ConversationRetentionService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-settings.Value.ConversationRetentionDays);
                await using var db = await contextFactory.CreateDbContextAsync(stoppingToken);
                var deleted = await db.Conversations.Where(c => c.LastMessageAt < cutoff).ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Retention removed {Count} inactive conversations", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Conversation retention cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
