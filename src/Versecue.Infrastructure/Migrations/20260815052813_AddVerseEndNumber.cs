using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versecue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerseEndNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerseEndNumber",
                table: "BibleVerses",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerseEndNumber",
                table: "BibleVerses");
        }
    }
}
