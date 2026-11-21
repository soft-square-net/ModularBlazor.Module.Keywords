using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Keywords.Infrastructure.Data.Migrations.Oracle
{
    /// <inheritdoc />
    public partial class InitialConcepts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "keywords_concepts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TenantId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    SameAsJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    RelatedJson = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    CreatedBy = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keywords_concepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "keywords_concept_labels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Language = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    SearchKey = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    ConceptId = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keywords_concept_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_keywords_concept_labels_keywords_concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "keywords_concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_keywords_concept_labels_ConceptId",
                table: "keywords_concept_labels",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_keywords_concept_labels_SearchKey",
                table: "keywords_concept_labels",
                column: "SearchKey");

            migrationBuilder.CreateIndex(
                name: "IX_keywords_concepts_Name",
                table: "keywords_concepts",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_keywords_concepts_TenantId",
                table: "keywords_concepts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_keywords_concepts_TenantId_Slug",
                table: "keywords_concepts",
                columns: new[] { "TenantId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "keywords_concept_labels");

            migrationBuilder.DropTable(
                name: "keywords_concepts");
        }
    }
}
