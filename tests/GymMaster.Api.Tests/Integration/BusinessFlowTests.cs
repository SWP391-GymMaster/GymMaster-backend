using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace GymMaster.Api.Tests.Integration;

// Integration test chay TRON luong nghiep vu that qua HTTP:
// package -> member -> ban goi -> thanh toan -> phan PT -> giao an -> ghi chu -> tien do -> dinh duong,
// va CRUD user/package day du. Phu nhieu controller + service + nhanh loi cung luc.
public class BusinessFlowTests : IClassFixture<GymMasterWebAppFactory>
{
    private readonly GymMasterWebAppFactory _factory;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public BusinessFlowTests(GymMasterWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var doc = await ReadDocAsync(res);
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => ClientAsAsync("admin@gymmaster.local", "Admin123!");
    private Task<HttpClient> StaffAsync() => ClientAsAsync("staff@gymmaster.local", "Staff123!");

    private static async Task<JsonDocument> ReadDocAsync(HttpResponseMessage res)
        => JsonDocument.Parse(await res.Content.ReadAsStringAsync());

    // POST JSON, kiem tra status ky vong, tra ve phan "data".
    private static async Task<JsonElement> PostOkAsync(HttpClient client, string url, object body, HttpStatusCode expect)
    {
        var res = await client.PostAsJsonAsync(url, body);
        var doc = await ReadDocAsync(res);
        Assert.True(res.StatusCode == expect,
            $"POST {url} => {(int)res.StatusCode} {res.StatusCode}. Body: {doc.RootElement}");
        return doc.RootElement.GetProperty("data").Clone();
    }

    // ---------- Users: CRUD day du (admin) ----------

    [Fact]
    public async Task Users_full_crud_flow()
    {
        var admin = await AdminAsync();
        var email = $"crud-{Guid.NewGuid():N}@gym.test";

        // Create
        var created = await PostOkAsync(admin, "/api/v1/users",
            new { fullName = "CRUD User", email, phone = (string?)null, role = "staff", password = "matkhau123" },
            HttpStatusCode.Created);
        var id = created.GetProperty("userId").GetInt64();

        // Get by id
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/users/{id}")).StatusCode);

        // Update
        var put = await admin.PutAsJsonAsync($"/api/v1/users/{id}",
            new { fullName = "CRUD Updated", phone = (string?)null, status = (string?)null, role = (string?)null });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Patch status
        var patch = await admin.PatchAsJsonAsync($"/api/v1/users/{id}/status", new { status = "locked" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // Reset password
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/users/{id}/reset-password", null)).StatusCode);

        // Delete (soft)
        Assert.True((await admin.DeleteAsync($"/api/v1/users/{id}")).IsSuccessStatusCode);

        // Sau khi xoa -> get 404
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/users/{id}")).StatusCode);
    }

    [Fact]
    public async Task Users_create_validation_and_conflict()
    {
        var admin = await AdminAsync();

        // Email sai -> 400
        var bad = await admin.PostAsJsonAsync("/api/v1/users",
            new { fullName = "X", email = "khong-email", phone = (string?)null, role = "staff", password = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        // Trung email admin -> 409
        var dup = await admin.PostAsJsonAsync("/api/v1/users",
            new { fullName = "X", email = "admin@gymmaster.local", phone = (string?)null, role = "staff", password = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    // ---------- Packages CRUD (admin) ----------

    [Fact]
    public async Task Packages_create_and_update()
    {
        var admin = await AdminAsync();

        var created = await PostOkAsync(admin, "/api/v1/packages",
            new { name = $"Goi-{Guid.NewGuid():N}", durationDays = (short)30, price = 300_000m, description = "goi test", supportsPT = false },
            HttpStatusCode.Created);
        var id = created.GetProperty("id").GetInt64();

        var put = await admin.PutAsJsonAsync($"/api/v1/packages/{id}",
            new { name = (string?)null, durationDays = (short?)null, price = (decimal?)350_000m, description = (string?)null, isActive = (bool?)null, supportsPT = (bool?)null });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
    }

    // ---------- Luong lon: ban goi -> thanh toan -> PT -> giao an -> tien do -> dinh duong ----------

    [Fact]
    public async Task Full_membership_pt_training_nutrition_flow()
    {
        var admin = await AdminAsync();
        var staff = await StaffAsync();

        // 1) Admin tao goi ho tro PT
        var pkg = await PostOkAsync(admin, "/api/v1/packages",
            new { name = $"PT-{Guid.NewGuid():N}", durationDays = (short)30, price = 500_000m, description = "goi PT", supportsPT = true },
            HttpStatusCode.Created);
        var packageId = pkg.GetProperty("id").GetInt64();

        // 2) Staff tao hoi vien (lay initialPassword de dang nhap sau)
        var memberEmail = $"flowmem-{Guid.NewGuid():N}@gym.test";
        var memberData = await PostOkAsync(staff, "/api/v1/members",
            new { fullName = "Flow Member", email = memberEmail, phone = (string?)null, password = "member1234",
                  dateOfBirth = (DateTime?)null, gender = (string?)null, address = (string?)null, emergencyContact = (string?)null },
            HttpStatusCode.Created);
        var memberId = memberData.GetProperty("member").GetProperty("id").GetInt64();

        // 3) Staff ban goi -> membership PendingPayment
        var sell = await PostOkAsync(staff, "/api/v1/memberships/sell",
            new { memberId, packageId, startDate = (DateOnly?)null }, HttpStatusCode.Created);
        var membershipId = sell.GetProperty("membership").GetProperty("id").GetInt64();

        // 4) Staff xac nhan thanh toan -> membership Active
        var pay = await staff.PostAsJsonAsync($"/api/v1/memberships/{membershipId}/payment",
            new { amount = 500_000m, method = "cash" });
        Assert.True(pay.IsSuccessStatusCode, $"payment => {pay.StatusCode}");

        // 5) Admin tao PT
        var trainerEmail = $"flowpt-{Guid.NewGuid():N}@gym.test";
        var trainerData = await PostOkAsync(admin, "/api/v1/trainers",
            new { fullName = "Flow PT", email = trainerEmail, phone = (string?)null, password = "trainer1234",
                  specialty = "Gym", bio = "pt", gender = "male", dateOfBirth = (DateTime?)null, yearsOfExperience = 3 },
            HttpStatusCode.Created);
        var trainerId = trainerData.GetProperty("trainer").GetProperty("id").GetInt64();

        // 6) Admin phan cong PT cho hoi vien (member da co goi PT active)
        await PostOkAsync(admin, "/api/v1/assignments", new { memberId, trainerId }, HttpStatusCode.Created);

        // 7) PT dang nhap, tao giao an + ghi chu cho hoi vien
        var pt = await ClientAsAsync(trainerEmail, "trainer1234");
        await PostOkAsync(pt, $"/api/v1/members/{memberId}/workout-plans",
            new
            {
                title = "Giao an tang co",
                goal = "Tang co",
                startDate = (DateOnly?)null,
                endDate = (DateOnly?)null,
                exercises = new[] { new { name = "Bench Press", sets = (byte)3, reps = "10", note = (string?)null, orderIndex = (short)0 } },
            },
            HttpStatusCode.Created);
        await PostOkAsync(pt, $"/api/v1/members/{memberId}/notes",
            new { content = "Buoi tap tot" }, HttpStatusCode.Created);

        // 8) Member dang nhap, ghi tien do + dat muc calo
        var member = await ClientAsAsync(memberEmail, "member1234");
        await PostOkAsync(member, $"/api/v1/members/{memberId}/progress",
            new { measuredAt = (DateTime?)null, weightKg = 70.5m, bodyFatPct = 18m, chestCm = (decimal?)null,
                  waistCm = 80m, hipCm = (decimal?)null, note = "ok" },
            HttpStatusCode.Created);
        var calorie = await member.PostAsJsonAsync($"/api/v1/members/{memberId}/calorie-target",
            new { effectiveDate = (DateOnly?)null, dailyCalories = 2200m, proteinG = 150m, carbG = 200m, fatG = 60m });
        Assert.True(calorie.IsSuccessStatusCode, $"calorie-target => {calorie.StatusCode}");

        // 9) Admin tao mon an, member ghi nhat ky bua an
        var food = await PostOkAsync(admin, "/api/v1/food-items",
            new { name = $"Com-{Guid.NewGuid():N}", unit = "phan", caloriesPerUnit = 350m, proteinG = 8m, carbG = 70m, fatG = 3m },
            HttpStatusCode.Created);
        var foodId = food.GetProperty("id").GetInt64();
        var meal = await member.PostAsJsonAsync("/api/v1/meal-logs",
            new { memberId, logDate = DateOnly.FromDateTime(DateTime.UtcNow), mealType = (byte)1,
                  items = new[] { new { foodItemId = foodId, quantity = 1m } } });
        Assert.True(meal.IsSuccessStatusCode, $"meal-logs => {meal.StatusCode}");

        // 10) Member xem cac man tong hop cua chinh minh
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/members/me/profile-360")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/members/me/workout-plans")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/members/me/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/v1/members/{memberId}/progress")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/v1/members/{memberId}/calorie-summary")).StatusCode);
    }

    // ---------- Auth: doi mat khau + logout ----------

    [Fact]
    public async Task Change_password_then_logout_flow()
    {
        // Tao user rieng de doi mat khau (khong dung tai khoan demo dung chung)
        var admin = await AdminAsync();
        var email = $"pwd-{Guid.NewGuid():N}@gym.test";
        await PostOkAsync(admin, "/api/v1/users",
            new { fullName = "Pwd User", email, phone = (string?)null, role = "staff", password = "matkhau123" },
            HttpStatusCode.Created);

        var client = await ClientAsAsync(email, "matkhau123");

        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password",
            new { currentPassword = "matkhau123", newPassword = "matkhaumoi123" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var logout = await client.PostAsync("/api/v1/auth/logout", null);
        Assert.True(logout.IsSuccessStatusCode, $"logout => {logout.StatusCode}");

        // Dang nhap lai bang mat khau moi
        var relogin = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "matkhaumoi123" });
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
    }

    // ---------- Forgot -> reset password (dev tra OTP) ----------

    [Fact]
    public async Task Forgot_then_reset_password_flow()
    {
        var admin = await AdminAsync();
        var email = $"reset-{Guid.NewGuid():N}@gym.test";
        await PostOkAsync(admin, "/api/v1/users",
            new { fullName = "Reset User", email, phone = (string?)null, role = "staff", password = "matkhau123" },
            HttpStatusCode.Created);

        var pub = _factory.CreateClient();

        // forgot-password luon tra 200 (message chung, chong user-enumeration).
        var forgot = await pub.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        // OTP that chi lo o moi truong Development; o "Testing" khong tra ve -> dung
        // ma sai de kiem tra nhanh INVALID_RESET_TOKEN cua controller reset-password.
        var reset = await pub.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { email, resetToken = "000000", newPassword = "matkhaumoi123" });
        Assert.Equal(HttpStatusCode.Unauthorized, reset.StatusCode);
    }
}
