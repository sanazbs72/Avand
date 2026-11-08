using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Avand.Ledger.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V02AssetJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_JournalLines_Amount",
                table: "JournalLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Balance",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "AmountIrr",
                table: "JournalLines",
                newName: "AmountMinor");

            migrationBuilder.RenameColumn(
                name: "BalanceIrr",
                table: "Accounts",
                newName: "BalanceMinor");

            migrationBuilder.AddColumn<string>(
                name: "Template",
                table: "Journals",
                type: "text",
                nullable: false,
                defaultValue: "F02");

            migrationBuilder.AddColumn<string>(
                name: "Asset",
                table: "Accounts",
                type: "text",
                nullable: false,
                defaultValue: "IRR");

            migrationBuilder.AddColumn<bool>(
                name: "DebitNormal",
                table: "Accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BuyDecisions",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionCode = table.Column<string>(type: "text", nullable: true),
                    OriginalOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncrementalUncoveredGoldMg = table.Column<long>(type: "bigint", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyDecisions", x => x.OperationId);
                    table.ForeignKey(
                        name: "FK_BuyDecisions_Journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "Journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoldBacking",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IsOutage = table.Column<bool>(type: "boolean", nullable: true),
                    OutgoingMg = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoldBacking", x => x.Id);
                    table.CheckConstraint("CK_GoldBacking_Control", "\"Id\" = 1 AND \"OutgoingMg\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "BuyBusinessClaims",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyBusinessClaims", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_BuyBusinessClaims_BuyDecisions_OperationId",
                        column: x => x.OperationId,
                        principalTable: "BuyDecisions",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuyQuoteClaims",
                columns: table => new
                {
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyQuoteClaims", x => x.QuoteId);
                    table.ForeignKey(
                        name: "FK_BuyQuoteClaims_BuyDecisions_OperationId",
                        column: x => x.OperationId,
                        principalTable: "BuyDecisions",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                ALTER TABLE public."Accounts" DISABLE TRIGGER avand_account_projection;
                UPDATE public."Accounts" SET "DebitNormal" = true WHERE "Bucket" = 'GatewayReceivable';
                ALTER TABLE public."Accounts" ENABLE TRIGGER avand_account_projection;
                """);
            migrationBuilder.Sql(V02JournalSql.Upgrade);

            migrationBuilder.AddCheckConstraint(
                name: "CK_JournalLines_Amount",
                table: "JournalLines",
                sql: "\"AmountMinor\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Asset",
                table: "Accounts",
                sql: "\"Asset\" = CASE WHEN \"Bucket\" IN ('CustodianGold', 'PlatformGoldPosition',\n    'CustomerGoldAvailable', 'CustomerGoldReserved', 'CustomerGoldRestricted') THEN 'AU' ELSE 'IRR' END\nAND \"DebitNormal\" = (\"Bucket\" IN ('GatewayReceivable', 'CustodianGold'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Balance",
                table: "Accounts",
                sql: "\"BalanceMinor\" >= 0 OR \"Bucket\" IN ('TradingRialClearing', 'PlatformGoldPosition')");

            migrationBuilder.CreateIndex(
                name: "IX_BuyBusinessClaims_OperationId",
                table: "BuyBusinessClaims",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_BuyDecisions_JournalId",
                table: "BuyDecisions",
                column: "JournalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyQuoteClaims_OperationId",
                table: "BuyQuoteClaims",
                column: "OperationId");

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Asset", "BalanceMinor", "Bucket", "DebitNormal", "Owner" },
                values: new object[,]
                {
                    { new Guid("26020000-0000-0000-0000-000000000001"), "IRR", 0L, "TradingRialClearing", false, "platform" },
                    { new Guid("26020000-0000-0000-0000-000000000002"), "IRR", 0L, "FeeRevenue", false, "platform" },
                    { new Guid("26020000-0000-0000-0000-000000000003"), "AU", 0L, "PlatformGoldPosition", false, "platform" }
                });

            migrationBuilder.InsertData(
                table: "GoldBacking",
                columns: new[] { "Id", "IsOutage", "OutgoingMg" },
                values: new object[] { 1, null, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Restore a pre-V02 backup to revert the asset journal migration.");
    }
}
