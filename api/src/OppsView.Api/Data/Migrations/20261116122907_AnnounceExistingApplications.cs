using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OppsView.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnnounceExistingApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Applications created before the outbox existed were never
            // announced, so the notifier doesn't know them. Announce each one
            // once, with exactly the JSON OutboxInterceptor writes; the
            // dispatcher then sends them like any other event.
            migrationBuilder.Sql("""
                INSERT INTO "OutboxMessages" ("Id", "Type", "Payload", "OccurredAt", "Attempts")
                SELECT
                    gen_random_uuid(),
                    'application.changed',
                    json_build_object(
                        'applicationId', a."Id",
                        'userId', a."UserId",
                        'company', a."Company",
                        'role', a."Role",
                        'platform', a."Platform",
                        'status', a."Status",
                        'previousStatus', NULL,
                        'appliedOn', to_char(a."AppliedOn", 'YYYY-MM-DD'),
                        'statusChangedAt', to_char(a."StatusChangedAt" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
                        'occurredAt', to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
                        'version', 1
                    )::text,
                    now(),
                    0
                FROM "Applications" AS a;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the events are (or will be) sent and then pruned.
        }
    }
}
