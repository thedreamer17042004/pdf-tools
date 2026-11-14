using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_OwnerUserId",
                table: "documents",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_documents_OwnerUserId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "documents");
        }
    }
}
