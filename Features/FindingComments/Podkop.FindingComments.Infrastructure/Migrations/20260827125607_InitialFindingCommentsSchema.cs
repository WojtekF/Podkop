using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Podkop.FindingComments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFindingCommentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finding_comments");

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "finding_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comment_votes",
                schema: "finding_comments",
                columns: table => new
                {
                    voter = table.Column<string>(type: "text", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vote_direction = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment_votes", x => new { x.comment_id, x.voter });
                    table.ForeignKey(
                        name: "fk_comment_votes_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "finding_comments",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_votes",
                schema: "finding_comments");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "finding_comments");
        }
    }
}
