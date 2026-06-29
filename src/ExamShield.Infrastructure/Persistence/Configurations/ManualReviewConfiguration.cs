using System.Text.Json;
using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExamShield.Infrastructure.Persistence.Configurations;

public sealed class ManualReviewConfiguration : IEntityTypeConfiguration<ManualReview>
{
    private sealed record ReviewedAnswerJson(int QuestionNumber, string Text);

    private static readonly ValueConverter<IReadOnlyList<ReviewedAnswer>, string> AnswersConverter = new(
        answers => JsonSerializer.Serialize(
            answers.Select(a => new ReviewedAnswerJson(a.QuestionNumber, a.Text)).ToList(),
            (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<ReviewedAnswerJson>>(json, (JsonSerializerOptions?)null)!
            .Select(j => new ReviewedAnswer(j.QuestionNumber, j.Text))
            .ToList<ReviewedAnswer>());

    public void Configure(EntityTypeBuilder<ManualReview> builder)
    {
        builder.ToTable("ManualReviews");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ManualReviewId(v));

        builder.Property(e => e.OcrResultId)
            .HasConversion(id => id.Value, v => new OcrResultId(v));

        builder.Property(e => e.CaptureId)
            .HasConversion(id => id.Value, v => new CaptureId(v));

        builder.Property(e => e.Status)
            .HasConversion<int>();

        builder.Property(e => e.CreatedAt)
            .HasConversion(dto => dto.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        builder.Property(e => e.ReviewedBy)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                v => v == null ? null : new UserId(v.Value))
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .HasConversion(
                dto => dto.HasValue ? (long?)dto.Value.UtcTicks : null,
                ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : (DateTimeOffset?)null)
            .IsRequired(false);

        builder.Property(e => e.SupervisorId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                v => v == null ? null : new UserId(v.Value))
            .IsRequired(false);

        builder.Property(e => e.RejectionReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.EscalationReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(e => e.SupervisedAt)
            .HasConversion(
                dto => dto.HasValue ? (long?)dto.Value.UtcTicks : null,
                ticks => ticks.HasValue ? new DateTimeOffset(ticks.Value, TimeSpan.Zero) : (DateTimeOffset?)null)
            .IsRequired(false);

        builder.Property(e => e.ReviewedAnswers)
            .HasConversion(AnswersConverter)
            .HasColumnType("TEXT")
            .IsRequired(false);

        builder.Ignore(e => e.DomainEvents);

        // Review queue list: reviews by status (Pending, PendingApproval, etc.)
        builder.HasIndex(e => e.Status);
        // Look up the review for a specific capture
        builder.HasIndex(e => e.CaptureId);
    }
}
