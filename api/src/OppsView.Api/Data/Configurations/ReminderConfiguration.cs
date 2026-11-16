using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OppsView.Api.Domain;

namespace OppsView.Api.Data.Configurations;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever(); // the notifier's id
        builder.Property(r => r.Kind).HasMaxLength(Reminder.KindMaxLength).IsRequired();
        builder.Property(r => r.Message).HasMaxLength(Reminder.MessageMaxLength).IsRequired();
        builder.Property(r => r.State).HasConversion<string>().HasMaxLength(16);
        // Reminders go with their application (and so with the user's account).
        builder.HasOne<JobApplication>().WithMany().HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.UserId, r.State });
    }
}

public class ReminderSettingsConfiguration : IEntityTypeConfiguration<ReminderSettings>
{
    public void Configure(EntityTypeBuilder<ReminderSettings> builder)
    {
        builder.ToTable("ReminderSettings");
        builder.HasKey(s => s.UserId);
        builder.Property(s => s.UserId).ValueGeneratedNever();
        builder.HasOne<User>().WithOne().HasForeignKey<ReminderSettings>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
