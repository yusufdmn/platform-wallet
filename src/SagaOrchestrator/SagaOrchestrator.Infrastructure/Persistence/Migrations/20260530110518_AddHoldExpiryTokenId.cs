using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHoldExpiryTokenId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "hold_expiry_token_id",
            table: "transaction_saga_states",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "hold_expiry_token_id",
            table: "transaction_saga_states");
    }
}
