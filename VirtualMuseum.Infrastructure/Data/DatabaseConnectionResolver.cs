using Microsoft.Extensions.Configuration;
using Npgsql;

namespace VirtualMuseum.Infrastructure.Data;

public static class DatabaseConnectionResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var databaseUrl =
            Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration["DATABASE_URL"]
            ?? configuration["DATABASE_PRIVATE_URL"];

        string raw;
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            raw = FromPostgresUrl(databaseUrl);
        }
        else
        {
            raw = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Database is not configured. Set ConnectionStrings:DefaultConnection, DATABASE_URL, or ConnectionStrings__DefaultConnection in web.config / environment.");
        }

        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            raw = FromPostgresUrl(raw);
        }

        return Normalize(raw);
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

    private static string Normalize(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 60,
            CommandTimeout = 120,
            KeepAlive = 30,
            Pooling = true
        };

        // Railway/cloud Postgres requires SSL; local Docker/dev often does not.
        if (RequiresSsl(builder.Host))
            builder.SslMode = SslMode.Require;
        else
            builder.SslMode = SslMode.Prefer;

        return builder.ConnectionString;
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
