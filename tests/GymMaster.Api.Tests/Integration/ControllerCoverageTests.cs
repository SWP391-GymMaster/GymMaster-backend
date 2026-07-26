using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration smoke test phu NOT cac controller con lai: request that di qua routing + auth + controller.
// Muc tieu: moi action chay it nhat 1 lan (routing/authorize/binding), khong nem 500.
public class ControllerCoverageTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ControllerCoverageTests(GymMasterWebAppFactory factory) => _factory = factory;

    private sealed record ApiEnvelope<T>(bool Success, T? Data);
    private sealed record LoginData(string AccessToken);
    private sealed record MemberData(long Id);

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

    private async Task<long> MyMemberIdAsync(HttpClient memberClient)
    {
        var res = await memberClient.GetAsync("/api/v1/members/me");
        var body = await res.Content.ReadFromJsonAsync<ApiEnvelope<MemberData>>(Json);
        return body!.Data!.Id;
    }

    // Da xu ly boi controller (khong bi chan o auth, khong loi he thong).
    private static void AssertHandled(HttpResponseMessage res)
        => Assert.True(
            res.StatusCode != HttpStatusCode.Unauthorized && res.StatusCode != HttpStatusCode.InternalServerError,
            $"Unexpected status {(int)res.StatusCode} {res.StatusCode}");

    // ---------- Admin-only GET ----------

    [Fact]
    public async Task AuditLogs_list_ok_for_admin()
    {
        var client = await AdminAsync();
        var res = await client.GetAsync("/api/v1/audit-logs");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Payments_list_and_summary_ok_for_admin()
    {
        var client = await AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/payments/summary")).StatusCode);
    }

    [Fact]
    public async Task Memberships_list_ok_for_staff()
    {
        var client = await StaffAsync();
        var res = await client.GetAsync("/api/v1/memberships");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Assignment_candidates_ok_for_admin()
    {
        var client = await AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/assignments/candidates/members")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/assignments/candidates/trainers")).StatusCode);
    }

    // ---------- Exercises (any authenticated) ----------

    [Fact]
    public async Task Exercises_list_ok()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/exercises");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- Account /users/me/profile ----------

    [Fact]
    public async Task Account_profile_ok_for_staff()
    {
        var client = await StaffAsync();
        var res = await client.GetAsync("/api/v1/users/me/profile");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Account_update_me_ok_for_member()
    {
        var client = await MemberAsync();
        var res = await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Ten Cap Nhat", phone = (string?)null });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---------- PT ----------

    [Fact]
    public async Task Pt_members_and_today_checkins_ok_for_pt()
    {
        var client = await PtAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/pt/members")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/pt/checkins/today")).StatusCode);
    }

    // ---------- Member-scoped GET (dung member id cua chinh member demo) ----------

    [Fact]
    public async Task Member_memberships_and_payments_handled()
    {
        var member = await MemberAsync();
        var id = await MyMemberIdAsync(member);

        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/memberships"));
        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/payments"));
    }

    [Fact]
    public async Task Member_progress_and_profile360_handled()
    {
        var member = await MemberAsync();
        var id = await MyMemberIdAsync(member);

        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/progress"));
        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/profile-360"));
    }

    [Fact]
    public async Task Member_calorie_endpoints_handled()
    {
        var member = await MemberAsync();
        var id = await MyMemberIdAsync(member);

        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/calorie-target"));
        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/calorie-summary"));
        AssertHandled(await member.GetAsync($"/api/v1/members/{id}/calorie-history"));
    }

    [Fact]
    public async Task MealLogs_list_handled_for_member()
    {
        var member = await MemberAsync();
        AssertHandled(await member.GetAsync("/api/v1/meal-logs"));
    }

    // ---------- PUT/DELETE tren id khong ton tai -> controller chay, tra 404 ----------

    [Fact]
    public async Task WorkoutPlan_update_nonexistent_returns_404_for_pt()
    {
        var client = await PtAsync();
        var res = await client.PutAsJsonAsync("/api/v1/workout-plans/999999",
            new { title = "x", description = (string?)null, startDate = (DateTime?)null, endDate = (DateTime?)null,
                  exercises = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task TrainerNote_delete_nonexistent_returns_404_for_pt()
    {
        var client = await PtAsync();
        var res = await client.DeleteAsync("/api/v1/trainer-notes/999999");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ---------- VnPay return khong co chu ky -> 400 ----------

    [Fact]
    public async Task VnPay_return_without_signature_is_handled()
    {
        var client = await MemberAsync();
        var res = await client.GetAsync("/api/v1/payments/vnpay/return");
        AssertHandled(res);
    }
}
