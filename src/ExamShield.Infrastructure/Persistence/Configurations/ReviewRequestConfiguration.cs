using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

public sealed class ReviewRequestConfiguration : IEntityTypeConfiguration<ReviewRequest>
{
    public void Configure(EntityTypeBuilder<ReviewRequest> builder)
    {
        builder.ToTable("ReviewRequests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ReviewRequestId(v));

        builder.Property(e => e.StudentId)
            .HasConversion(id => id.Value, v => new StudentId(v));

        builder.Property(e => e.CaptureId)
            .HasConversion(id => id.Value, v => new CaptureId(v));

        builder.Property(e => e.Reason)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>();

        builder.Property(e => e.CreatedAt)
            .HasConversion(dto => dto.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        builder.Ignore(e => e.DomainEvents);
    }
}
