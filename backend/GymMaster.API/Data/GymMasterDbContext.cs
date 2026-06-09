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

    public DbSet<TrainerAssignment> TrainerAssignments => Set<TrainerAssignment>();

    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();

    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    public DbSet<ExerciseCatalog> ExerciseCatalogs => Set<ExerciseCatalog>();

    public DbSet<TrainerNote> TrainerNotes => Set<TrainerNote>();

    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

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

        modelBuilder.Entity<TrainerAssignment>(entity =>
        {
            entity.ToTable("trainer_assignments");
            entity.HasKey(a => a.Id);

            entity
                .HasOne(a => a.Member)
                .WithMany()
                .HasForeignKey(a => a.MemberId);

            entity
                .HasOne(a => a.Trainer)
                .WithMany()
                .HasForeignKey(a => a.TrainerId);

            entity.HasIndex(a => new { a.MemberId, a.Status });
            entity.HasIndex(a => new { a.TrainerId, a.Status });
        });

        modelBuilder.Entity<WorkoutPlan>(entity =>
        {
            entity.ToTable("workout_plans", t => t.UseSqlOutputClause(false));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).HasMaxLength(150).IsRequired();
            entity.Property(p => p.Goal).HasMaxLength(255);

            entity
                .HasOne(p => p.Member)
                .WithMany()
                .HasForeignKey(p => p.MemberId);

            entity
                .HasOne(p => p.Trainer)
                .WithMany()
                .HasForeignKey(p => p.TrainerId);

            entity.HasIndex(p => new { p.MemberId, p.Status });
            entity.HasIndex(p => new { p.TrainerId, p.Status });
        });

        modelBuilder.Entity<ExerciseCatalog>(entity =>
        {
            entity.ToTable("exercise_catalog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.MuscleGroup).HasMaxLength(80);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<WorkoutExercise>(entity =>
        {
            entity.ToTable("workout_exercises");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Note).HasMaxLength(255);
            entity.Property(e => e.WeightKg).HasPrecision(6, 2);

            entity
                .HasOne(e => e.WorkoutPlan)
                .WithMany(p => p.Exercises)
                .HasForeignKey(e => e.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Exercise)
                .WithMany()
                .HasForeignKey(e => e.ExerciseId);

            entity.HasIndex(e => new { e.WorkoutPlanId, e.SortOrder }).IsUnique();
            entity.HasIndex(e => e.ExerciseId);
        });

        modelBuilder.Entity<TrainerNote>(entity =>
        {
            entity.ToTable("trainer_notes");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Content).HasMaxLength(1000).IsRequired();

            entity
                .HasOne(n => n.Member)
                .WithMany()
                .HasForeignKey(n => n.MemberId);

            entity
                .HasOne(n => n.Trainer)
                .WithMany()
                .HasForeignKey(n => n.TrainerId);

            entity.HasIndex(n => new { n.MemberId, n.NoteDate });
            entity.HasIndex(n => new { n.TrainerId, n.NoteDate });
        });

        modelBuilder.Entity<MembershipPackage>(entity =>
        {
            entity.ToTable("membership_packages");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(150).IsRequired();
            entity.Property(p => p.Price).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("memberships");
            entity.HasKey(m => m.Id);

            entity
                .HasOne(m => m.Member)
                .WithMany()
                .HasForeignKey(m => m.MemberId);

            entity
                .HasOne(m => m.Package)
                .WithMany()
                .HasForeignKey(m => m.PackageId);

            entity.HasIndex(m => new { m.MemberId, m.Status });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(12, 2);
            entity.Property(p => p.Method).HasColumnName("PaymentMethod");
            entity.Property(p => p.CreatedBy).HasColumnName("CreatedByUserId");

            entity
                .HasOne(p => p.Membership)
                .WithMany()
                .HasForeignKey(p => p.MembershipId);

            entity.HasIndex(p => p.MembershipId);
            entity.HasIndex(p => p.PaidAt);
        });

        modelBuilder.Entity<CheckIn>(entity =>
        {
            entity.ToTable("check_ins");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CreatedBy).HasColumnName("CreatedByUserId");

            entity
                .HasOne(c => c.Member)
                .WithMany()
                .HasForeignKey(c => c.MemberId);

            entity.HasIndex(c => new { c.MemberId, c.CheckInAt });
        });
    }
}
