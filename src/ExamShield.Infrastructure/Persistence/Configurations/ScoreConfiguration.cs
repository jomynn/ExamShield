using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

public sealed class ScoreConfiguration : IEntityTypeConfiguration<Score>
{
    public void Configure(EntityTypeBuilder<Score> builder)
    {
        builder.ToTable("Scores");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ScoreId(v));

        builder.Property(e => e.CaptureId)
            .HasConversion(id => id.Value, v => new CaptureId(v));

        builder.Property(e => e.ExamId)
            .HasConversion(id => id.Value, v => new ExamId(v));

        builder.Property(e => e.StudentId)
            .HasConversion(id => id.Value, v => new StudentId(v));

        builder.Property(e => e.CorrectAnswers);
        builder.Property(e => e.TotalQuestions);
        builder.Property(e => e.Percentage);

        builder.Property(e => e.ScoredAt)
            .HasConversion(dto => dto.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        builder.Property(e => e.IsPublished);

        builder.Property(e => e.PublishedAt)
            .HasConversion(
                dto => dto.HasValue ? (long?)dto.Value.UtcTicks : null,
                ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : (DateTimeOffset?)null)
            .IsRequired(false);

        builder.Ignore(e => e.DomainEvents);

        // Results page: scores for an exam, only published ones visible to students
        builder.HasIndex(s => new { s.ExamId, s.IsPublished });
        // Student portal: student's own scores
        builder.HasIndex(s => s.StudentId);
        // Linkback from OCR to score
        builder.HasIndex(s => s.CaptureId);
    }
}
