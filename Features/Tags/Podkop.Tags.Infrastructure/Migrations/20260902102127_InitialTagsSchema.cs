using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Podkop.Tags.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTagsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tags");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag_memberships",
                schema: "tags",
                columns: table => new
                {
                    tag = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag_memberships", x => new { x.tag, x.content_type, x.content_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_tag_memberships_tag_created_at",
                schema: "tags",
                table: "tag_memberships",
                columns: new[] { "tag", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "tags");

            migrationBuilder.DropTable(
                name: "tag_memberships",
                schema: "tags");
        }
    }
}
