using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
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
var connectionString = DatabaseConnectionResolver.Resolve(builder.Configuration);
if (DatabaseConnectionResolver.IsHeroku && DatabaseConnectionResolver.IsLocalHost(connectionString))
{
    throw new InvalidOperationException(
        "Heroku: no cloud database configured. From Railway → Postgres → Variables, copy DATABASE_PUBLIC_URL and run: " +
        "heroku config:set DATABASE_URL=\"<paste DATABASE_PUBLIC_URL>\" -a YOUR_APP_NAME");
}

if (DatabaseConnectionResolver.IsHeroku && DatabaseConnectionResolver.IsRailwayInternalHost(connectionString))
{
    throw new InvalidOperationException(
        "Heroku cannot use Railway's internal host (postgres.railway.internal). " +
        "Set DATABASE_URL to DATABASE_PUBLIC_URL from Railway (host ends with .proxy.rlwy.net).");
}

builder.Services.AddDbContext<MuseumDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
        npgsql.MigrationsAssembly(typeof(MuseumDbContext).Assembly.FullName);
    });
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

        policy
            .SetIsOriginAllowed(origin =>
            {
                if (corsOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                    return true;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                if (uri.Host.EndsWith(".herokuapp.com", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (uri.Host.EndsWith(".netlify.app", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (uri.Host.Equals("egyptianmuseum.me", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("www.egyptianmuseum.me", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
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
app.Logger.LogInformation("PostgreSQL target: {DatabaseHost}", DatabaseConnectionResolver.DescribeHost(connectionString));
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
        // Heroku kills the dyno if boot takes too long; avoid long retry loops without a database.
        var maxRetries = DatabaseConnectionResolver.IsHeroku ? 3 : 12;
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
            catch (Exception ex) when (attempt < maxRetries)
            {
                startupLogger.LogWarning(ex, "Database migration attempt {Attempt} failed, retrying in 5s...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                startupLogger.LogError(ex, "Database migration failed after {MaxRetries} attempts.", maxRetries);
                if (DatabaseConnectionResolver.IsHeroku)
                {
                    startupLogger.LogWarning(
                        "Heroku: continuing startup without migrations so /health and API can respond. Fix DATABASE_URL.");
                    break;
                }
                throw new InvalidOperationException("Unable to initialize database. See logs for details.", ex);
            }
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Database initialization failed: {Message}. Set ConnectionStrings__DefaultConnection or DATABASE_URL (Railway).", ex.Message);
        if (!DatabaseConnectionResolver.IsHeroku)
            throw;
    }
}
else
{
    app.Logger.LogInformation("Skipping database migration on startup. Set Database:RunMigrationsOnStartup=true to enable.");
}

// Lightweight probe (no database).
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
