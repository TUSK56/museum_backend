using Microsoft.Extensions.Configuration;
using Npgsql;

namespace VirtualMuseum.Infrastructure.Data;

public static class DatabaseConnectionResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var databaseUrl = ResolveDatabaseUrl(configuration);

        string raw;
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            raw = FromPostgresUrl(databaseUrl);
        }
        else
        {
            raw = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Database is not configured. Set DATABASE_URL, DATABASE_PUBLIC_URL (Railway on Heroku), " +
                    "or ConnectionStrings__DefaultConnection.");
        }

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            raw = FromPostgresUrl(raw);
        }

        return Normalize(raw);
    }

    private static string? ResolveDatabaseUrl(IConfiguration configuration)
    {
        // Railway → Heroku: must use DATABASE_PUBLIC_URL (zephyr.proxy.rlwy.net), not postgres.railway.internal.
        if (IsHeroku)
        {
            return FirstNonEmpty(
                Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"),
                configuration["DATABASE_PUBLIC_URL"],
                Environment.GetEnvironmentVariable("DATABASE_URL"),
                configuration["DATABASE_URL"],
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                configuration.GetConnectionString("DefaultConnection"));
        }

        return FirstNonEmpty(
            Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL"),
            Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL"),
            Environment.GetEnvironmentVariable("DATABASE_URL"),
            configuration["DATABASE_PRIVATE_URL"],
            configuration["DATABASE_PUBLIC_URL"],
            configuration["DATABASE_URL"]);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    public static string DescribeHost(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return $"{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch
        {
            return "(invalid connection string)";
        }
    }

    private static string FromPostgresUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        if (uri.Scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException(
                $"Unsupported database URL scheme '{uri.Scheme}'. Expected postgres or postgresql.");
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
            Password = password
        };

        return Normalize(builder.ConnectionString);
    }

    public static bool IsHeroku =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DYNO"));

    public static bool IsLocalHost(string connectionString)
    {
        try
        {
            var host = new NpgsqlConnectionStringBuilder(connectionString).Host?.Trim().ToLowerInvariant();
            return host is "localhost" or "127.0.0.1" or "::1";
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRailwayInternalHost(string connectionString)
    {
        try
        {
            var host = new NpgsqlConnectionStringBuilder(connectionString).Host;
            return host?.Contains("railway.internal", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string connectionString)
    {
        var source = new NpgsqlConnectionStringBuilder(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = RequiresSsl(source.Host) ? 15 : 60,
            CommandTimeout = 120,
            KeepAlive = 30,
            Pooling = true
        };

        if (RequiresSsl(builder.Host))
            builder.SslMode = SslMode.Require;
        else
            builder.SslMode = SslMode.Prefer;

        var result = builder.ConnectionString;

        // Railway public proxy (*.rlwy.net) — matches appsettings.Production.example.json guidance.
        if (IsRailwayProxyHost(builder.Host)
            && !result.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
        {
            result += ";Trust Server Certificate=true";
        }

        return result;
    }

    private static bool IsRailwayProxyHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var normalized = host.Trim().ToLowerInvariant();
        return normalized.EndsWith(".rlwy.net", StringComparison.Ordinal)
            || normalized.Contains("proxy.rlwy.net", StringComparison.Ordinal);
    }

    private static bool RequiresSsl(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var normalized = host.Trim().ToLowerInvariant();
        return normalized is not ("localhost" or "127.0.0.1" or "::1")
            && !normalized.EndsWith(".local", StringComparison.Ordinal);
    }
}
