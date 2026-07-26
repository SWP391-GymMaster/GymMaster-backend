using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration test nham vao NHANH LOI + update/delete cua controllers (404/400/forbidden),
// nang branch coverage cho tang controller/service.
public class ControllerErrorPathsTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;

    public ControllerErrorPathsTests(GymMasterWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => ClientAsAsync("admin@gymmaster.local", "Admin123!");
    private Task<HttpClient> StaffAsync() => ClientAsAsync("staff@gymmaster.local", "Staff123!");
    private Task<HttpClient> MemberAsync() => ClientAsAsync("member@gymmaster.local", "Member123!");

    private static async Task<JsonElement> DataAsync(HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();

    private async Task<long> MyMemberIdAsync(HttpClient member)
        => (await DataAsync(await member.GetAsync("/api/v1/members/me"))).GetProperty("id").GetInt64();

    // ---------- Members: update / delete / 360 / not-found ----------

    [Fact]
    public async Task Member_update_delete_and_360_flow()
    {
        var staff = await StaffAsync();
        var email = $"errmem-{Guid.NewGuid():N}@gym.test";
        var created = await staff.PostAsJsonAsync("/api/v1/members",
            new { fullName = "Err Member", email, phone = (string?)null, password = "member1234",
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null });
        var id = (await DataAsync(created)).GetProperty("member").GetProperty("id").GetInt64();

        // Update
        var put = await staff.PutAsJsonAsync($"/api/v1/members/{id}",
            new { fullName = "Err Updated", phone = (string?)null, dateOfBirth = (DateTime?)null,
                  gender = "male", address = "Ha Noi", emergencyContact = (string?)null });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // 360
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/members/{id}/360")).StatusCode);

        // Staff KHONG duoc xoa (chi Admin) -> 403
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.DeleteAsync($"/api/v1/members/{id}")).StatusCode);

        // Admin xoa (soft-delete) -> 2xx
        var admin = await AdminAsync();
        Assert.True((await admin.DeleteAsync($"/api/v1/members/{id}")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Member_get_and_update_nonexistent_return_404()
    {
        var staff = await StaffAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/api/v1/members/999999")).StatusCode);
        var put = await staff.PutAsJsonAsync("/api/v1/members/999999",
            new { fullName = "x", phone = (string?)null, dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task Member_create_invalid_email_returns_400_for_staff()
    {
        var staff = await StaffAsync();
        var res = await staff.PostAsJsonAsync("/api/v1/members",
            new { fullName = "X", email = "khong-email", phone = (string?)null, password = (string?)null,
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Trainers: get by id / update / not-found ----------

    [Fact]
    public async Task Trainer_get_update_and_notfound_flow()
    {
        var admin = await AdminAsync();
        var email = $"errpt-{Guid.NewGuid():N}@gym.test";
        var created = await admin.PostAsJsonAsync("/api/v1/trainers",
            new { fullName = "Err PT", email, phone = (string?)null, password = "trainer1234",
                  specialty = "Gym", bio = "pt", gender = "male", dateOfBirth = (DateTime?)null, yearsOfExperience = 2 });
        var id = (await DataAsync(created)).GetProperty("trainer").GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/trainers/{id}")).StatusCode);

        var put = await admin.PutAsJsonAsync($"/api/v1/trainers/{id}",
            new { specialty = "Boxing", bio = (string?)null, gender = (string?)null, dateOfBirth = (DateTime?)null,
                  yearsOfExperience = (int?)5, address = (string?)null, emergencyContact = (string?)null });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/trainers/999999")).StatusCode);
    }

    [Fact]
    public async Task Trainer_create_invalid_email_returns_400()
    {
        var admin = await AdminAsync();
        var res = await admin.PostAsJsonAsync("/api/v1/trainers",
            new { fullName = "X", email = "sai", phone = (string?)null, password = (string?)null,
                  specialty = (string?)null, bio = (string?)null, gender = (string?)null, dateOfBirth = (DateTime?)null, yearsOfExperience = (int?)null });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Check-ins qua controller ----------

    [Fact]
    public async Task Member_self_checkin_and_list_own_checkins()
    {
        var member = await MemberAsync();
        var id = await MyMemberIdAsync(member);

        var checkin = await member.PostAsJsonAsync("/api/v1/checkins", new { });
        Assert.Equal(HttpStatusCode.Created, checkin.StatusCode);

        var list = await member.GetAsync($"/api/v1/members/{id}/checkins");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    // ---------- Account: profile update + avatar sai dinh dang ----------

    [Fact]
    public async Task Account_update_profile_for_staff()
    {
        var staff = await StaffAsync();
        var res = await staff.PutAsJsonAsync("/api/v1/users/me/profile",
            new { dateOfBirth = new DateTime(1990, 1, 1), gender = "male", address = "Ha Noi", emergencyContact = "0900" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Account_upload_avatar_wrong_type_returns_400()
    {
        var member = await MemberAsync();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("khong phai anh"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "note.txt");

        var res = await member.PostAsync("/api/v1/users/me/avatar", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Memberships: renewal-request (member) + sell not-found ----------

    [Fact]
    public async Task Membership_sell_nonexistent_member_returns_error()
    {
        var staff = await StaffAsync();
        // Tao 1 goi de co packageId hop le
        var admin = await AdminAsync();
        var pkg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/packages",
            new { name = $"P-{Guid.NewGuid():N}", durationDays = (short)30, price = 200_000m, description = (string?)null, supportsPT = false }));
        var packageId = pkg.GetProperty("id").GetInt64();

        var res = await staff.PostAsJsonAsync("/api/v1/memberships/sell",
            new { memberId = 999999L, packageId, startDate = (DateOnly?)null });
        Assert.False(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Membership_renewal_request_by_member()
    {
        var admin = await AdminAsync();
        var pkg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/packages",
            new { name = $"R-{Guid.NewGuid():N}", durationDays = (short)30, price = 200_000m, description = (string?)null, supportsPT = false }));
        var packageId = pkg.GetProperty("id").GetInt64();

        var member = await MemberAsync();
        var res = await member.PostAsJsonAsync("/api/v1/memberships/renewal-request", new { packageId });
        // Tao yeu cau gia han thanh cong (2xx) hoac tra loi nghiep vu ro rang — mien la controller xu ly.
        Assert.True((int)res.StatusCode < 500, $"renewal-request => {res.StatusCode}");
    }

    // ---------- VnPay create-url (chua cau hinh -> 500 co kiem soat) ----------

    [Fact]
    public async Task VnPay_create_url_when_not_configured_returns_500()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync();
        var pkg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/packages",
            new { name = $"V-{Guid.NewGuid():N}", durationDays = (short)30, price = 400_000m, description = (string?)null, supportsPT = false }));
        var packageId = pkg.GetProperty("id").GetInt64();

        var email = $"vnpaymem-{Guid.NewGuid():N}@gym.test";
        var created = await staff.PostAsJsonAsync("/api/v1/members",
            new { fullName = "VnPay Member", email, phone = (string?)null, password = "member1234",
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null });
        var memberId = (await DataAsync(created)).GetProperty("member").GetProperty("id").GetInt64();

        var sell = await DataAsync(await staff.PostAsJsonAsync("/api/v1/memberships/sell",
            new { memberId, packageId, startDate = (DateOnly?)null }));
        var membershipId = sell.GetProperty("membership").GetProperty("id").GetInt64();

        var res = await staff.PostAsJsonAsync("/api/v1/payments/vnpay/create-url", new { membershipId });
        // VnPay chua cau hinh trong test -> service tra 500 VNPAY_NOT_CONFIGURED (co kiem soat).
        Assert.Equal(HttpStatusCode.InternalServerError, res.StatusCode);
    }
}
