var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

// Makes the compiler-generated Program class visible to the test project,
// which needs it for WebApplicationFactory<Program>.
public partial class Program;
