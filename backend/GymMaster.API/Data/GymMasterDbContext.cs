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
    }
}
