using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Podkop.Findings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFindingsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "findings");

            migrationBuilder.CreateTable(
                name: "findings",
                schema: "findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    thumbnail = table.Column<string>(type: "text", nullable: true),
                    author = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    promoted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_findings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "finding_votes",
                schema: "findings",
                columns: table => new
                {
                    voter = table.Column<string>(type: "text", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finding_votes", x => new { x.finding_id, x.voter });
                    table.ForeignKey(
                        name: "fk_finding_votes_findings_finding_id",
                        column: x => x.finding_id,
                        principalSchema: "findings",
                        principalTable: "findings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finding_votes",
                schema: "findings");

            migrationBuilder.DropTable(
                name: "findings",
                schema: "findings");
        }
    }
}
