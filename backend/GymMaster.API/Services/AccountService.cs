using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymMaster.API.Services;

public sealed class AccountService : IAccountService
{
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly GymMasterDbContext _dbContext;
    private readonly IAvatarStorage _avatarStorage;
    private readonly IAuditService _auditService;

    public AccountService(
        GymMasterDbContext dbContext,
        IAvatarStorage avatarStorage,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _avatarStorage = avatarStorage;
        _auditService = auditService;
    }

    public async Task<AuthServiceResult<AuthUserResponse>> UpdateAsync(
        ClaimsPrincipal principal,
        UpdateMyAccountRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Fail<AuthUserResponse>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.Phone is not null)
        {
            var phone = NormalizePhone(request.Phone);

            if (phone is not null && await _dbContext.Users.AnyAsync(
                    other => other.Phone == phone && other.Id != user.Id && !other.IsDeleted,
                    cancellationToken))
            {
                return Fail<AuthUserResponse>(
                    "DUPLICATE",
                    "So dien thoai nay da duoc su dung.",
                    StatusCodes.Status409Conflict);
            }

            user.Phone = phone;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("UPDATE_ACCOUNT", "User", user.Id, null, cancellationToken);

        return AuthServiceResult<AuthUserResponse>.Success(
            ToUserResponse(user, await GetMemberProfileIdAsync(user.Id, cancellationToken)));
    }

    public async Task<AuthServiceResult<AuthUserResponse>> UploadAvatarAsync(
        ClaimsPrincipal principal,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Fail<AuthUserResponse>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        if (file is null ||
            file.Length <= 0 ||
            file.Length > MaxAvatarBytes ||
            !AllowedAvatarContentTypes.Contains(file.ContentType))
        {
            return Fail<AuthUserResponse>(
                "VALIDATION_ERROR",
                "Anh dai dien phai la jpeg/png/webp va toi da 5 MB.",
                StatusCodes.Status400BadRequest);
        }

        try
        {
            await using var content = file.OpenReadStream();
            user.AvatarUrl = await _avatarStorage.UploadAsync(
                user.Id,
                content,
                file.ContentType,
                cancellationToken);
        }
        catch (AvatarStorageException exception)
        {
            return Fail<AuthUserResponse>(
                exception.Code,
                exception.Message,
                exception.Code == "CLOUDINARY_NOT_CONFIGURED"
                    ? StatusCodes.Status500InternalServerError
                    : StatusCodes.Status502BadGateway);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("UPDATE_AVATAR", "User", user.Id, null, cancellationToken);

        return AuthServiceResult<AuthUserResponse>.Success(
            ToUserResponse(user, await GetMemberProfileIdAsync(user.Id, cancellationToken)));
    }

    public async Task<AuthServiceResult<PersonalProfileResponse>> GetProfileAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Fail<PersonalProfileResponse>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        return GetPrimaryRole(user) switch
        {
            RoleNames.Admin or RoleNames.Staff => AuthServiceResult<PersonalProfileResponse>.Success(
                ToPersonalProfileResponse(await GetOrCreateStaffProfileAsync(user, cancellationToken))),
            RoleNames.Pt => await GetTrainerPersonalProfileAsync(user.Id, cancellationToken),
            RoleNames.Member => Fail<PersonalProfileResponse>(
                "NOT_SUPPORTED",
                "Hoi vien cap nhat ho so tai /api/v1/members/me.",
                StatusCodes.Status400BadRequest),
            _ => Fail<PersonalProfileResponse>(
                "NOT_SUPPORTED",
                "Vai tro khong duoc ho tro.",
                StatusCodes.Status400BadRequest)
        };
    }

    public async Task<AuthServiceResult<PersonalProfileResponse>> UpdateProfileAsync(
        ClaimsPrincipal principal,
        UpdatePersonalProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetUserFromPrincipalAsync(principal, cancellationToken);

        if (user is null)
        {
            return Fail<PersonalProfileResponse>(
                "UNAUTHORIZED",
                "Token khong hop le.",
                StatusCodes.Status401Unauthorized);
        }

        var role = GetPrimaryRole(user);
        if (role is RoleNames.Admin or RoleNames.Staff)
        {
            var profile = await GetOrCreateStaffProfileAsync(user, cancellationToken);
            ApplyPersonalProfile(profile, request);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync("UPDATE_PERSONAL_PROFILE", "StaffProfile", profile.Id, null, cancellationToken);

            return AuthServiceResult<PersonalProfileResponse>.Success(ToPersonalProfileResponse(profile));
        }

        if (role == RoleNames.Pt)
        {
            var profile = await _dbContext.TrainerProfiles
                .FirstOrDefaultAsync(item => item.UserId == user.Id && !item.IsDeleted, cancellationToken);

            if (profile is null)
            {
                return Fail<PersonalProfileResponse>(
                    "NOT_FOUND",
                    "Chua co ho so huan luyen vien - lien he quan tri vien.",
                    StatusCodes.Status404NotFound);
            }

            ApplyPersonalProfile(profile, request);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync("UPDATE_PERSONAL_PROFILE", "TrainerProfile", profile.Id, null, cancellationToken);

            return AuthServiceResult<PersonalProfileResponse>.Success(ToPersonalProfileResponse(profile));
        }

        return Fail<PersonalProfileResponse>(
            "NOT_SUPPORTED",
            "Hoi vien cap nhat ho so tai /api/v1/members/me.",
            StatusCodes.Status400BadRequest);
    }

    private async Task<User?> GetUserFromPrincipalAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetActorId(principal);

        return userId is null
            ? null
            : await _dbContext.Users
                .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .FirstOrDefaultAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);
    }

    private async Task<long?> GetMemberProfileIdAsync(long userId, CancellationToken cancellationToken)
    {
        return await _dbContext.MemberProfiles
            .Where(profile => profile.UserId == userId && !profile.IsDeleted)
            .Select(profile => (long?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<StaffProfile> GetOrCreateStaffProfileAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var profile = await FindStaffProfileAsync(user.Id, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new StaffProfile
        {
            UserId = user.Id,
            User = user
        };

        _dbContext.StaffProfiles.Add(profile);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsStaffProfileUserUniqueViolation(exception))
        {
            _dbContext.Entry(profile).State = EntityState.Detached;

            profile = await FindStaffProfileAsync(user.Id, cancellationToken);
            if (profile is not null)
            {
                return profile;
            }

            throw;
        }

        await _auditService.LogAsync(
            "CREATE_STAFF_PROFILE",
            "StaffProfile",
            profile.Id,
            new { selfService = true },
            cancellationToken);

        return profile;
    }

    private Task<StaffProfile?> FindStaffProfileAsync(long userId, CancellationToken cancellationToken)
    {
        return _dbContext.StaffProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId && !profile.IsDeleted, cancellationToken);
    }

    private async Task<AuthServiceResult<PersonalProfileResponse>> GetTrainerPersonalProfileAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.TrainerProfiles
            .FirstOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);

        return profile is null
            ? Fail<PersonalProfileResponse>(
                "NOT_FOUND",
                "Chua co ho so huan luyen vien - lien he quan tri vien.",
                StatusCodes.Status404NotFound)
            : AuthServiceResult<PersonalProfileResponse>.Success(ToPersonalProfileResponse(profile));
    }

    private static void ApplyPersonalProfile(StaffProfile profile, UpdatePersonalProfileRequest request)
    {
        if (request.DateOfBirth is not null)
        {
            profile.DateOfBirth = request.DateOfBirth;
        }

        if (request.Gender is not null)
        {
            profile.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        }

        if (request.Address is not null)
        {
            profile.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        }

        if (request.EmergencyContact is not null)
        {
            profile.EmergencyContact = string.IsNullOrWhiteSpace(request.EmergencyContact) ? null : request.EmergencyContact.Trim();
        }

        profile.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplyPersonalProfile(TrainerProfile profile, UpdatePersonalProfileRequest request)
    {
        if (request.DateOfBirth is not null)
        {
            profile.DateOfBirth = request.DateOfBirth;
        }

        if (request.Gender is not null)
        {
            profile.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        }

        if (request.Address is not null)
        {
            profile.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        }

        if (request.EmergencyContact is not null)
        {
            profile.EmergencyContact = string.IsNullOrWhiteSpace(request.EmergencyContact) ? null : request.EmergencyContact.Trim();
        }

        profile.UpdatedAt = DateTime.UtcNow;
    }

    private static PersonalProfileResponse ToPersonalProfileResponse(StaffProfile profile)
    {
        return new PersonalProfileResponse(
            profile.DateOfBirth,
            profile.Gender,
            profile.Address,
            profile.EmergencyContact);
    }

    private static PersonalProfileResponse ToPersonalProfileResponse(TrainerProfile profile)
    {
        return new PersonalProfileResponse(
            profile.DateOfBirth,
            profile.Gender,
            profile.Address,
            profile.EmergencyContact);
    }

    private static AuthUserResponse ToUserResponse(User user, long? memberProfileId)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.AvatarUrl,
            GetPrimaryRole(user),
            user.Status,
            memberProfileId);
    }

    private static string GetPrimaryRole(User user)
    {
        return user.UserRoles.Select(userRole => userRole.Role.Name).FirstOrDefault() ?? RoleNames.Member;
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static bool IsStaffProfileUserUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
            sqlException.Message.Contains("IX_staff_profiles_UserId", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizePhone(string? phone)
    {
        return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
