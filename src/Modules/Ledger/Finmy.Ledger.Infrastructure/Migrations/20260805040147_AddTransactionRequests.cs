using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finmy.Ledger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionRequests",
                schema: "ledger",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ErrorDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorType = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionRequests", x => x.TransactionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRequests_ExpiresAtUtc",
                schema: "ledger",
                table: "TransactionRequests",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionRequests",
                schema: "ledger");
        }
    }
}
