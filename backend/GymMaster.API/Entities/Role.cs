namespace GymMaster.API.Entities;

public sealed class Role
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<UserRole> UserRoles { get; set; } = [];
}

public static class RoleNames
{
    public const string Admin = "admin";
    public const string Staff = "staff";
    public const string Pt = "pt";
    public const string Member = "member";

    public static readonly string[] All = [Admin, Staff, Pt, Member];
}
