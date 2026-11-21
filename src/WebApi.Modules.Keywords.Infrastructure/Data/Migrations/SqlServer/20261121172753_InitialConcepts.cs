using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Keywords.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "keywords");

            migrationBuilder.CreateTable(
                name: "concepts",
                schema: "keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SameAsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "concept_labels",
                schema: "keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Language = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SearchKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ConceptId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concept_labels_concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalSchema: "keywords",
                        principalTable: "concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_concept_labels_ConceptId",
                schema: "keywords",
                table: "concept_labels",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_concept_labels_SearchKey",
                schema: "keywords",
                table: "concept_labels",
                column: "SearchKey");

            migrationBuilder.CreateIndex(
                name: "IX_concepts_Name",
                schema: "keywords",
                table: "concepts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_concepts_TenantId",
                schema: "keywords",
                table: "concepts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_concepts_TenantId_Slug",
                schema: "keywords",
                table: "concepts",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concept_labels",
                schema: "keywords");

            migrationBuilder.DropTable(
                name: "concepts",
                schema: "keywords");
        }
    }
}
