using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FixRowVersionDefault : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE transaction_saga_states ALTER COLUMN row_version SET DEFAULT '\\x00000000'::bytea;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE transaction_saga_states ALTER COLUMN row_version DROP DEFAULT;");
    }
}
