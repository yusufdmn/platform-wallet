using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIsCompensating : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCompensating",
            table: "transaction_saga_states",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsCompensating",
            table: "transaction_saga_states");
    }
}
