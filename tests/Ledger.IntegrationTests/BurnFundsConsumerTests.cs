using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Consumers;
using PlatformWallet.Ledger.Domain;
using PlatformWallet.Ledger.Infrastructure.Persistence;
using PlatformWallet.Ledger.IntegrationTests.Fixtures;
using Xunit;

namespace PlatformWallet.Ledger.IntegrationTests;

[Collection(LedgerIntegrationGroup.Name)]
public class BurnFundsConsumerTests(LedgerIntegrationFixture fixture)
{
    [Fact]
    public async Task Consume_burns_funds_and_creates_two_postings()
    {
        var accountId     = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        const decimal seedBalance = 500m;
        const decimal burnAmount  = 200m;
        const string  asset       = "USD";

        var debit = Account.Create(accountId, "burn-source", asset);
        debit.ApplyCredit(seedBalance);
        fixture.DbContext.Accounts.Add(debit);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var worldBefore = (await fixture.DbContext.Accounts.FindAsync(SystemAccounts.WorldId))!.Balance;
        fixture.DbContext.ChangeTracker.Clear();

        var repository = new LedgerRepository(fixture.DbContext);
        var consumer   = new BurnFundsConsumer(repository, NullLogger<BurnFundsConsumer>.Instance);

        var msg     = new BurnFunds(correlationId, accountId, burnAmount, asset);
        var context = Substitute.For<ConsumeContext<BurnFunds>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        fixture.DbContext.ChangeTracker.Clear();

        var debitAccount = await fixture.DbContext.Accounts.FindAsync(accountId);
        debitAccount!.Balance.Should().Be(seedBalance - burnAmount);

        var world = await fixture.DbContext.Accounts.FindAsync(SystemAccounts.WorldId);
        world!.Balance.Should().Be(worldBefore + burnAmount);

        var postings = await fixture.DbContext.Postings
            .Where(p => p.TxId == correlationId)
            .ToListAsync();

        postings.Should().HaveCount(2);
        postings.Sum(p => p.AmountSigned).Should().Be(0m);
        postings.Should().AllSatisfy(p => p.Phase.Should().Be(Phase.Burn));

        await context.Received(1).Publish(
            Arg.Is<FundsBurned>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_publishes_BurnFailed_when_insufficient_funds()
    {
        var accountId     = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        const string asset = "USD";

        fixture.DbContext.Accounts.Add(Account.Create(accountId, "empty-account", asset));
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var repository = new LedgerRepository(fixture.DbContext);
        var consumer   = new BurnFundsConsumer(repository, NullLogger<BurnFundsConsumer>.Instance);

        var msg     = new BurnFunds(correlationId, accountId, Amount: 50m, Asset: asset);
        var context = Substitute.For<ConsumeContext<BurnFunds>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        fixture.DbContext.ChangeTracker.Clear();

        var postings = await fixture.DbContext.Postings
            .Where(p => p.TxId == correlationId)
            .ToListAsync();
        postings.Should().BeEmpty();

        await context.Received(1).Publish(
            Arg.Is<BurnFailed>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_publishes_BurnFailed_when_account_not_found()
    {
        var correlationId = Guid.NewGuid();
        var unknownId     = Guid.NewGuid();

        var repository = new LedgerRepository(fixture.DbContext);
        var consumer   = new BurnFundsConsumer(repository, NullLogger<BurnFundsConsumer>.Instance);

        var msg     = new BurnFunds(correlationId, unknownId, Amount: 10m, Asset: "USD");
        var context = Substitute.For<ConsumeContext<BurnFunds>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        await context.Received(1).Publish(
            Arg.Is<BurnFailed>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }
}
