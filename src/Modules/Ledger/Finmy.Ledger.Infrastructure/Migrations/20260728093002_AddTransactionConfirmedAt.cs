using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finmy.Ledger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionConfirmedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                schema: "ledger",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                schema: "ledger",
                table: "Transactions");
        }
    }
}
