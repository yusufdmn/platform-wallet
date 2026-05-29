using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddVoidAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "void_attempts",
            table: "transaction_saga_states",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "void_attempts",
            table: "transaction_saga_states");
    }
}
