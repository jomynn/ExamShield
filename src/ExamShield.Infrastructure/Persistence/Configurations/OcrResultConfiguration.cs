using System.Text.Json;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExamShield.Infrastructure.Persistence.Configurations;

public sealed class OcrResultConfiguration : IEntityTypeConfiguration<OcrResult>
{
    private sealed record ExtractedAnswerJson(int QuestionNumber, string Text, double Confidence);

    private static readonly ValueConverter<IReadOnlyList<ExtractedAnswer>, string> AnswersConverter = new(
        answers => JsonSerializer.Serialize(
            answers.Select(a => new ExtractedAnswerJson(a.QuestionNumber, a.Text, a.Confidence.Value)).ToList(),
            (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<ExtractedAnswerJson>>(json, (JsonSerializerOptions?)null)!
            .Select(j => new ExtractedAnswer(j.QuestionNumber, j.Text, new OcrConfidence(j.Confidence)))
            .ToList<ExtractedAnswer>());

    public void Configure(EntityTypeBuilder<OcrResult> builder)
    {
        builder.ToTable("OcrResults");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new OcrResultId(v));

        builder.Property(e => e.CaptureId)
            .HasConversion(id => id.Value, v => new CaptureId(v));

        builder.Property(e => e.Status)
            .HasConversion<int>();

        builder.Property(e => e.OverallConfidence)
            .HasConversion(c => c.Value, v => new OcrConfidence(v))
            .HasColumnName("OverallConfidence");

        builder.Property(e => e.ProcessedAt)
            .HasConversion(dto => dto.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        builder.Property(e => e.Answers)
            .HasConversion(AnswersConverter)
            .HasColumnType("TEXT");

        builder.Ignore(e => e.DomainEvents);

        // OCR queue: pending/in-progress results for the review queue
        builder.HasIndex(e => new { e.CaptureId, e.Status });
    }
}
