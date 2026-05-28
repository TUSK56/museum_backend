using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VirtualMuseum.API.Middleware;
using VirtualMuseum.Application.Interfaces;
using VirtualMuseum.Application.Services;
using VirtualMuseum.Infrastructure.Data;
using VirtualMuseum.Infrastructure.Repositories;
using VirtualMuseum.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway (and similar platforms) inject PORT; must listen on 0.0.0.0, not a fixed 8080.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Database - Scoped lifetime (default for AddDbContext)
var connectionString = ResolveConnectionString(builder.Configuration);

builder.Services.AddDbContext<MuseumDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    // Use a writable per-app directory by default across hosting environments.
    dataProtectionKeyPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
}

Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services
    .AddDataProtection()
    .SetApplicationName("VirtualMuseum.API")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IArtifactRepository, ArtifactRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPendingUserRegistrationRepository, PendingUserRegistrationRepository>();

// Services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ArtifactService>();
builder.Services.AddScoped<EraService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<MaterialService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<UserService>();

builder.Services.AddHttpClient("N8n", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Key (or Jwt:Secret) is not configured in appsettings.json");
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters for production safety.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Map JWT "role" claim to ClaimTypes.Role so [Authorize(Roles = "Admin")] works reliably.
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length == 0)
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                return;
            }

            throw new InvalidOperationException("Cors:Origins must be configured in non-development environments.");
        }

        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

// Controllers with validation
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "3D Virtual Museum API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
var enableSwagger =
    builder.Configuration.GetValue<bool?>("Swagger:Enabled")
    ?? true;

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Shared hosts / reverse proxies often don't show up as "known".
    // Clearing these allows forwarded headers to be processed.
    KnownNetworks = { },
    KnownProxies = { }
});

// Global exception handling - must be first
app.UseMiddleware<ExceptionHandlingMiddleware>();

var runMigrationsOnStartup =
    builder.Configuration.GetValue<bool?>("Database:RunMigrationsOnStartup")
    ?? true;
// Database connection validation and migration (enabled by default for Railway deployments)
if (runMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var context = scope.ServiceProvider.GetRequiredService<MuseumDbContext>();
    startupLogger.LogInformation(
        "Database startup options: RunMigrationsOnStartup={RunMigrationsOnStartup}",
        runMigrationsOnStartup);
    try
    {
        const int maxRetries = 12;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                startupLogger.LogInformation("Attempting database migration... (Attempt {Attempt}/{MaxRetries})", attempt, maxRetries);
                await context.Database.MigrateAsync();
                await DatabaseSeeder.SeedAsync(context);
                startupLogger.LogInformation("Database migrations and seeding completed successfully.");
                break;
            }
            catch (Exception) when (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch
            {
                startupLogger.LogError("Database migration failed after retries. Verify PostgreSQL is reachable and ConnectionStrings:DefaultConnection or DATABASE_URL is set.");
                throw new InvalidOperationException("Unable to initialize database.");
            }
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Database initialization failed: {Message}. Set ConnectionStrings__DefaultConnection or DATABASE_URL (Railway).", ex.Message);
        throw;
    }
}
else
{
    app.Logger.LogInformation("Skipping database migration on startup. Set Database:RunMigrationsOnStartup=true to enable.");
}

static string ResolveConnectionString(IConfiguration configuration)
{
    var databaseUrl =
        Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        return ConvertDatabaseUrlToNpgsql(databaseUrl);
    }

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");
    }

    if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return ConvertDatabaseUrlToNpgsql(connectionString);
    }

    return connectionString;
}

static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    if (uri.Scheme is not ("postgres" or "postgresql"))
    {
        throw new InvalidOperationException($"Unsupported DATABASE_URL scheme '{uri.Scheme}'. Expected postgres or postgresql.");
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var database = uri.AbsolutePath.TrimStart('/');
    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = string.IsNullOrWhiteSpace(database) ? "railway" : database,
        Username = username,
        Password = password,
        SslMode = SslMode.Require
    };

    return builder.ConnectionString;
}

// Lightweight probe for Railway healthchecks (no database required).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // Relative path works even when hosted under an IIS virtual directory / PathBase.
        c.SwaggerEndpoint("v1/swagger.json", "3D Virtual Museum API v1");
    });
}
// Railway terminates TLS at the edge; redirecting HTTP healthchecks to HTTPS breaks probes.
var behindProxy = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PORT"));
if (!app.Environment.IsDevelopment() && !behindProxy)
{
    app.UseHsts();
}

if (!behindProxy)
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Required for integration tests
public partial class Program { }
