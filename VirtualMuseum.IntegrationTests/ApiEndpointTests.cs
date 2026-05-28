using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace VirtualMuseum.IntegrationTests;

public class ApiEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Auth_Login_WithValidCredentials_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "admin@museum.com", password = "admin@123" });
        Assert.True(response.IsSuccessStatusCode, $"Expected 200, got {(int)response.StatusCode}");
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("success", json);
    }

    [Fact]
    public async Task Auth_Login_WithInvalidCredentials_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "invalid@test.com", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auth_Login_BeforeEmailVerified_Returns403_WithMessage()
    {
        var email = $"unverified{Guid.NewGuid():N}@test.com";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Unverified User",
            email,
            region = "Test",
            password = "Test@123"
        });
        Assert.True(reg.IsSuccessStatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test@123" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        var body = await login.Content.ReadAsStringAsync();
        Assert.Contains("verify", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Auth_Login_WithNullBody_Returns400()
    {
        var response = await _client.PostAsync("/api/auth/login", new StringContent("", Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Auth_Register_WithValidData_Returns201()
    {
        var uniqueEmail = $"user{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test User",
            email = uniqueEmail,
            region = "Test Region",
            password = "Test@123"
        });
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Auth_Register_WithDuplicateEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test",
            email = "admin@museum.com",
            region = "Test",
            password = "Test@123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Artifacts_GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/artifacts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Categories_GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Eras_GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/eras");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Materials_GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/materials");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Tags_GetAll_Returns200()
    {
        var response = await _client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Artifacts_GetById_WithInvalidId_Returns404()
    {
        var response = await _client.GetAsync("/api/artifacts/00000000-0000-0000-0000-000000000000");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Swagger_Loads_Returns200()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Auth_ForgotPasswordRequest_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password/request", new { email = "admin@museum.com" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNever500(response);
    }

    [Fact]
    public async Task Eras_Create_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/eras", new { name = "Test Era", startYear = 1, endYear = 2 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Eras_Create_AsAdmin_Returns201()
    {
        var token = await GetAccessTokenAsync("admin@museum.com", "admin@123");
        Assert.False(string.IsNullOrEmpty(token));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/eras");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { name = $"Era-{Guid.NewGuid():N}", startYear = 100, endYear = 200 });
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Eras_Create_AsUser_Returns403()
    {
        var (email, password) = await RegisterVerifiedUserAsync();
        var token = await GetAccessTokenAsync(email, password);
        Assert.False(string.IsNullOrEmpty(token));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/eras");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { name = "Should Fail", startYear = 1, endYear = 2 });
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Users_GetAll_AsAdmin_Returns200()
    {
        var token = await GetAccessTokenAsync("admin@museum.com", "admin@123");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Users_GetAll_AsUser_Returns403()
    {
        var (email, password) = await RegisterVerifiedUserAsync();
        var token = await GetAccessTokenAsync(email, password);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<string?> GetAccessTokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
            return null;
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;
        return data.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
    }

    private async Task<(string Email, string Password)> RegisterVerifiedUserAsync()
    {
        var email = $"user{Guid.NewGuid():N}@test.com";
        const string password = "Test@123";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Role Test User",
            email,
            region = "Test",
            password
        });
        reg.EnsureSuccessStatusCode();

        var otpResp = await _client.PostAsJsonAsync("/api/auth/send-otp", new { email });
        otpResp.EnsureSuccessStatusCode();
        var otpJson = await otpResp.Content.ReadAsStringAsync();
        using var otpDoc = JsonDocument.Parse(otpJson);
        var code = otpDoc.RootElement.GetProperty("data").GetProperty("code").GetString()
                   ?? throw new InvalidOperationException("Expected OTP code in response (Smtp:Enabled must be false for tests).");
        var verify = await _client.PostAsJsonAsync("/api/auth/verify-otp", new { email, code });
        verify.EnsureSuccessStatusCode();
        return (email, password);
    }

    private static void AssertNever500(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
