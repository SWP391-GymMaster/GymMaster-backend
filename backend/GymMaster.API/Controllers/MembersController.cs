using GymMaster.API.DTOs;
using GymMaster.API.Entities;
using GymMaster.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymMaster.API.Controllers;


[ApiController]
[Route("api/v1/members")]
[Authorize]
public sealed class MembersController : ApiControllerBase
{
    private readonly IMemberService _memberService;
    private readonly IProgressService _progressService;
    private readonly IWorkoutPlanService _workoutPlanService;
    private readonly ITrainerNoteService _trainerNoteService;

    public MembersController(
        IMemberService memberService,
        IProgressService progressService,
        IWorkoutPlanService workoutPlanService,
        ITrainerNoteService trainerNoteService)
    {
        _memberService = memberService;
        _progressService = progressService;
        _workoutPlanService = workoutPlanService;
        _trainerNoteService = trainerNoteService;
    }

    // FR-MEM-01
    [HttpPost]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> Create(
        [FromBody] CreateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.CreateAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-02
    [HttpGet]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff}")]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _memberService.ListAsync(query, page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/profile-360")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> GetMyProfile360(CancellationToken cancellationToken)
    {
        var memberIdResult = await _memberService.GetCurrentMemberProfileIdAsync(User, cancellationToken);
        if (!memberIdResult.Succeeded)
        {
            return ToActionResult(memberIdResult);
        }

        var result = await _progressService.GetProfile360Async(memberIdResult.Value, User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/workout-plans")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> GetMyWorkoutPlans(CancellationToken cancellationToken)
    {
        var memberIdResult = await _memberService.GetCurrentMemberProfileIdAsync(User, cancellationToken);
        if (!memberIdResult.Succeeded)
        {
            return ToActionResult(memberIdResult);
        }

        var result = await _workoutPlanService.GetByMemberAsync(memberIdResult.Value, User, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me/notes")]
    [Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> GetMyNotes(CancellationToken cancellationToken)
    {
        var memberIdResult = await _memberService.GetCurrentMemberProfileIdAsync(User, cancellationToken);
        if (!memberIdResult.Succeeded)
        {
            return ToActionResult(memberIdResult);
        }

        var result = await _trainerNoteService.GetByMemberAsync(memberIdResult.Value, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-05: ownership check trong service (Member chi xem cua minh).
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var result = await _memberService.GetByIdAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // NOTE(part-y): Member 360 CHI co MOT ban duy nhat = ProgressService.GetProfile360Async
    // (canonical o {id}/profile-360, spec 006: lich su goi + tien do + dinh duong + PT).
    // Route {id}/360 (alias) + me/profile-360 (self) deu goi cung ban Y nay -> het ban trung.
    [HttpGet("{id:long}/360")]
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Staff},{RoleNames.Pt},{RoleNames.Member}")]
    public async Task<IActionResult> Get360(long id, CancellationToken cancellationToken)
    {
        var result = await _progressService.GetProfile360Async(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-03
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        [FromBody] UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.UpdateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-MEM-04 (soft-delete)
    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _memberService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : ToActionResult(result);
    }

    // FR-WP-01 / AC-04: PT tao giao an cho member duoc phan cong.
    [HttpPost("{id:long}/workout-plans")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> CreateWorkoutPlan(
        long id,
        [FromBody] CreateWorkoutPlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workoutPlanService.CreateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-PT-04 / AC-06: PT(assigned)/Admin/Member(self) xem giao an.
    [HttpGet("{id:long}/workout-plans")]
    [Authorize(Roles = $"{RoleNames.Pt},{RoleNames.Admin},{RoleNames.Member}")]
    public async Task<IActionResult> GetWorkoutPlans(long id, CancellationToken cancellationToken)
    {
        var result = await _workoutPlanService.GetByMemberAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-NOTE-01: PT ghi note cho member duoc phan cong.
    [HttpPost("{id:long}/notes")]
    [Authorize(Roles = RoleNames.Pt)]
    public async Task<IActionResult> CreateNote(
        long id,
        [FromBody] CreateTrainerNoteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _trainerNoteService.CreateAsync(id, request, User, cancellationToken);
        return ToActionResult(result);
    }

    // FR-PT-04: PT(assigned)/Admin/Member(self) xem ghi chu.
    [HttpGet("{id:long}/notes")]
    [Authorize(Roles = $"{RoleNames.Pt},{RoleNames.Admin},{RoleNames.Member}")]
    public async Task<IActionResult> GetNotes(long id, CancellationToken cancellationToken)
    {
        var result = await _trainerNoteService.GetByMemberAsync(id, User, cancellationToken);
        return ToActionResult(result);
    }
}
