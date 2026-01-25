using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeBase.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePayloadColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payload",
                table: "time_series_data");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payload",
                table: "time_series_data",
                type: "jsonb",
                nullable: true);
        }
    }
}
