using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avand.Application.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TopUps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdmissionKey = table.Column<string>(type: "text", nullable: false),
                    AmountIrr = table.Column<long>(type: "bigint", nullable: false),
                    Merchant = table.Column<string>(type: "text", nullable: false),
                    PaymentId = table.Column<string>(type: "text", nullable: true),
                    SessionCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Continuation = table.Column<string>(type: "text", nullable: true),
                    ProviderState = table.Column<string>(type: "text", nullable: false),
                    PostingState = table.Column<string>(type: "text", nullable: true),
                    MessageCode = table.Column<string>(type: "text", nullable: true),
                    CreditedBucket = table.Column<string>(type: "text", nullable: true),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopUps", x => x.Id);
                    table.CheckConstraint("CK_TopUps_Amount", "\"AmountIrr\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "GatewayEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopUpId = table.Column<Guid>(type: "uuid", nullable: false),
                    Merchant = table.Column<string>(type: "text", nullable: false),
                    PaymentId = table.Column<string>(type: "text", nullable: false),
                    CaptureReference = table.Column<string>(type: "text", nullable: true),
                    AmountIrr = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    Digest = table.Column<string>(type: "text", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatewayEvidence_TopUps_TopUpId",
                        column: x => x.TopUpId,
                        principalTable: "TopUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryWork",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TopUpId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeasedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryWork", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecoveryWork_TopUps_TopUpId",
                        column: x => x.TopUpId,
                        principalTable: "TopUps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayEvidence_Merchant_CaptureReference",
                table: "GatewayEvidence",
                columns: new[] { "Merchant", "CaptureReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayEvidence_TopUpId_Digest",
                table: "GatewayEvidence",
                columns: new[] { "TopUpId", "Digest" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryWork_DueAt",
                table: "RecoveryWork",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryWork_TopUpId_Kind",
                table: "RecoveryWork",
                columns: new[] { "TopUpId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUps_CustomerId_AdmissionKey",
                table: "TopUps",
                columns: new[] { "CustomerId", "AdmissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopUps_Merchant_PaymentId",
                table: "TopUps",
                columns: new[] { "Merchant", "PaymentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatewayEvidence");

            migrationBuilder.DropTable(
                name: "RecoveryWork");

            migrationBuilder.DropTable(
                name: "TopUps");
        }
    }
}
