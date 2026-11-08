using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Avand.Ledger.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V03SellLiquidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Asset",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "LiquidityControl",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    CommitmentsIrr = table.Column<long>(type: "bigint", nullable: false),
                    BufferIrr = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiquidityControl", x => x.Id);
                    table.CheckConstraint("CK_LiquidityControl_Control", "\"Id\" = 1 AND \"CommitmentsIrr\" >= 0 AND \"BufferIrr\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SellDecisions",
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
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellDecisions", x => x.OperationId);
                    table.ForeignKey(
                        name: "FK_SellDecisions_Journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "Journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellBusinessClaims",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellBusinessClaims", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_SellBusinessClaims_SellDecisions_OperationId",
                        column: x => x.OperationId,
                        principalTable: "SellDecisions",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellQuoteClaims",
                columns: table => new
                {
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellQuoteClaims", x => x.QuoteId);
                    table.ForeignKey(
                        name: "FK_SellQuoteClaims_SellDecisions_OperationId",
                        column: x => x.OperationId,
                        principalTable: "SellDecisions",
                        principalColumn: "OperationId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Replace the asset/normal-side check before seeding, while no deferred trigger events are pending.
            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Asset",
                table: "Accounts",
                sql: "\"Asset\" = CASE WHEN \"Bucket\" IN ('CustodianGold', 'PlatformGoldPosition',\n    'CustomerGoldAvailable', 'CustomerGoldReserved', 'CustomerGoldRestricted') THEN 'AU' ELSE 'IRR' END\nAND \"DebitNormal\" = (\"Bucket\" IN ('GatewayReceivable', 'CustodianGold', 'BankCash'))");

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Asset", "BalanceMinor", "Bucket", "DebitNormal", "Owner" },
                values: new object[,]
                {
                    { new Guid("26030000-0000-0000-0000-000000000001"), "IRR", 0L, "BankCash", true, "platform" },
                    { new Guid("26030000-0000-0000-0000-000000000002"), "IRR", 0L, "PlatformCapital", false, "platform" }
                });

            migrationBuilder.InsertData(
                table: "LiquidityControl",
                columns: new[] { "Id", "BufferIrr", "CommitmentsIrr", "Version" },
                values: new object[] { 1, 0L, 0L, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_SellBusinessClaims_OperationId",
                table: "SellBusinessClaims",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_SellDecisions_JournalId",
                table: "SellDecisions",
                column: "JournalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellQuoteClaims_OperationId",
                table: "SellQuoteClaims",
                column: "OperationId");

            migrationBuilder.Sql(V03JournalSql.Upgrade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Restore a pre-V03 backup to revert the sell liquidity migration.");

    }
}
