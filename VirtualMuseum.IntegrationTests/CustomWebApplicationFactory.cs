using Microsoft.AspNetCore.Mvc.Testing;

namespace VirtualMuseum.IntegrationTests;

/// <summary>
/// Uses PostgreSQL from appsettings.json or DATABASE_URL. Stop any running API instance before: dotnet test
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
}
