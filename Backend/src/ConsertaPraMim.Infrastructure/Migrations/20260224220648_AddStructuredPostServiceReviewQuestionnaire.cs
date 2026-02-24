using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConsertaPraMim.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredPostServiceReviewQuestionnaire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommunicationRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompositeScore",
                table: "Reviews",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostBenefitRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NpsScore",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PunctualityRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceQualityRating",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WouldHireAgain",
                table: "Reviews",
                type: "bit",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Reviews_QuestionnaireScores_Range",
                table: "Reviews",
                sql: "([ServiceQualityRating] IS NULL OR ([ServiceQualityRating] >= 1 AND [ServiceQualityRating] <= 5)) AND ([PunctualityRating] IS NULL OR ([PunctualityRating] >= 1 AND [PunctualityRating] <= 5)) AND ([CommunicationRating] IS NULL OR ([CommunicationRating] >= 1 AND [CommunicationRating] <= 5)) AND ([CostBenefitRating] IS NULL OR ([CostBenefitRating] >= 1 AND [CostBenefitRating] <= 5)) AND ([NpsScore] IS NULL OR ([NpsScore] >= 0 AND [NpsScore] <= 10)) AND ([CompositeScore] IS NULL OR ([CompositeScore] >= 0 AND [CompositeScore] <= 100))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Reviews_QuestionnaireScores_Range",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CommunicationRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CompositeScore",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CostBenefitRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "NpsScore",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "PunctualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceQualityRating",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "WouldHireAgain",
                table: "Reviews");
        }
    }
}
