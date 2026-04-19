using Microsoft.EntityFrameworkCore.Migrations;



#nullable disable



namespace Cortex.API.Migrations

{

    /// <inheritdoc />

    public class AddAiTriageSuggestedStatus : Migration

    {

        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)

        {

            migrationBuilder.AddColumn<string>(

                name: "AiTriageSuggestedStatus",

                table: "Tickets",

                type: "nvarchar(max)",

                nullable: true);

        }



        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)

        {

            migrationBuilder.DropColumn(

                name: "AiTriageSuggestedStatus",

                table: "Tickets");

        }

    }

}

