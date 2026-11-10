using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Company).HasMaxLength(JobApplication.CompanyMaxLength).IsRequired();
        builder.Property(a => a.Role).HasMaxLength(JobApplication.RoleMaxLength).IsRequired();
        builder.Property(a => a.JobUrl).HasMaxLength(JobApplication.JobUrlMaxLength);
        builder.Property(a => a.Notes).HasMaxLength(JobApplication.NotesMaxLength);

        // Enums as text ("Interview", not 2): readable in SQL, and reordering
        // or inserting enum members can't silently corrupt existing rows.
        builder.Property(a => a.Platform).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Every query is "this user's applications, newest first".
        builder.HasIndex(a => new { a.UserId, a.AppliedOn });

        // History is exposed as IReadOnlyList; EF reads and writes the private
        // _history field behind it (found by naming convention).
        builder.HasMany(a => a.History)
            .WithOne()
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
