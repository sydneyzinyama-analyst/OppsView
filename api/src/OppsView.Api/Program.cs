using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OppsView.Api.Data;
using OppsView.Api.Domain;
using OppsView.Api.Features.Applications;
using OppsView.Api.Features.Auth;
using OppsView.Api.Infrastructure;

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

// ---- Auth --------------------------------------------------------------
// Options pattern + validation: the app refuses to start if the JWT settings
// are missing or the signing key is too short, instead of failing on the
// first login.
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configure the JwtBearer handler from the same validated JwtOptions the
// token service uses, so issuing and validating can't disagree.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwt, time) =>
    {
        // Keep claim names as they are in the token ("sub", "email") instead
        // of mapping them to long WS-* URIs.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // The default validator reads DateTime.UtcNow directly. Checking
            // expiry against the injected TimeProvider means issuing
            // (TokenService) and validating use the same clock, in production
            // and in tests.
            LifetimeValidator = (notBefore, expires, _, parameters) =>
                JwtLifetime.IsValid(notBefore, expires, time.GetUtcNow().UtcDateTime, parameters.ClockSkew),
        };
    });
builder.Services.AddAuthorization();

// ---- App services ------------------------------------------------------
// TimeProvider (new in .NET 8) instead of DateTime.UtcNow, so tests can fake time.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
// Scoped = one instance per HTTP request, the same lifetime as the DbContext they use.
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// ---- HTTP / JSON -------------------------------------------------------
builder.Services
    .AddControllers(options =>
    {
        // Validation errors use the JSON property names ("appliedOn"), not the
        // C# ones ("AppliedOn"), so the client can map them to form fields.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
        options.Filters.Add<DomainExceptionFilter>();
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807 problem+json bodies for errors (500s, 404s, ...).
builder.Services.AddProblemDetails();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "OppsView API", Version = "v1" });
    // Adds the "Authorize" button: paste a token from /auth/login.
    var scheme = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

var app = builder.Build();

// ---- Pipeline (order matters) ------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Makes the compiler-generated Program class visible to the test project,
// which needs it for WebApplicationFactory<Program>.
public partial class Program;
