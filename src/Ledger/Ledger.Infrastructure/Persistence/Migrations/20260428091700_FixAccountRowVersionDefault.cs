using Microsoft.EntityFrameworkCore.Migrations;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Migrations;

public partial class FixAccountRowVersionDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts ALTER COLUMN row_version SET DEFAULT '\\x00000000'::bytea;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE accounts ALTER COLUMN row_version DROP DEFAULT;");
    }
}
