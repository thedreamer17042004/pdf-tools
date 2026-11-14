using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_annotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PageIndex = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Width = table.Column<double>(type: "double precision", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Opacity = table.Column<double>(type: "double precision", nullable: false),
                    Contents = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GeometryJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_annotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_annotations_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_annotations_DocumentId",
                table: "document_annotations",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_annotations_DocumentId_PageIndex",
                table: "document_annotations",
                columns: new[] { "DocumentId", "PageIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_annotations");
        }
    }
}
