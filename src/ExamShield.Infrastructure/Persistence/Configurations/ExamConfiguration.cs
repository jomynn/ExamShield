using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

internal sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Exams");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, v => new ExamId(v));

        builder.Property(e => e.Name).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasConversion<string>();

        builder.Property(e => e.CreatedAt)
            .HasConversion(
                dt => dt.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));

        builder.Property(e => e.ScheduledAt)
            .HasConversion(
                dt => dt.HasValue ? (long?)dt.Value.UtcTicks : null,
                v => v.HasValue ? (DateTimeOffset?)new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        builder.Property(e => e.EndsAt)
            .HasConversion(
                dt => dt.HasValue ? (long?)dt.Value.UtcTicks : null,
                v => v.HasValue ? (DateTimeOffset?)new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        builder.Ignore(e => e.DomainEvents);
    }
}
