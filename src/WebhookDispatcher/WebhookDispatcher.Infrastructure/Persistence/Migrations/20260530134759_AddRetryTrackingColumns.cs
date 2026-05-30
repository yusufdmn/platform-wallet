using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRetryTrackingColumns : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetriedAt",
                table: "failed_webhook_deliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "failed_webhook_deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "failed_webhook_deliveries",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Failed");

            migrationBuilder.CreateIndex(
                name: "IX_failed_webhook_deliveries_Status",
                table: "failed_webhook_deliveries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_failed_webhook_deliveries_Status",
                table: "failed_webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "RetriedAt",
                table: "failed_webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "failed_webhook_deliveries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "failed_webhook_deliveries");
        }
}
