using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SUPPORT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnswerMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "answer_mode",
                table: "chat_message",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "answer_mode",
                table: "chat_message");
        }
    }
}
