using Microsoft.EntityFrameworkCore;
using OppsView.Api.Domain;

namespace OppsView.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
    public DbSet<StatusChange> StatusHistory => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration<T> in this assembly, so each
        // entity's mapping lives in its own file under Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
