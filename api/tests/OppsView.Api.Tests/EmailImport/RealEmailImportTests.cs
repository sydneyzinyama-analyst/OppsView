using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OppsView.Api.Data;
using OppsView.Api.Features.EmailImport;
using OppsView.Api.Features.EmailImport.Parsing;

namespace OppsView.Api.Tests.EmailImport;

/// <summary>
/// The real sample emails through the real parser and importer together:
/// what would actually happen to a user's data.
/// </summary>
public sealed class RealEmailImportTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Guid _userId;

    public RealEmailImportTests()
    {
        _connection.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
        var user = User.Create("me@example.com", Now);
        user.SetPasswordHash("hash");
        db.Users.Add(user);
        // Applied to BBD by hand before connecting Gmail.
        db.Applications.Add(JobApplication.Create(user.Id, "BBD Software", "Java Engineer", JobPlatform.CompanyPortal,
            DateOnly.FromDateTime(Now.AddDays(-20)), null, null, Now.AddDays(-20)));
        db.SaveChanges();
        _userId = user.Id;
    }

    public void Dispose() => _connection.Dispose();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static IncomingEmail Sample(string file, int daysAgo)
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "EmailImport", "Samples", file));
        return new IncomingEmail(file, lines[0][5..].Trim(), lines[1][8..].Trim(), string.Join('\n', lines.Skip(3)), Now.AddDays(-daysAgo));
    }

    [Fact]
    public async Task Imports_the_sample_inbox_sensibly()
    {
        await using (var db = NewDb())
        {
            var summary = await new EmailImporter(db, new RuleBasedEmailParser(), new FakeTimeProvider(Now))
                .ImportAsync(_userId,
                [
                    Sample("discovery-received.txt", 5),
                    Sample("futuresoft-received.txt", 4),
                    Sample("pnet-received.txt", 3),
                    Sample("bbd-rejected.txt", 2),
                    Sample("cartrack-rejected.txt", 1),
                ], CancellationToken.None);

            // Discovery created. FutureSoft (no job title) and Pnet (no company)
            // created with a placeholder for the missing detail. BBD (typed in
            // by hand as "BBD Software") rejected. Cartrack's rejection is for
            // a job OppsView never saw: created, already Rejected. Nothing to review.
            Assert.Equal(new ImportSummary(Created: 4, Updated: 1, NoChange: 0, Ignored: 0, NeedsReview: 0, AlreadyProcessed: 0), summary);
        }

        await using var check = NewDb();
        var apps = await check.Applications.AsNoTracking().ToListAsync();
        var discovery = apps.Single(a => a.Company == "Discovery");
        Assert.Equal(("Developer (Junior)", ApplicationSource.Email), (discovery.Role, discovery.Source));
        Assert.Equal(ApplicationStatus.Rejected, apps.Single(a => a.Company == "BBD Software").Status);

        Assert.Contains(apps, a => a.Company == "FutureSoft" && a.Role == EmailImporter.UnknownRole);
        Assert.Contains(apps, a => a.Company == EmailImporter.UnknownCompany && a.Role == "Junior AI & Full-Stack Developer");

        var cartrack = apps.Single(a => a.Company == "Cartrack");
        Assert.Equal(("IT Support Technician - Tier 1", ApplicationStatus.Rejected), (cartrack.Role, cartrack.Status));
        Assert.False(await check.ImportedEmails.AnyAsync(e => e.Outcome == ImportOutcome.NeedsReview));
    }
}
