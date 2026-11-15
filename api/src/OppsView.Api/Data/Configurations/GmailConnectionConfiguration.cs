using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class GmailConnectionConfiguration : IEntityTypeConfiguration<GmailConnection>
{
    public void Configure(EntityTypeBuilder<GmailConnection> builder)
    {
        builder.ToTable("GmailConnections");
        // One connection per user, so the user id is the key.
        builder.HasKey(c => c.UserId);
        builder.Property(c => c.UserId).ValueGeneratedNever();
        builder.Property(c => c.EmailAddress).HasMaxLength(GmailConnection.EmailMaxLength).IsRequired();
        builder.Property(c => c.EncryptedRefreshToken).IsRequired();

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<GmailConnection>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
