using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaNews.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ManagerContactActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "user_activity_1_check",
                table: "user_activity");

            migrationBuilder.AddCheckConstraint(
                name: "user_activity_1_check",
                table: "user_activity",
                sql: "kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted','manager_contact_requested')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "user_activity_1_check",
                table: "user_activity");

            migrationBuilder.AddCheckConstraint(
                name: "user_activity_1_check",
                table: "user_activity",
                sql: "kind IN ('insight_opened','reasons_opened','chat_question_accepted','source_opened','action_interacted')");
        }
    }
}
