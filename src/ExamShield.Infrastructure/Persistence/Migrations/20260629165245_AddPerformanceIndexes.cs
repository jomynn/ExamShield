using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamShield.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Scores_CaptureId",
                table: "Scores",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_ExamId_IsPublished",
                table: "Scores",
                columns: new[] { "ExamId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_StudentId",
                table: "Scores",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_OcrResults_CaptureId_Status",
                table: "OcrResults",
                columns: new[] { "CaptureId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualReviews_CaptureId",
                table: "ManualReviews",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualReviews_Status",
                table: "ManualReviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_DeviceId",
                table: "Captures",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_ExamId_Status",
                table: "Captures",
                columns: new[] { "ExamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Captures_InvigilatorId",
                table: "Captures",
                column: "InvigilatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Captures_StudentId",
                table: "Captures",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_CaptureId",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Scores_ExamId_IsPublished",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Scores_StudentId",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_OcrResults_CaptureId_Status",
                table: "OcrResults");

            migrationBuilder.DropIndex(
                name: "IX_ManualReviews_CaptureId",
                table: "ManualReviews");

            migrationBuilder.DropIndex(
                name: "IX_ManualReviews_Status",
                table: "ManualReviews");

            migrationBuilder.DropIndex(
                name: "IX_Captures_DeviceId",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_Captures_ExamId_Status",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_Captures_InvigilatorId",
                table: "Captures");

            migrationBuilder.DropIndex(
                name: "IX_Captures_StudentId",
                table: "Captures");
        }
    }
}
