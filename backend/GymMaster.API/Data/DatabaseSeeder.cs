using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GymMasterDbContext>();

        // Schema do team DB cung cap (KHONG tu tao/sua schema). Chi seed du lieu:
        // dam bao co du roles + 1 tai khoan admin de dang nhap neu DB con trong.
        foreach (var roleName in RoleNames.All)
        {
            if (!await dbContext.Roles.AnyAsync(role => role.Name == roleName))
            {
                dbContext.Roles.Add(new Role
                {
                    Name = roleName,
                    Description = $"{roleName} role"
                });
            }
        }

        await dbContext.SaveChangesAsync();

        // Tai khoan demo san co cho moi role (de test). Chi tao neu CHUA co — khong reset mat khau da doi.
        await EnsureUserAsync(dbContext, "admin@gymmaster.local", "GymMaster Admin", "Admin123!", RoleNames.Admin);
        await EnsureUserAsync(dbContext, "staff@gymmaster.local", "GymMaster Staff", "Staff123!", RoleNames.Staff);
        await EnsureUserAsync(dbContext, "pt@gymmaster.local", "GymMaster PT", "Pt123!", RoleNames.Pt);
        await EnsureUserAsync(dbContext, "member@gymmaster.local", "GymMaster Member", "Member123!", RoleNames.Member);
    }

    private static async Task EnsureUserAsync(
        GymMasterDbContext dbContext,
        string email,
        string fullName,
        string password,
        string roleName)
    {
        var role = await dbContext.Roles.SingleAsync(item => item.Name == roleName);
        var user = await dbContext.Users
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Email == email);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                FullName = fullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
                Status = UserStatuses.Active
            };

            dbContext.Users.Add(user);
        }

        if (user.UserRoles.All(userRole => userRole.RoleId != role.Id))
        {
            user.UserRoles.Add(new UserRole { User = user, Role = role });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureAuthCompatibilityAsync(GymMasterDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('users', 'FailedLoginCount') IS NULL
                ALTER TABLE [users] ADD [FailedLoginCount] int NOT NULL CONSTRAINT [DF_users_FailedLoginCount] DEFAULT 0;

            IF COL_LENGTH('users', 'LoginWindowStartedAt') IS NULL
                ALTER TABLE [users] ADD [LoginWindowStartedAt] datetime2 NULL;

            IF COL_LENGTH('users', 'LockedUntil') IS NULL
                ALTER TABLE [users] ADD [LockedUntil] datetime2 NULL;

            IF COL_LENGTH('users', 'LastLoginAt') IS NULL
                ALTER TABLE [users] ADD [LastLoginAt] datetime2 NULL;

            IF COL_LENGTH('users', 'IsDeleted') IS NULL
                ALTER TABLE [users] ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_users_IsDeleted] DEFAULT 0;

            IF COL_LENGTH('roles', 'Description') IS NULL
                ALTER TABLE [roles] ADD [Description] nvarchar(255) NULL;

            IF OBJECT_ID(N'[password_reset_tokens]', N'U') IS NULL
            BEGIN
                CREATE TABLE [password_reset_tokens] (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [UserId] bigint NOT NULL,
                    [TokenHash] nvarchar(255) NOT NULL,
                    [ExpiresAt] datetime2 NOT NULL,
                    [UsedAt] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_password_reset_tokens] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_password_reset_tokens_users_UserId] FOREIGN KEY ([UserId]) REFERENCES [users] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_password_reset_tokens_UserId] ON [password_reset_tokens] ([UserId]);
            END

            IF OBJECT_ID(N'[member_profiles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [member_profiles] (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [UserId] bigint NOT NULL,
                    [DateOfBirth] datetime2 NULL,
                    [Gender] nvarchar(20) NULL,
                    [Address] nvarchar(255) NULL,
                    [EmergencyContact] nvarchar(100) NULL,
                    [JoinedAt] datetime2 NOT NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_member_profiles_IsDeleted] DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_member_profiles] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_member_profiles_users_UserId] FOREIGN KEY ([UserId]) REFERENCES [users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_member_profiles_UserId] ON [member_profiles] ([UserId]);
            END

            IF OBJECT_ID(N'[trainer_profiles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [trainer_profiles] (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [UserId] bigint NOT NULL,
                    [Specialty] nvarchar(150) NULL,
                    [Bio] nvarchar(1000) NULL,
                    [Gender] nvarchar(20) NULL,
                    [DateOfBirth] datetime2 NULL,
                    [YearsOfExperience] int NULL,
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_trainer_profiles_IsDeleted] DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_trainer_profiles] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_trainer_profiles_users_UserId] FOREIGN KEY ([UserId]) REFERENCES [users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_trainer_profiles_UserId] ON [trainer_profiles] ([UserId]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH('trainer_profiles', 'Gender') IS NULL
                    ALTER TABLE [trainer_profiles] ADD [Gender] nvarchar(20) NULL;

                IF COL_LENGTH('trainer_profiles', 'DateOfBirth') IS NULL
                    ALTER TABLE [trainer_profiles] ADD [DateOfBirth] datetime2 NULL;

                IF COL_LENGTH('trainer_profiles', 'YearsOfExperience') IS NULL
                    ALTER TABLE [trainer_profiles] ADD [YearsOfExperience] int NULL;
            END

            IF OBJECT_ID(N'[audit_logs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [audit_logs] (
                    [Id] bigint IDENTITY(1,1) NOT NULL,
                    [UserId] bigint NULL,
                    [Action] nvarchar(100) NOT NULL,
                    [Entity] nvarchar(60) NOT NULL,
                    [EntityId] bigint NOT NULL,
                    [Metadata] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_audit_logs] PRIMARY KEY ([Id])
                );

                CREATE INDEX [IX_audit_logs_Entity_EntityId] ON [audit_logs] ([Entity], [EntityId]);
            END
            """);
    }
}
