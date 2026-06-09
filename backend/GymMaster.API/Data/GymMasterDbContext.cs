using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Data;

public sealed class GymMasterDbContext : DbContext
{
    public GymMasterDbContext(DbContextOptions<GymMasterDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();

    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ProgressLog> ProgressLogs => Set<ProgressLog>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    public DbSet<MealLog> MealLogs => Set<MealLog>();

    public DbSet<MealLogItem> MealLogItems => Set<MealLogItem>();

    public DbSet<CalorieTarget> CalorieTargets => Set<CalorieTarget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(255).IsRequired();
            entity.Property(user => user.Phone).HasMaxLength(30);
            entity.Property(user => user.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(user => user.FullName).HasMaxLength(150).IsRequired();
            entity.Property(user => user.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Name).HasMaxLength(30).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(255);
            entity.HasIndex(role => role.Name).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });

            entity
                .HasOne(userRole => userRole.User)
                .WithMany(user => user.UserRoles)
                .HasForeignKey(userRole => userRole.UserId);

            entity
                .HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(userRole => userRole.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(255).IsRequired();

            entity
                .HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(255).IsRequired();

            entity
                .HasOne(token => token.User)
                .WithMany(user => user.PasswordResetTokens)
                .HasForeignKey(token => token.UserId);
        });

        modelBuilder.Entity<MemberProfile>(entity =>
        {
            entity.ToTable("member_profiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Gender).HasMaxLength(20);
            entity.Property(profile => profile.Address).HasMaxLength(255);
            entity.Property(profile => profile.EmergencyContact).HasMaxLength(100);
            entity.HasIndex(profile => profile.UserId).IsUnique();

            entity
                .HasOne(profile => profile.User)
                .WithMany()
                .HasForeignKey(profile => profile.UserId);
        });

        modelBuilder.Entity<TrainerProfile>(entity =>
        {
            entity.ToTable("trainer_profiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Specialty).HasMaxLength(150);
            entity.Property(profile => profile.Bio).HasMaxLength(1000);
            entity.Property(profile => profile.Gender).HasMaxLength(20);
            entity.HasIndex(profile => profile.UserId).IsUnique();

            entity
                .HasOne(profile => profile.User)
                .WithMany()
                .HasForeignKey(profile => profile.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(100).IsRequired();
            entity.Property(log => log.Entity).HasMaxLength(60).IsRequired();
            entity.HasIndex(log => new { log.Entity, log.EntityId });
        });

        modelBuilder.Entity<MembershipPackage>(entity =>
        {
            entity.ToTable("membership_packages");
            entity.HasKey(package => package.Id);
            entity.Property(package => package.Name).HasMaxLength(100).IsRequired();
            entity.Property(package => package.Description).HasMaxLength(500);
            entity.Property(package => package.Price).HasPrecision(12, 2);
            entity.HasIndex(package => package.Name).IsUnique();
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.StartDate).HasColumnType("date");
            entity.Property(membership => membership.EndDate).HasColumnType("date");
            entity.Property(membership => membership.Status).HasConversion<byte>().HasColumnType("tinyint");
            entity.HasIndex(membership => membership.MemberId);

            entity
                .HasOne(membership => membership.Package)
                .WithMany()
                .HasForeignKey(membership => membership.PackageId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(12, 2);
            entity.Property(payment => payment.PaymentMethod).HasConversion<byte>().HasColumnType("tinyint");
            entity.Property(payment => payment.Status).HasConversion<byte>().HasColumnType("tinyint");
            entity.HasIndex(payment => payment.MembershipId);
        });

        modelBuilder.Entity<ProgressLog>(entity =>
        {
            entity.ToTable("progress_logs");
            entity.HasKey(progress => progress.Id);
            entity.Property(progress => progress.WeightKg).HasPrecision(5, 2);
            entity.Property(progress => progress.BodyFatPercent).HasPrecision(5, 2);
            entity.Property(progress => progress.ChestCm).HasPrecision(5, 2);
            entity.Property(progress => progress.WaistCm).HasPrecision(5, 2);
            entity.Property(progress => progress.HipCm).HasPrecision(5, 2);
            entity.Property(progress => progress.Note).HasMaxLength(500);
            entity.HasIndex(progress => new { progress.MemberId, progress.MeasuredAt });
        });

        modelBuilder.Entity<FoodItem>(entity =>
        {
            entity.ToTable("food_items");
            entity.HasKey(food => food.Id);
            entity.Property(food => food.Name).HasMaxLength(150).IsRequired();
            entity.Property(food => food.Unit).HasMaxLength(30).IsRequired();
            entity.Property(food => food.CaloriesPerUnit).HasPrecision(8, 2);
            entity.Property(food => food.ProteinG).HasPrecision(8, 2);
            entity.Property(food => food.CarbG).HasPrecision(8, 2);
            entity.Property(food => food.FatG).HasPrecision(8, 2);
            entity.HasIndex(food => food.Name).IsUnique();
        });

        modelBuilder.Entity<MealLog>(entity =>
        {
            entity.ToTable("meal_logs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.LogDate).HasColumnType("date");
            entity.Property(log => log.MealType).HasConversion<byte>().HasColumnType("tinyint");
            entity.HasIndex(log => new { log.MemberId, log.LogDate });

            entity
                .HasMany(log => log.Items)
                .WithOne()
                .HasForeignKey(item => item.MealLogId);
        });

        modelBuilder.Entity<MealLogItem>(entity =>
        {
            entity.ToTable("meal_log_items");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Quantity).HasPrecision(8, 2);
            entity.Property(item => item.Calories).HasPrecision(8, 2);

            entity
                .HasOne(item => item.FoodItem)
                .WithMany()
                .HasForeignKey(item => item.FoodItemId);
        });

        modelBuilder.Entity<CalorieTarget>(entity =>
        {
            entity.ToTable("calorie_targets");
            entity.HasKey(target => target.Id);
            entity.Property(target => target.EffectiveDate).HasColumnType("date");
            entity.Property(target => target.DailyCalories).HasPrecision(8, 2);
            entity.Property(target => target.ProteinG).HasPrecision(8, 2);
            entity.Property(target => target.CarbG).HasPrecision(8, 2);
            entity.Property(target => target.FatG).HasPrecision(8, 2);
            entity.HasIndex(target => new { target.MemberId, target.EffectiveDate }).IsUnique();
        });
    }
}
