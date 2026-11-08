using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avand.Application.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V02BuyOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BuyQuotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedPrincipalIrr = table.Column<long>(type: "bigint", nullable: false),
                    AskIrrPerGram = table.Column<long>(type: "bigint", nullable: false),
                    BidIrrPerGram = table.Column<long>(type: "bigint", nullable: false),
                    PriceObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuantityMg = table.Column<long>(type: "bigint", nullable: false),
                    PrincipalIrr = table.Column<long>(type: "bigint", nullable: false),
                    FeeRatePpm = table.Column<int>(type: "integer", nullable: false),
                    FeeIrr = table.Column<long>(type: "bigint", nullable: false),
                    TotalDebitIrr = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersion = table.Column<string>(type: "text", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyQuotes", x => x.Id);
                    table.CheckConstraint("CK_BuyQuotes_Terms", "\"RequestedPrincipalIrr\" > 0 AND \"AskIrrPerGram\" > 0 AND \"BidIrrPerGram\" > 0 AND \"QuantityMg\" > 0\nAND \"PrincipalIrr\" > 0 AND \"FeeRatePpm\" >= 0 AND \"FeeIrr\" >= 0\nAND \"TotalDebitIrr\" = \"PrincipalIrr\" + \"FeeIrr\" AND \"ExpiresAt\" > \"IssuedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "BuyOrders",
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
                    IncrementalUncoveredGoldMg = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyOrders", x => x.Id);
                    table.CheckConstraint("CK_BuyOrders_Status", "\"Status\" IN ('Processing', 'FinancialOutcomeUnknown', 'Completed', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_BuyOrders_BuyQuotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "BuyQuotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuyRecoveryWork",
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
                    table.PrimaryKey("PK_BuyRecoveryWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyRecoveryWork_BuyOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "BuyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcurementIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    UncoveredGoldMg = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementIntents", x => x.Id);
                    table.CheckConstraint("CK_ProcurementIntents_Quantity", "\"UncoveredGoldMg\" > 0");
                    table.ForeignKey(
                        name: "FK_ProcurementIntents_BuyOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "BuyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuyOrders_CustomerId_IdempotencyKey",
                table: "BuyOrders",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyOrders_OperationId",
                table: "BuyOrders",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyOrders_QuoteId",
                table: "BuyOrders",
                column: "QuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BuyQuotes_CustomerId",
                table: "BuyQuotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BuyRecoveryWork_DueAt",
                table: "BuyRecoveryWork",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_BuyRecoveryWork_OrderId",
                table: "BuyRecoveryWork",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementIntents_OrderId",
                table: "ProcurementIntents",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuyRecoveryWork");

            migrationBuilder.DropTable(
                name: "ProcurementIntents");

            migrationBuilder.DropTable(
                name: "BuyOrders");

            migrationBuilder.DropTable(
                name: "BuyQuotes");
        }
    }
}
