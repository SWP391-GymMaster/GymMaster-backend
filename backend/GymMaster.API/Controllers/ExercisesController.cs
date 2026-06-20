using GymMaster.API.Data;
using GymMaster.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymMaster.API.Controllers;

[ApiController]
[Route("api/v1/exercises")]
[Authorize]
public sealed class ExercisesController : ApiControllerBase
{
    private readonly GymMasterDbContext _dbContext;

    public ExercisesController(GymMasterDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // GET /api/v1/exercises — danh sach bai tap active (PT dung khi tao giao an)
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var exercises = await _dbContext.ExerciseCatalogs
            .Where(e => e.IsActive)
            .OrderBy(e => e.MuscleGroup)
            .ThenBy(e => e.Name)
            .Select(e => new ExerciseCatalogResponse(e.Id, e.Name, e.MuscleGroup, e.Description))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<ExerciseCatalogResponse>>.Ok(exercises));
    }
}
