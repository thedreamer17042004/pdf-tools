using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Processing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultStorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ResultFileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ResultContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_CreatedAtUtc",
                table: "processing_jobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_Status",
                table: "processing_jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processing_jobs");
        }
    }
}
