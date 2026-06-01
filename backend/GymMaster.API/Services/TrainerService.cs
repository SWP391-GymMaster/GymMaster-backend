using GymMaster.API.Data;
using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Services;

public sealed class TrainerService : ITrainerService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;

    public TrainerService(GymMasterDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // FR-PT-PROF-01: tao TrainerProfile gan voi User role PT.
    public async Task<AuthServiceResult<TrainerResponse>> CreateAsync(
        CreateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == request.UserId && !item.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Fail<TrainerResponse>("NOT_FOUND", "Khong tim thay nguoi dung.", StatusCodes.Status404NotFound);
        }

        if (!user.UserRoles.Any(userRole => userRole.Role.Name == RoleNames.Pt))
        {
            return Fail<TrainerResponse>(
                "INVALID_ROLE",
                "Nguoi dung nay khong phai role PT.",
                StatusCodes.Status422UnprocessableEntity);
        }

        if (await _dbContext.TrainerProfiles.AnyAsync(
                profile => profile.UserId == user.Id && !profile.IsDeleted, cancellationToken))
        {
            return Fail<TrainerResponse>("DUPLICATE", "Nguoi dung nay da co ho so PT.", StatusCodes.Status409Conflict);
        }

        if (request.YearsOfExperience is < 0)
        {
            return Fail<TrainerResponse>(
                "VALIDATION_ERROR", "So nam kinh nghiem khong hop le.", StatusCodes.Status400BadRequest);
        }

        var profile = new TrainerProfile
        {
            UserId = user.Id,
            User = user,
            Specialty = string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim(),
            Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim(),
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            DateOfBirth = request.DateOfBirth,
            YearsOfExperience = request.YearsOfExperience
        };

        _dbContext.TrainerProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("CREATE_TRAINER", "TrainerProfile", profile.Id, null, cancellationToken);

        return AuthServiceResult<TrainerResponse>.Success(ToResponse(profile), StatusCodes.Status201Created);
    }

    public async Task<AuthServiceResult<PagedResult<TrainerResponse>>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var trainers = _dbContext.TrainerProfiles
            .Include(profile => profile.User)
            .Where(profile => !profile.IsDeleted && !profile.User.IsDeleted);

        var totalItems = await trainers.CountAsync(cancellationToken);
        var items = await trainers
            .OrderByDescending(profile => profile.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var result = new PagedResult<TrainerResponse>(
            items.Select(ToResponse).ToList(), page, pageSize, totalItems, totalPages);

        return AuthServiceResult<PagedResult<TrainerResponse>>.Success(result);
    }

    public async Task<AuthServiceResult<TrainerResponse>> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var profile = await FindAsync(id, cancellationToken);

        return profile is null
            ? Fail<TrainerResponse>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound)
            : AuthServiceResult<TrainerResponse>.Success(ToResponse(profile));
    }

    public async Task<AuthServiceResult<TrainerResponse>> UpdateAsync(
        long id,
        UpdateTrainerRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await FindAsync(id, cancellationToken);

        if (profile is null)
        {
            return Fail<TrainerResponse>("NOT_FOUND", "Khong tim thay ho so PT.", StatusCodes.Status404NotFound);
        }

        if (request.Specialty is not null)
        {
            profile.Specialty = string.IsNullOrWhiteSpace(request.Specialty) ? null : request.Specialty.Trim();
        }

        if (request.Bio is not null)
        {
            profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        }

        if (request.Gender is not null)
        {
            profile.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
        }

        if (request.DateOfBirth is not null)
        {
            profile.DateOfBirth = request.DateOfBirth;
        }

        if (request.YearsOfExperience is not null)
        {
            if (request.YearsOfExperience < 0)
            {
                return Fail<TrainerResponse>(
                    "VALIDATION_ERROR", "So nam kinh nghiem khong hop le.", StatusCodes.Status400BadRequest);
            }

            profile.YearsOfExperience = request.YearsOfExperience;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync("UPDATE_TRAINER", "TrainerProfile", profile.Id, null, cancellationToken);

        return AuthServiceResult<TrainerResponse>.Success(ToResponse(profile));
    }

    private Task<TrainerProfile?> FindAsync(long id, CancellationToken cancellationToken)
    {
        return _dbContext.TrainerProfiles
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.Id == id && !profile.IsDeleted && !profile.User.IsDeleted, cancellationToken);
    }

    private static TrainerResponse ToResponse(TrainerProfile profile)
    {
        return new TrainerResponse(
            profile.Id,
            profile.UserId,
            profile.User.Email,
            profile.User.FullName,
            profile.Specialty,
            profile.Bio,
            profile.Gender,
            profile.DateOfBirth,
            profile.YearsOfExperience,
            profile.CreatedAt);
    }

    private static AuthServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return AuthServiceResult<T>.Failure(code, message, statusCode);
    }
}
