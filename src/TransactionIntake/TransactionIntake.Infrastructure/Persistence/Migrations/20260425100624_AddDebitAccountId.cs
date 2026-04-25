using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDebitAccountId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "debit_account_id",
            table: "transactions",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "debit_account_id",
            table: "transactions");
    }
}
