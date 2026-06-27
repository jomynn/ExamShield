using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamShield.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, v => new DeviceId(v));

        builder.Property(d => d.Name).HasMaxLength(200);

        builder.Property(d => d.PublicKey)
            .HasConversion(
                pk => pk.Bytes.ToArray(),
                v => new PublicKey(v));

        builder.Property(d => d.RegisteredAt)
            .HasConversion(
                dt => dt.UtcTicks,
                v => new DateTimeOffset(v, TimeSpan.Zero));

        builder.Property(d => d.Status);

        builder.Ignore(d => d.IsActive);
        builder.Ignore(d => d.DomainEvents);
    }
}
