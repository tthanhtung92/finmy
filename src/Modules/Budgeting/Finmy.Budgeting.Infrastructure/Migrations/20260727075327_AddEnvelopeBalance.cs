using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finmy.Budgeting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvelopeBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Spent",
                schema: "budgeting",
                table: "Envelopes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "budgeting",
                table: "Envelopes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Spent",
                schema: "budgeting",
                table: "Envelopes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "budgeting",
                table: "Envelopes");
        }
    }
}
