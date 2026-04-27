using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRowVersionDefault : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "row_version",
            table: "accounts",
            type: "bytea",
            rowVersion: true,
            nullable: false,
            defaultValueSql: "'\\x00000000'::bytea",
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldRowVersion: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<byte[]>(
            name: "row_version",
            table: "accounts",
            type: "bytea",
            rowVersion: true,
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldRowVersion: true,
            oldDefaultValueSql: "'\\x00000000'::bytea");
    }
}
