using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlfaNews.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class FeedSnapshotMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "feed_snapshot_pkey",
                table: "feed_snapshot");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "feed_snapshot",
                type: "text",
                nullable: false,
                defaultValueSql: "'live'");

            migrationBuilder.AddPrimaryKey(
                name: "feed_snapshot_pkey",
                table: "feed_snapshot",
                columns: new[] { "company_id", "mode" });

            migrationBuilder.AddCheckConstraint(
                name: "feed_snapshot_1_check",
                table: "feed_snapshot",
                sql: "mode IN ('live','demo')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM feed_snapshot WHERE mode <> 'live'");

            migrationBuilder.DropPrimaryKey(
                name: "feed_snapshot_pkey",
                table: "feed_snapshot");

            migrationBuilder.DropCheckConstraint(
                name: "feed_snapshot_1_check",
                table: "feed_snapshot");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "feed_snapshot");

            migrationBuilder.AddPrimaryKey(
                name: "feed_snapshot_pkey",
                table: "feed_snapshot",
                column: "company_id");
        }
    }
}
