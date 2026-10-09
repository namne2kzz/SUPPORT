using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="IngestionRun"/> to <c>kb_ingestion_run</c>.</summary>
internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("kb_ingestion_run");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Product).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Trigger).HasMaxLength(20).IsRequired();

        builder.HasIndex(r => new { r.Product, r.StartedAt }).IsDescending(false, true);
    }
}
