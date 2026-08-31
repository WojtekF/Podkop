using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Podkop.FindingComments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempts",
                schema: "finding_comments",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "error",
                schema: "finding_comments",
                table: "outbox_messages",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempts",
                schema: "finding_comments",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "error",
                schema: "finding_comments",
                table: "outbox_messages");
        }
    }
}
