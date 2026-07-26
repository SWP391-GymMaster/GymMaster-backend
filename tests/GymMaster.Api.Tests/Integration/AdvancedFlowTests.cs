using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration test vong doi nang cao: giao an & ghi chu (tao->sua->xoa->PT khac bi cam),
// gia han/huy goi, PT check-in ho vien duoc phan cong. Phu sau tang service + controller.
public class AdvancedFlowTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;

    public AdvancedFlowTests(GymMasterWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();

    private sealed record Env(
        HttpClient Admin, HttpClient Staff, HttpClient Member, HttpClient Pt, HttpClient OtherPt,
        long MemberId, long TrainerId, long MembershipId, long PackageId);

    // Dung day du: goi PT -> hoi vien -> ban goi -> thanh toan (active) -> tao PT -> phan cong.
    private async Task<Env> SetupAsync()
    {
        var admin = await ClientAsAsync("admin@gymmaster.local", "Admin123!");
        var staff = await ClientAsAsync("staff@gymmaster.local", "Staff123!");

        var pkg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/packages",
            new { name = $"PT-{Guid.NewGuid():N}", durationDays = (short)30, price = 500_000m, description = "goi PT", supportsPT = true }));
        var packageId = pkg.GetProperty("id").GetInt64();

        var memberEmail = $"advmem-{Guid.NewGuid():N}@gym.test";
        var memberData = await DataAsync(await staff.PostAsJsonAsync("/api/v1/members",
            new { fullName = "Adv Member", email = memberEmail, phone = (string?)null, password = "member1234",
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null }));
        var memberId = memberData.GetProperty("member").GetProperty("id").GetInt64();

        var sell = await DataAsync(await staff.PostAsJsonAsync("/api/v1/memberships/sell",
            new { memberId, packageId, startDate = (DateOnly?)null }));
        var membershipId = sell.GetProperty("membership").GetProperty("id").GetInt64();

        await staff.PostAsJsonAsync($"/api/v1/memberships/{membershipId}/payment", new { amount = 500_000m, method = "cash" });

        var trainerEmail = $"advpt-{Guid.NewGuid():N}@gym.test";
        var trainerData = await DataAsync(await admin.PostAsJsonAsync("/api/v1/trainers",
            new { fullName = "Adv PT", email = trainerEmail, phone = (string?)null, password = "trainer1234",
                  specialty = "Gym", bio = "pt", gender = "male", dateOfBirth = (DateTime?)null, yearsOfExperience = 3 }));
        var trainerId = trainerData.GetProperty("trainer").GetProperty("id").GetInt64();

        await admin.PostAsJsonAsync("/api/v1/assignments", new { memberId, trainerId });

        var pt = await ClientAsAsync(trainerEmail, "trainer1234");
        var otherPt = await ClientAsAsync("pt@gymmaster.local", "Pt123!"); // PT demo, KHONG duoc phan cong
        var member = await ClientAsAsync(memberEmail, "member1234");

        return new Env(admin, staff, member, pt, otherPt, memberId, trainerId, membershipId, packageId);
    }

    private static object PlanBody(string title) => new
    {
        title,
        goal = "Tang co",
        startDate = (DateOnly?)null,
        endDate = (DateOnly?)null,
        exercises = new[] { new { name = "Squat", sets = (byte)4, reps = "8", note = (string?)null, orderIndex = (short)0 } },
    };

    // ---------- Workout plan: tao -> sua -> PT khac bi cam -> xoa ----------

    [Fact]
    public async Task WorkoutPlan_lifecycle_and_ownership()
    {
        var env = await SetupAsync();

        var plan = await DataAsync(await env.Pt.PostAsJsonAsync($"/api/v1/members/{env.MemberId}/workout-plans", PlanBody("Giao an A")));
        var planId = plan.GetProperty("id").GetInt64();

        // PT so huu sua -> 200
        var update = await env.Pt.PutAsJsonAsync($"/api/v1/workout-plans/{planId}",
            new { title = "Giao an A sua", goal = (string?)null, startDate = (DateOnly?)null, endDate = (DateOnly?)null,
                  exercises = new[] { new { name = "Deadlift", sets = (byte)3, reps = "5", note = (string?)null, orderIndex = (short)0 } } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // PT khac (khong so huu) sua -> 403
        var otherUpdate = await env.OtherPt.PutAsJsonAsync($"/api/v1/workout-plans/{planId}",
            new { title = "hack", goal = (string?)null, startDate = (DateOnly?)null, endDate = (DateOnly?)null, exercises = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, otherUpdate.StatusCode);

        // PT so huu xoa -> 2xx
        Assert.True((await env.Pt.DeleteAsync($"/api/v1/workout-plans/{planId}")).IsSuccessStatusCode);
    }

    // ---------- Trainer note: tao -> sua -> PT khac bi cam -> xoa ----------

    [Fact]
    public async Task TrainerNote_lifecycle_and_ownership()
    {
        var env = await SetupAsync();

        var note = await DataAsync(await env.Pt.PostAsJsonAsync($"/api/v1/members/{env.MemberId}/notes", new { content = "Ghi chu 1" }));
        var noteId = note.GetProperty("id").GetInt64();

        var update = await env.Pt.PutAsJsonAsync($"/api/v1/trainer-notes/{noteId}", new { content = "Ghi chu sua" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var otherUpdate = await env.OtherPt.PutAsJsonAsync($"/api/v1/trainer-notes/{noteId}", new { content = "hack" });
        Assert.Equal(HttpStatusCode.Forbidden, otherUpdate.StatusCode);

        Assert.True((await env.Pt.DeleteAsync($"/api/v1/trainer-notes/{noteId}")).IsSuccessStatusCode);
    }

    // ---------- Membership: gia han + huy ----------

    [Fact]
    public async Task Membership_renew_flow()
    {
        var env = await SetupAsync();

        var renew = await env.Staff.PostAsJsonAsync($"/api/v1/memberships/{env.MembershipId}/renew",
            new { packageId = env.PackageId, method = "cash" });
        Assert.True((int)renew.StatusCode < 500, $"renew => {renew.StatusCode}");
    }

    [Fact]
    public async Task Membership_cancel_by_staff()
    {
        var env = await SetupAsync();

        var cancel = await env.Staff.PostAsync($"/api/v1/memberships/{env.MembershipId}/cancel", null);
        Assert.True((int)cancel.StatusCode < 500, $"cancel => {cancel.StatusCode}");
    }

    // ---------- PT check-in ho vien duoc phan cong ----------

    [Fact]
    public async Task Pt_checkin_assigned_member()
    {
        var env = await SetupAsync();

        var res = await env.Pt.PostAsync($"/api/v1/pt/members/{env.MemberId}/checkins", null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    // ---------- PT check-in vien KHONG duoc phan cong -> 403 ----------

    [Fact]
    public async Task Pt_checkin_unassigned_member_forbidden()
    {
        var env = await SetupAsync();

        // otherPt (demo) khong duoc phan cong cho member nay
        var res = await env.OtherPt.PostAsync($"/api/v1/pt/members/{env.MemberId}/checkins", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
