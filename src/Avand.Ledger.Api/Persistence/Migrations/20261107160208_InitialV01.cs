using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Avand.Ledger.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Bucket = table.Column<string>(type: "text", nullable: false),
                    BalanceIrr = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_Balance", "\"BalanceIrr\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Eligibility",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsEligible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Eligibility", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "InitiationDecisions",
                columns: table => new
                {
                    Producer = table.Column<string>(type: "text", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Applied = table.Column<bool>(type: "boolean", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiationDecisions", x => new { x.Producer, x.CommandId });
                });

            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalLines",
                columns: table => new
                {
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDebit = table.Column<bool>(type: "boolean", nullable: false),
                    AmountIrr = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLines", x => new { x.JournalId, x.AccountId });
                    table.CheckConstraint("CK_JournalLines_Amount", "\"AmountIrr\" > 0");
                    table.ForeignKey(
                        name: "FK_JournalLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalLines_Journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "Journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationDecisions",
                columns: table => new
                {
                    Producer = table.Column<string>(type: "text", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessReference = table.Column<string>(type: "text", nullable: false),
                    Merchant = table.Column<string>(type: "text", nullable: true),
                    CaptureReference = table.Column<string>(type: "text", nullable: true),
                    Fingerprint = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountIrr = table.Column<long>(type: "bigint", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditedBucket = table.Column<string>(type: "text", nullable: true),
                    RejectionCode = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationDecisions", x => new { x.Producer, x.OperationId });
                    table.ForeignKey(
                        name: "FK_OperationDecisions_Journals_JournalId",
                        column: x => x.JournalId,
                        principalTable: "Journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Owner_Bucket",
                table: "Accounts",
                columns: new[] { "Owner", "Bucket" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InitiationDecisions_Producer_PaymentIntentId",
                table: "InitiationDecisions",
                columns: new[] { "Producer", "PaymentIntentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_AccountId",
                table: "JournalLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationDecisions_JournalId",
                table: "OperationDecisions",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationDecisions_Merchant_CaptureReference",
                table: "OperationDecisions",
                columns: new[] { "Merchant", "CaptureReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationDecisions_Producer_BusinessReference",
                table: "OperationDecisions",
                columns: new[] { "Producer", "BusinessReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Eligibility");

            migrationBuilder.DropTable(
                name: "InitiationDecisions");

            migrationBuilder.DropTable(
                name: "JournalLines");

            migrationBuilder.DropTable(
                name: "OperationDecisions");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Journals");
        }
    }
}
