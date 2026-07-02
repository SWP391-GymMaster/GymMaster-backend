namespace GymMaster.API.Entities;

public sealed class User
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string Status { get; set; } = UserStatuses.Active;

    public int FailedLoginCount { get; set; }

    public DateTime? LoginWindowStartedAt { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<UserRole> UserRoles { get; set; } = [];

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    public List<PasswordResetToken> PasswordResetTokens { get; set; } = [];
}

public static class UserStatuses
{
    public const string Active = "active";
    public const string Locked = "locked";
}
