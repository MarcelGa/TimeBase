using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeBase.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderGrpcAndCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grpc_endpoint",
                table: "providers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "grpc_endpoint",
                table: "providers");
        }
    }
}
