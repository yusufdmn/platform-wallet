using FluentAssertions;
using Xunit;

namespace PlatformWallet.TransactionIntake.IntegrationTests;

[Trait("Category", "Integration")]
public class OutboxAtomicityTests
{
    [Fact(Skip = "Enabled once IntakeDbContext + MassTransit EF Outbox are wired.")]
    public void Transaction_insert_and_outbox_message_commit_atomically()
    {
        // TODO: kill Postgres container mid-transaction, verify neither
        // `transactions` nor `outbox_message` row is present on reconnect.
        true.Should().BeTrue();
    }
}
