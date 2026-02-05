using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeBase.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeSeriesDataCompositeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The primary key already exists in the database (created by init.sql as time_series_data_pkey)
            // This migration only updates the EF Core model to reflect that
            // No database changes needed - this is a model-only migration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No database changes to roll back - this is a model-only migration
        }
    }
}