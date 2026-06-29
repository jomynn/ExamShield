using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

internal sealed class CaptureConfiguration : IEntityTypeConfiguration<Capture>
{
    public void Configure(EntityTypeBuilder<Capture> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new CaptureId(v));

        builder.Property(c => c.ExamId)
            .HasConversion(id => id.Value, v => new ExamId(v));

        builder.Property(c => c.StudentId)
            .HasConversion(id => id.Value, v => new StudentId(v));

        builder.Property(c => c.DeviceId)
            .HasConversion(id => id.Value, v => new DeviceId(v));

        builder.Property(c => c.PageNumber)
            .HasConversion(p => p.Value, v => new PageNumber(v));

        builder.Property(c => c.ExpectedHash)
            .HasConversion(h => h.Hex, v => Hash.FromHex(v))
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(c => c.Signature)
            .HasConversion(s => s.Bytes.ToArray(), v => new Signature(v));

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.StorageKey).HasMaxLength(500).IsRequired(false);

        builder.Property(c => c.EncryptedDek).IsRequired(false);

        builder.Property(c => c.InvigilatorId)
            .HasConversion(id => id!.Value, v => new UserId(v))
            .IsRequired(false);

        builder.Ignore(c => c.DomainEvents);

        // Composite index for the most common list query: captures for an exam, filtered by status
        builder.HasIndex(c => new { c.ExamId, c.Status });
        // Invigilator scope enforcement: Invigilator/Operator list their own captures
        builder.HasIndex(c => c.InvigilatorId);
        // Device history queries
        builder.HasIndex(c => c.DeviceId);
        // Student answer-sheet lookup
        builder.HasIndex(c => c.StudentId);
    }
}
