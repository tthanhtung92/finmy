using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finmy.Ledger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAtUtc",
                schema: "ledger",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                schema: "ledger",
                table: "Transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                schema: "ledger",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "ledger",
                table: "Transactions");
        }
    }
}
