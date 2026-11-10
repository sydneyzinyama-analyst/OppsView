using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class StatusChangeConfiguration : IEntityTypeConfiguration<StatusChange>
{
    public void Configure(EntityTypeBuilder<StatusChange> builder)
    {
        builder.ToTable("StatusHistory");
        builder.HasKey(h => h.Id);
        // Important with client-generated Guids: without this, EF assumes a
        // non-empty key means "already in the database" and issues an UPDATE
        // instead of an INSERT when a new history row is added to a tracked
        // application.
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
    }
}
