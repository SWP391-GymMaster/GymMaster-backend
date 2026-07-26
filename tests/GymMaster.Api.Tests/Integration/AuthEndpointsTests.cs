using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration test THAT: request HTTP di xuyen middleware -> routing -> controller ->
// service -> DbContext (InMemory). Kiem tra ca xac thuc JWT va phan quyen theo role.
public class AuthEndpointsTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointsTests(GymMasterWebAppFactory factory) => _factory = factory;

    // ---- Cac shape JSON toi thieu de doc response ----
    private sealed record ApiEnvelope<T>(bool Success, T? Data, ApiErr? Error);
    private sealed record ApiErr(string Code, string Message);
    private sealed record LoginData(string AccessToken, string RefreshToken, MeData User, string Role);
    private sealed record MeData(long UserId, string Email, string FullName, string Role);

    private static async Task<(HttpStatusCode Status, ApiEnvelope<T>? Body)> ReadAsync<T>(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<ApiEnvelope<T>>(Json);
        return (res.StatusCode, body);
    }

    private async Task<string> LoginAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var (status, body) = await ReadAsync<LoginData>(res);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Success);
        return body.Data!.AccessToken;
    }

    private static void Authorize(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ---------- Root ----------

    [Fact]
    public async Task Root_endpoint_is_up()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_with_seeded_admin_returns_token()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@gymmaster.local", password = "Admin123!" });
        var (status, body) = await ReadAsync<LoginData>(res);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Success);
        Assert.False(string.IsNullOrEmpty(body.Data!.AccessToken));
        Assert.Equal("admin", body.Data.Role);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "admin@gymmaster.local", password = "SaiMatKhau!" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var (_, body) = await ReadAsync<object>(res);
        Assert.False(body!.Success);
    }

    // ---------- /auth/me (yeu cau JWT) ----------

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_token_returns_current_user()
    {
        var client = _factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client, "admin@gymmaster.local", "Admin123!");
        Authorize(client, token);

        var res = await client.GetAsync("/api/v1/auth/me");
        var (status, body) = await ReadAsync<MeData>(res);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("admin@gymmaster.local", body!.Data!.Email);
    }

    [Fact]
    public async Task Me_with_garbage_token_returns_401()
    {
        var client = _factory.CreateClient();
        Authorize(client, "khong-phai-jwt-hop-le");

        var res = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ---------- Phan quyen theo role (RBAC) ----------

    [Fact]
    public async Task Dashboard_summary_allowed_for_admin()
    {
        var client = _factory.CreateClient();
        Authorize(client, await LoginAndGetTokenAsync(client, "admin@gymmaster.local", "Admin123!"));

        var res = await client.GetAsync("/api/v1/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Dashboard_summary_forbidden_for_member()
    {
        var client = _factory.CreateClient();
        Authorize(client, await LoginAndGetTokenAsync(client, "member@gymmaster.local", "Member123!"));

        var res = await client.GetAsync("/api/v1/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Users_list_forbidden_for_member()
    {
        var client = _factory.CreateClient();
        Authorize(client, await LoginAndGetTokenAsync(client, "member@gymmaster.local", "Member123!"));

        var res = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Users_list_allowed_for_admin()
    {
        var client = _factory.CreateClient();
        Authorize(client, await LoginAndGetTokenAsync(client, "admin@gymmaster.local", "Admin123!"));

        var res = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Luong dang ky -> dang nhap -> me ----------

    [Fact]
    public async Task Register_then_login_then_me_full_flow()
    {
        var client = _factory.CreateClient();
        var email = $"newuser-{Guid.NewGuid():N}@gym.test";

        // Dang ky
        var register = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Nguoi Moi", email, phone = (string?)null, password = "matkhau123" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Dang nhap bang tai khoan vua tao
        var token = await LoginAndGetTokenAsync(client, email, "matkhau123");

        // Goi /me
        Authorize(client, token);
        var me = await client.GetAsync("/api/v1/auth/me");
        var (status, body) = await ReadAsync<MeData>(me);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(email, body!.Data!.Email);
        Assert.Equal("member", body.Data.Role);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();

        var res = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { fullName = "Trung", email = "admin@gymmaster.local", phone = (string?)null, password = "matkhau123" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
