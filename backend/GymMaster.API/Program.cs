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
builder.Services.Configure<CloudinaryOptions>(
    builder.Configuration.GetSection(CloudinaryOptions.SectionName));
builder.Services.Configure<CheckInOptions>(
    builder.Configuration.GetSection(CheckInOptions.SectionName));
builder.Services.Configure<VnPayOptions>(
    builder.Configuration.GetSection(VnPayOptions.SectionName));
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<GeminiOptions>(
    builder.Configuration.GetSection(GeminiOptions.SectionName));

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
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IAvatarStorage, CloudinaryAvatarStorage>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
// spec5vs8 — trainer assignment, workout plan, trainer note, dashboard
builder.Services.AddScoped<IAssignmentService, AssignmentService>();
builder.Services.AddScoped<IWorkoutPlanService, WorkoutPlanService>();
builder.Services.AddScoped<ITrainerNoteService, TrainerNoteService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
// spec 003/004/006/007 — membership, payment, progress, nutrition, check-in
builder.Services.AddScoped<IMembershipPackageService, MembershipPackageService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IProgressService, ProgressService>();
builder.Services.AddScoped<IFoodItemService, FoodItemService>();
// Quet anh mon an bang AI (Gemini)
builder.Services.AddScoped<IFoodScanService, FoodScanService>();
builder.Services.AddHttpClient<IFoodImageAnalyzer, GeminiService>();
builder.Services.AddScoped<INutritionService, NutritionService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();

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

// Sau proxy Cloud Run, TLS da duoc terminate o tang ngoai => chi ep HTTPS o dev.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
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
