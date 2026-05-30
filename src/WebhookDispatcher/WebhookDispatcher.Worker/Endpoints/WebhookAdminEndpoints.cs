using System.Diagnostics;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlatformWallet.WebhookDispatcher.Application.Services;
using PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

namespace PlatformWallet.WebhookDispatcher.Worker.Endpoints;

public static class WebhookAdminEndpoints
{
    private const string AdminPolicy        = "ledger:admin";
    private const int    DefaultTake        = 25;
    private const int    MaxTake            = 100;
    private const int    MinIntervalMs      = 100;
    private const int    MaxIntervalMs      = 60_000;
    private const int    DefaultIntervalMs  = 1_000;
    private const int    ReplayAllMaxBatch  = 500;

    public static void MapWebhookAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/webhooks").RequireAuthorization(AdminPolicy);

        group.MapGet ("/failed",          ListFailedAsync);
        group.MapPost("/{id:long}/retry", RetryOneAsync);
        group.MapPost("/replay-all",      ReplayAllAsync);
    }

    private static async Task<IResult> ListFailedAsync(
        [FromQuery] string?       status,
        [FromQuery] int?          take,
        [FromQuery] int?          skip,
        IWebhookConnectionFactory conns,
        CancellationToken         ct)
    {
        var limit  = ClampTake(take);
        var offset = skip is > 0 ? skip.Value : 0;

        var filter = ParseStatusFilter(status);

        const string Sql = """
            SELECT "Id"                   AS Id,
                   "EventType"            AS EventType,
                   "CorrelationId"        AS CorrelationId,
                   "Reason"               AS Reason,
                   "LastHttpStatusCode"   AS LastHttpStatusCode,
                   "Status"               AS Status,
                   "RetryCount"           AS RetryCount,
                   "FailedAt"             AS FailedAt,
                   "RetriedAt"            AS RetriedAt
            FROM   failed_webhook_deliveries
            WHERE  (@status IS NULL OR "Status" = @status)
            ORDER  BY "FailedAt" DESC
            LIMIT  @limit
            OFFSET @offset;
            """;

        using var conn = conns.Create();
        var rows = await conn.QueryAsync<FailedRow>(
            new CommandDefinition(Sql, new { status = filter, limit, offset }, cancellationToken: ct));

        return Results.Ok(new { items = rows, take = limit, skip = offset });
    }

    private static async Task<IResult> RetryOneAsync(
        long                    id,
        WebhookDbContext        db,
        IWebhookDeliveryService delivery,
        CancellationToken       ct)
    {
        var row = await db.FailedDeliveries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return Results.NotFound();
        }

        await RetryAsync(row, db, delivery, ct);

        return Results.Ok(new
        {
            id          = row.Id,
            status      = row.Status.ToString(),
            retry_count = row.RetryCount,
        });
    }

    private static async Task<IResult> ReplayAllAsync(
        [FromQuery] bool          confirm,
        [FromQuery] int?          intervalMs,
        [FromQuery] int?          max,
        IServiceProvider          sp,
        IWebhookConnectionFactory conns,
        CancellationToken         ct)
    {
        if (!confirm)
        {
            return Results.BadRequest(new { error = "Add ?confirm=true to replay every failed delivery." });
        }

        var interval = ClampInterval(intervalMs);
        var limit    = ClampReplayMax(max);

        const string Sql = """
            SELECT "Id"
            FROM   failed_webhook_deliveries
            WHERE  "Status" = 'Failed'
            ORDER  BY "FailedAt" ASC
            LIMIT  @limit;
            """;

        long[] ids;
        using (var conn = conns.Create())
        {
            ids = (await conn.QueryAsync<long>(
                new CommandDefinition(Sql, new { limit }, cancellationToken: ct))).ToArray();
        }

        var sw        = Stopwatch.StartNew();
        var delivered = 0;
        var failed    = 0;

        for (var i = 0; i < ids.Length; i++)
        {
            if (i > 0)
            {
                await Task.Delay(interval, ct);
            }

            await using var scope = sp.CreateAsyncScope();
            var db       = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
            var delivery = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

            var id  = ids[i];
            var row = await db.FailedDeliveries.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null)
            {
                failed++;
                continue;
            }

            await RetryAsync(row, db, delivery, ct);
            if (row.Status == FailedDeliveryStatus.Delivered)
            {
                delivered++;
            }
            else
            {
                failed++;
            }
        }

        sw.Stop();

        return Results.Ok(new
        {
            attempted  = ids.Length,
            delivered,
            failed,
            durationMs = sw.ElapsedMilliseconds,
            intervalMs = interval,
        });
    }

    private static async Task RetryAsync(
        FailedWebhookDelivery   row,
        WebhookDbContext        db,
        IWebhookDeliveryService delivery,
        CancellationToken       ct)
    {
        row.MarkRetrying();
        await db.SaveChangesAsync(ct);

        try
        {
            await delivery.DeliverAsync(row.EventType, row.CorrelationId, ct);
            row.MarkDelivered(DateTimeOffset.UtcNow);
        }
        catch (WebhookDeliveryException ex)
        {
            row.RecordRetryFailure(DateTimeOffset.UtcNow, ex.Message, ex.StatusCode, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            row.RecordRetryFailure(DateTimeOffset.UtcNow, ex.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    private static int ClampTake(int? take) =>
        take is null or <= 0 ? DefaultTake
        : take > MaxTake     ? MaxTake
        : take.Value;

    private static int ClampInterval(int? interval) =>
        interval is null            ? DefaultIntervalMs
        : interval < MinIntervalMs  ? MinIntervalMs
        : interval > MaxIntervalMs  ? MaxIntervalMs
        : interval.Value;

    private static int ClampReplayMax(int? max) =>
        max is null or <= 0        ? ReplayAllMaxBatch
        : max > ReplayAllMaxBatch  ? ReplayAllMaxBatch
        : max.Value;

    private static string? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return Enum.TryParse<FailedDeliveryStatus>(status, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : status;
    }

    private sealed record FailedRow(
        long           Id,
        string         EventType,
        Guid           CorrelationId,
        string         Reason,
        int?           LastHttpStatusCode,
        string         Status,
        int            RetryCount,
        DateTimeOffset FailedAt,
        DateTimeOffset? RetriedAt);
}
