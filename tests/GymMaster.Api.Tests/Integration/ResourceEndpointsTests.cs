using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration test cho cac controller tai nguyen (members, trainers, packages, check-ins, food-items...).
// Chay that qua HTTP -> kiem tra routing + phan quyen + service tra du lieu.
public class ResourceEndpointsTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ResourceEndpointsTests(GymMasterWebAppFactory factory) => _factory = factory;

    private sealed record ApiEnvelope<T>(bool Success, T? Data);
    private sealed record LoginData(string AccessToken);

    private async Task<HttpClient> ClientAsAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await res.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }

    private Task<HttpClient> AdminAsync() => ClientAsAsync("admin@gymmaster.local", "Admin123!");
    private Task<HttpClient> StaffAsync() => ClientAsAsync("staff@gymmaster.local", "Staff123!");
    private Task<HttpClient> MemberAsync() => ClientAsAsync("member@gymmaster.local", "Member123!");
    private Task<HttpClient> PtAsync() => ClientAsAsync("pt@gymmaster.local", "Pt123!");

    // ---------- Members ----------

    [Fact]
    public async Task Members_list_ok_for_staff()
    {
        var client = await StaffAsync();
        var res = await client.GetAsync("/api/v1/members?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Members_create_ok_for_admin()
    {
        var client = await AdminAsync();
        var email = $"newmem-{Guid.NewGuid():N}@gym.test";

        var res = await client.PostAsJsonAsync("/api/v1/members",
            new { fullName = "Hoi Vien Moi", email, phone = (string?)null, password = (string?)null,
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Members_me_ok_for_member()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/members/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Trainers ----------

    [Fact]
    public async Task Trainers_list_ok_for_admin()
    {
        var client = await AdminAsync();
        var res = await client.GetAsync("/api/v1/trainers?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Trainers_me_ok_for_pt()
    {
        var client = await PtAsync();
        var res = await client.GetAsync("/api/v1/trainers/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Trainers_list_forbidden_for_member()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/trainers");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- Packages ----------

    [Fact]
    public async Task Packages_list_ok_for_any_authenticated()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/packages");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Check-ins ----------

    [Fact]
    public async Task Checkins_list_ok_for_staff()
    {
        var client = await StaffAsync();
        var res = await client.GetAsync("/api/v1/checkins");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Checkin_create_ok_for_member_self()
    {
        var client = await MemberAsync();
        var res = await client.PostAsJsonAsync("/api/v1/checkins", new { });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Checkins_list_forbidden_for_member()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/checkins");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---------- Food items ----------

    [Fact]
    public async Task FoodItems_list_ok_for_member()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/food-items");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Unauthenticated ----------

    [Fact]
    public async Task Members_list_requires_authentication()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/members");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
