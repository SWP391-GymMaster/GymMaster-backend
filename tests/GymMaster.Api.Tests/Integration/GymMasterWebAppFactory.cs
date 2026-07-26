using GymMaster.API.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GymMaster.Api.Tests.Integration;

// Dung host API THAT (Program.cs) nhung thay SQL Server bang EF Core InMemory.
// Moi factory co 1 database InMemory rieng (ten GUID) -> cac test class doc lap nhau.
// Seeder cua app van chay luc khoi dong -> co san roles + 4 tai khoan demo de login that.
public sealed class GymMasterWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"it-{Guid.NewGuid()}";

    // Khop secret nay voi cai app dung de ky/verify JWT -> token login se hop le.
    public const string JwtSecret = "khoa-bi-mat-du-dai-cho-hs256-toi-thieu-32-ky-tu";
    public const string JwtIssuer = "GymMaster";
    public const string JwtAudience = "GymMaster.Client";

    // Program.cs doc SecretKey JWT EAGER (`.Get<JwtOptions>()`) ngay khi build host —
    // TRUOC khi ConfigureAppConfiguration cua factory kip chay. Neu chi dua secret qua
    // ConfigureAppConfiguration thi middleware verify bang key rong -> 401 du token hop le.
    // Bien moi truong duoc CreateBuilder nap som nhat nen middleware va AuthService dung chung 1 key.
    static GymMasterWebAppFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__SecretKey", JwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Server=(localdb)\\unused;Database=unused;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" != Development -> tat HttpsRedirection (tranh 307) va MapOpenApi.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                // Chuoi ket noi that khong dung toi (DbContext bi thay o duoi) nhung
                // phai co gia tri de UseSqlServer khong nem khi dang ky ban dau.
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\unused;Database=unused;",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Go dang ky DbContextOptions gan SQL Server.
            services.RemoveAll<DbContextOptions<GymMasterDbContext>>();
            services.RemoveAll<DbContextOptions>();

            // InMemory dung mot internal service provider RIENG -> khong dung do voi cac
            // service provider cua SQL Server con sot lai trong container (loi "Only a single
            // database provider can be registered").
            var inMemoryProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<GymMasterDbContext>(options =>
                options
                    .UseInMemoryDatabase(_dbName)
                    .UseInternalServiceProvider(inMemoryProvider)
                    // Mot so service dung transaction (Membership/VnPay) — InMemory khong ho tro,
                    // bo qua canh bao de khong nem khi chay qua controller.
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        });
    }
}
