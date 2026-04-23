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
public class MintFundsConsumerTests(LedgerIntegrationFixture fixture)
{
    [Fact]
    public async Task Consume_mints_funds_and_creates_two_postings()
    {
        var accountId     = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        const decimal amount = 150m;
        const string asset  = "USD";

        fixture.DbContext.Accounts.Add(Account.Create(accountId, "test-account", asset));
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var repository = new LedgerRepository(fixture.DbContext);
        var consumer   = new MintFundsConsumer(repository, NullLogger<MintFundsConsumer>.Instance);

        var msg     = new MintFunds(correlationId, accountId, amount, asset);
        var context = Substitute.For<ConsumeContext<MintFunds>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        fixture.DbContext.ChangeTracker.Clear();

        var creditAccount = await fixture.DbContext.Accounts.FindAsync(accountId);
        creditAccount!.Balance.Should().Be(amount);

        var world = await fixture.DbContext.Accounts.FindAsync(SystemAccounts.WorldId);
        world!.Balance.Should().Be(-amount);

        var postings = await fixture.DbContext.Postings
            .Where(p => p.TxId == correlationId)
            .ToListAsync();

        postings.Should().HaveCount(2);
        postings.Sum(p => p.AmountSigned).Should().Be(0m);
        postings.Should().AllSatisfy(p => p.Phase.Should().Be(Phase.Mint));

        await context.Received(1).Publish(
            Arg.Is<FundsMinted>(e => e.CorrelationId == correlationId),
            Arg.Any<CancellationToken>());
    }
}
