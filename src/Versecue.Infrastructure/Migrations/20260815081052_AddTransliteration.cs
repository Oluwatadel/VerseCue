using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Versecue.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransliteration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Transliteration",
                table: "BibleVerses",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Transliteration",
                table: "BibleVerses");
        }
    }
}
