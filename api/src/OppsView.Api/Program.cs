using Microsoft.EntityFrameworkCore;
using OppsView.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----------------------------------------------------------
// Locally the connection string comes from user-secrets; in CI/Azure from the
// ConnectionStrings__Default environment variable. Never from appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "Connection string 'Default' is missing. See api/README.md for how to set it with dotnet user-secrets.");
    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.Run();

// Makes the compiler-generated Program class visible to the test project,
// which needs it for WebApplicationFactory<Program>.
public partial class Program;
