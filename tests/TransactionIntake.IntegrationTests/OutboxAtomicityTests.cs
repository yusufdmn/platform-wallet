using FluentAssertions;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;
using PlatformWallet.TransactionIntake.Infrastructure;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PlatformWallet.TransactionIntake.IntegrationTests;

[Trait("Category", "Integration")]
public class OutboxAtomicityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("intake_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync()    => await _postgres.DisposeAsync();

    [Fact]
    public async Task Transaction_insert_and_outbox_message_commit_atomically()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSTGRES_HOST"]     = _postgres.Hostname,
                ["POSTGRES_PORT"]     = _postgres.GetMappedPublicPort(5432).ToString(),
                ["POSTGRES_USER"]     = "postgres",
                ["POSTGRES_PASSWORD"] = "postgres",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntakeInfrastructure(cfg);

        services.AddValidatorsFromAssemblyContaining<SubmitMintValidator>();
        services.AddMediatR(c =>
        {
            c.RegisterServicesFromAssemblyContaining<SubmitMintHandler>();
            c.AddOpenBehavior(typeof(PlatformWallet.TransactionIntake.Application.Behaviours.ValidationBehaviour<,>));
        });

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<IntakeDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });
            x.UsingInMemory((ctx, cfg2) => cfg2.ConfigureEndpoints(ctx));
        });

        await using var sp = services.BuildServiceProvider();

        // Apply migrations
        await using (var scope = sp.CreateAsyncScope())
        {
            var dbCtx = scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
            await dbCtx.Database.MigrateAsync();
        }

        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new SubmitMintCommand(
            CreditAccountId: Guid.NewGuid(),
            Amount:          100m,
            Asset:           "USD",
            IdempotencyKey:  Guid.NewGuid().ToString()));

        result.WasDuplicate.Should().BeFalse();

        await using var verifyScope = sp.CreateAsyncScope();
        var ctx2 = verifyScope.ServiceProvider.GetRequiredService<IntakeDbContext>();

        var txCount = await ctx2.Transactions.CountAsync();
        txCount.Should().Be(1, "transaction row must be committed");

        // Outbox messages are written to OutboxMessage table in same transaction (raw SQL avoids MassTransit internal type ref)
        var outboxCount = await ctx2.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"OutboxMessage\"")
            .FirstAsync();
        outboxCount.Should().BeGreaterThan(0, "at least one outbox message must exist alongside the transaction row");
    }
}
