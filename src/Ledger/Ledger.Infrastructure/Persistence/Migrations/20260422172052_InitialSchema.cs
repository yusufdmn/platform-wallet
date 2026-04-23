using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchema : Migration
{
    private static readonly string[] AccountIdxColumns    = ["account_id", "created_at"];
    private static readonly bool[]   AccountIdxDescending = [false, true];
    private static readonly string[] UniqueIdxColumns     = ["tx_id", "account_id", "phase"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "accounts",
            columns: table => new
            {
                id          = table.Column<Guid>(type: "uuid", nullable: false),
                name        = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                asset       = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                balance     = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                held_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                is_system   = table.Column<bool>(type: "boolean", nullable: false),
                metadata    = table.Column<string>(type: "jsonb", nullable: false),
                row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                created_at  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_accounts", x => x.id);
                table.CheckConstraint("ck_accounts_balance_floor",     "is_system = true OR balance >= 0");
                table.CheckConstraint("ck_accounts_held_amount_floor", "held_amount >= 0");
            });

        migrationBuilder.CreateTable(
            name: "postings",
            columns: table => new
            {
                id           = table.Column<long>(type: "bigint", nullable: false)
                                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                tx_id        = table.Column<Guid>(type: "uuid", nullable: false),
                account_id   = table.Column<Guid>(type: "uuid", nullable: false),
                asset        = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                amount_signed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                entry_kind   = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                phase        = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at   = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_postings", x => x.id);
                table.ForeignKey(
                    name: "FK_postings_accounts_account_id",
                    column: x => x.account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_postings_account_created",
            table: "postings",
            columns: AccountIdxColumns,
            descending: AccountIdxDescending);

        migrationBuilder.CreateIndex(
            name: "ix_postings_tx",
            table: "postings",
            column: "tx_id");

        migrationBuilder.CreateIndex(
            name: "uq_posting_tx_account_phase",
            table: "postings",
            columns: UniqueIdxColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "postings");
        migrationBuilder.DropTable(name: "accounts");
    }
}
