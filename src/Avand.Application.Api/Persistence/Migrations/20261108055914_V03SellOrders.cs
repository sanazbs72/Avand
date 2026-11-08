using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avand.Application.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V03SellOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SellQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputMode = table.Column<string>(type: "text", nullable: false),
                    InputValue = table.Column<long>(type: "bigint", nullable: false),
                    AskIrrPerGram = table.Column<long>(type: "bigint", nullable: false),
                    BidIrrPerGram = table.Column<long>(type: "bigint", nullable: false),
                    PriceObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuantityMg = table.Column<long>(type: "bigint", nullable: false),
                    GrossIrr = table.Column<long>(type: "bigint", nullable: false),
                    FeeRatePpm = table.Column<int>(type: "integer", nullable: false),
                    FeeIrr = table.Column<long>(type: "bigint", nullable: false),
                    NetIrr = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersion = table.Column<string>(type: "text", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellQuotes", x => x.Id);
                    table.CheckConstraint("CK_SellQuotes_Terms", "\"InputMode\" IN ('Weight', 'GrossIrr') AND \"InputValue\" > 0 AND \"AskIrrPerGram\" > 0\nAND \"BidIrrPerGram\" > 0 AND \"QuantityMg\" > 0 AND \"GrossIrr\" > 0 AND \"FeeRatePpm\" >= 0\nAND \"FeeIrr\" >= 0 AND \"NetIrr\" > 0 AND \"NetIrr\" = \"GrossIrr\" - \"FeeIrr\" AND \"ExpiresAt\" > \"IssuedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "SellOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestHash = table.Column<string>(type: "text", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Request = table.Column<string>(type: "text", nullable: false),
                    AdmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReasonCode = table.Column<string>(type: "text", nullable: true),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellOrders", x => x.Id);
                    table.CheckConstraint("CK_SellOrders_Status", "\"Status\" IN ('Processing', 'FinancialOutcomeUnknown', 'Completed', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_SellOrders_SellQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "SellQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellRecoveryWork",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeasedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellRecoveryWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellRecoveryWork_SellOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "SellOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellOrders_CustomerId_IdempotencyKey",
                table: "SellOrders",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellOrders_OperationId",
                table: "SellOrders",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellOrders_QuoteId",
                table: "SellOrders",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellQuotes_CustomerId",
                table: "SellQuotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SellRecoveryWork_DueAt",
                table: "SellRecoveryWork",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_SellRecoveryWork_OrderId",
                table: "SellRecoveryWork",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SellRecoveryWork");

            migrationBuilder.DropTable(
                name: "SellOrders");

            migrationBuilder.DropTable(
                name: "SellQuotes");
        }
    }
}
