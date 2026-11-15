using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OppsView.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoReplyDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NoReplyDeadline",
                table: "Applications",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_NoReplyDeadline",
                table: "Applications",
                column: "NoReplyDeadline");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_NoReplyDeadline",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "NoReplyDeadline",
                table: "Applications");
        }
    }
}
