using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GymMasterDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        await EnsureAuthCompatibilityAsync(dbContext);

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

        var adminRole = await dbContext.Roles.SingleAsync(role => role.Name == RoleNames.Admin);
        var admin = await dbContext.Users
            .Include(user => user.UserRoles)
            .FirstOrDefaultAsync(user => user.Email == "admin@gymmaster.local");

        if (admin is null)
        {
            admin = new User
            {
                Email = "admin@gymmaster.local",
                FullName = "GymMaster Admin",
                Status = UserStatuses.Active
            };

            dbContext.Users.Add(admin);
        }

        admin.FullName = "GymMaster Admin";
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!", 12);
        admin.Status = UserStatuses.Active;
        admin.UpdatedAt = DateTime.UtcNow;

        if (admin.UserRoles.All(userRole => userRole.RoleId != adminRole.Id))
        {
            admin.UserRoles.Add(new UserRole
            {
                User = admin,
                Role = adminRole
            });
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
            """);
    }
}
