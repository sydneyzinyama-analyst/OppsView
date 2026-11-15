using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OppsView.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "StatusHistory",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Everything that existed before email import was typed in by hand.
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Applications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Everything that existed before email import was typed in by hand.
                defaultValue: "Manual");

            migrationBuilder.CreateTable(
                name: "ImportedEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Sender = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedEmails_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImportedEmails_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedEmails_ApplicationId",
                table: "ImportedEmails",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedEmails_UserId_MessageId",
                table: "ImportedEmails",
                columns: new[] { "UserId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportedEmails_UserId_Outcome",
                table: "ImportedEmails",
                columns: new[] { "UserId", "Outcome" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportedEmails");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StatusHistory");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Applications");
        }
    }
}
