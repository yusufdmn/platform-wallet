using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.ApiGateway.Yarp.Infrastructure.Rabbit;

namespace PlatformWallet.ApiGateway.Yarp.Endpoints;

public static class DlqAdminEndpoints
{
    private const int DefaultPeekCount = 25;
    private const int MaxPeekCount     = 200;
    private const int MaxReplayAll     = 500;

    public static void MapDlqAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/dlq").RequireAuthorization("LedgerAdmin");

        group.MapGet("/", ListDlqAsync);
        group.MapGet("/{queue}", PeekAsync);
        group.MapPost("/{queue}/replay-one", ReplayOneAsync);
        group.MapPost("/{queue}/replay-all", ReplayAllAsync);
    }

    private static async Task<IResult> ListDlqAsync(
        IRabbitMqManagementClient client,
        CancellationToken         ct)
    {
        try
        {
            var queues = await client.ListDlqAsync(ct);
            return Results.Ok(queues);
        }
        catch (HttpRequestException ex)
        {
            return ManagementUnavailable(ex);
        }
    }

    private static async Task<IResult> PeekAsync(
        string                    queue,
        [FromQuery] int?          take,
        IRabbitMqManagementClient client,
        CancellationToken         ct)
    {
        var count = ClampPeekCount(take);
        try
        {
            var messages = await client.PeekAsync(queue, count, ct);
            return Results.Ok(messages);
        }
        catch (HttpRequestException ex)
        {
            return ManagementUnavailable(ex);
        }
    }

    private static async Task<IResult> ReplayOneAsync(
        string                    queue,
        IRabbitMqManagementClient client,
        CancellationToken         ct)
    {
        try
        {
            var message = await client.DrainOneAsync(queue, ct);
            if (message is null)
            {
                return Results.NoContent();
            }

            await client.PublishAsync(queue, message, ct);
            return Results.Ok(new { replayed = 1 });
        }
        catch (HttpRequestException ex)
        {
            return ManagementUnavailable(ex);
        }
    }

    private static async Task<IResult> ReplayAllAsync(
        string                    queue,
        [FromQuery] bool          confirm,
        IRabbitMqManagementClient client,
        CancellationToken         ct)
    {
        if (!confirm)
        {
            return Results.BadRequest(new
            {
                error = "Add ?confirm=true to drain the entire queue.",
            });
        }

        var replayed = 0;
        try
        {
            for (var i = 0; i < MaxReplayAll; i++)
            {
                var message = await client.DrainOneAsync(queue, ct);
                if (message is null)
                {
                    return Results.Ok(new { replayed, remaining = 0 });
                }

                await client.PublishAsync(queue, message, ct);
                replayed++;
            }

            return Results.Json(
                new { replayed, remaining = -1, error = $"replay-all capped at {MaxReplayAll} per call" },
                statusCode: StatusCodes.Status207MultiStatus);
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(
                new { replayed, error = ex.Message },
                statusCode: StatusCodes.Status207MultiStatus);
        }
    }

    private static int ClampPeekCount(int? take) =>
        take is null or <= 0 ? DefaultPeekCount
        : take > MaxPeekCount ? MaxPeekCount
        : take.Value;

    private static IResult ManagementUnavailable(HttpRequestException ex) =>
        Results.Problem(
            title:      "RabbitMQ Management API unavailable",
            detail:     ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
