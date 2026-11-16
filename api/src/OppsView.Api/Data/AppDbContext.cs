using Microsoft.EntityFrameworkCore;
using OppsView.Api.Domain;
using OppsView.Api.Infrastructure.Messaging;

namespace OppsView.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<StatusChange> StatusHistory => Set<StatusChange>();
    public DbSet<ImportedEmail> ImportedEmails => Set<ImportedEmail>();
    public DbSet<GmailConnection> GmailConnections => Set<GmailConnection>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderSettings> ReminderSettings => Set<ReminderSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in this assembly, so each
        // entity's mapping lives in its own file under Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
