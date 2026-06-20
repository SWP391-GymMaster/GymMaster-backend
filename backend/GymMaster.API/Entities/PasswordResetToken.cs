namespace GymMaster.API.Entities;

public sealed class PasswordResetToken
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    // So lan nhap OTP sai (gioi han 3 lan -> vo hieu ma).
    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
