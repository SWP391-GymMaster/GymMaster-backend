using System.Text;
using GymMaster.API.Data;
using GymMaster.API.Services;
using GymMaster.API.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<GymMasterDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(
    builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.Configure<CheckInOptions>(
    builder.Configuration.GetSection(CheckInOptions.SectionName));
builder.Services.Configure<VnPayOptions>(
    builder.Configuration.GetSection(VnPayOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IFoodItemService, FoodItemService>();
builder.Services.AddScoped<INutritionService, NutritionService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();

// Tim mon online (Open Food Facts): nho tam ket qua + HttpClient co timeout + User-Agent.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IFoodOnlineSearchService, FoodOnlineSearchService>(client =>
{
    var baseUrl = builder.Configuration["OpenFoodFacts:BaseUrl"] ?? "https://world.openfoodfacts.org/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GymMasterApp-SWP391 (admin@gymmaster.local)");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    app = "GymMaster API",
    status = "running"
}));

// Schema DB do team DB cung cap (backend KHONG tu tao/sua schema).
// Seeder chi dam bao co roles + admin de dang nhap (khong dung toi cau truc bang).
await DatabaseSeeder.SeedAsync(app.Services);

app.Run();
