using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHttpStatusAndResponseBody : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastHttpResponseBody",
            table: "failed_webhook_deliveries",
            type: "character varying(8192)",
            maxLength: 8192,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "LastHttpStatusCode",
            table: "failed_webhook_deliveries",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastHttpResponseBody",
            table: "failed_webhook_deliveries");

        migrationBuilder.DropColumn(
            name: "LastHttpStatusCode",
            table: "failed_webhook_deliveries");
    }
}
