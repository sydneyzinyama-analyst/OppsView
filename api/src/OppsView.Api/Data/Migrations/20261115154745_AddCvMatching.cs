using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OppsView.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCvMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobAd",
                table: "Applications",
                type: "character varying(50000)",
                maxLength: 50000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchResultJson",
                table: "Applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchScore",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MatchedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cvs",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cvs", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Cvs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cvs");

            migrationBuilder.DropColumn(
                name: "JobAd",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "MatchResultJson",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "MatchScore",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "MatchedAt",
                table: "Applications");
        }
    }
}
