using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ChangeDecimalPrecisionTo28x8 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "amount_signed",
            table: "postings",
            type: "numeric(28,8)",
            precision: 28,
            scale: 8,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,2)",
            oldPrecision: 18,
            oldScale: 2);

        migrationBuilder.AlterColumn<decimal>(
            name: "held_amount",
            table: "accounts",
            type: "numeric(28,8)",
            precision: 28,
            scale: 8,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,2)",
            oldPrecision: 18,
            oldScale: 2);

        migrationBuilder.AlterColumn<decimal>(
            name: "balance",
            table: "accounts",
            type: "numeric(28,8)",
            precision: 28,
            scale: 8,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(18,2)",
            oldPrecision: 18,
            oldScale: 2);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "amount_signed",
            table: "postings",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(28,8)",
            oldPrecision: 28,
            oldScale: 8);

        migrationBuilder.AlterColumn<decimal>(
            name: "held_amount",
            table: "accounts",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(28,8)",
            oldPrecision: 28,
            oldScale: 8);

        migrationBuilder.AlterColumn<decimal>(
            name: "balance",
            table: "accounts",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(28,8)",
            oldPrecision: 28,
            oldScale: 8);
    }
}
