using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class ImportedEmailConfiguration : IEntityTypeConfiguration<ImportedEmail>
{
    public void Configure(EntityTypeBuilder<ImportedEmail> builder)
    {
        builder.ToTable("ImportedEmails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.MessageId).HasMaxLength(ImportedEmail.MessageIdMaxLength).IsRequired();
        builder.Property(e => e.Sender).HasMaxLength(ImportedEmail.SenderMaxLength).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(ImportedEmail.SubjectMaxLength).IsRequired();
        builder.Property(e => e.Company).HasMaxLength(JobApplication.CompanyMaxLength);
        builder.Property(e => e.Role).HasMaxLength(JobApplication.RoleMaxLength);
        builder.Property(e => e.Reason).HasMaxLength(ImportedEmail.ReasonMaxLength);

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Platform).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting an application keeps the email record (so the email isn't
        // re-imported on the next sync); it just loses the link.
        builder.HasOne<JobApplication>()
            .WithMany()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        // The database itself guarantees an email is processed once per user.
        builder.HasIndex(e => new { e.UserId, e.MessageId }).IsUnique();
        // The "Needs review" list.
        builder.HasIndex(e => new { e.UserId, e.Outcome });
    }
}
