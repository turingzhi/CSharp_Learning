using IssueTracker.Api.Contracts;
using IssueTracker.Api.Security;
using IssueTracker.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IssueTracker.Api.Controllers;

[Authorize]
[ApiController]
public sealed class MembershipController(MembershipService service) : ControllerBase
{
    [HttpPost("api/projects/{projectId:guid}/members")]
    [ProjectAccess(ProjectResource.Project, "projectId", ProjectPermission.Owner)]
    public async Task<IActionResult> Add(Guid projectId, AddMemberRequest request, CancellationToken ct)
    {
        var result = await service.AddAsync(projectId, request.UserId, ct);
        return result switch
        {
            MembershipResult.Success => StatusCode(201, new { projectId, request.UserId, role = "Member" }),
            MembershipResult.InvalidUser => Problem(statusCode: 400, title: "User does not exist."),
            MembershipResult.Duplicate => Problem(statusCode: 409, title: "User already belongs to this project."),
            _ => NotFound()
        };
    }

    [HttpDelete("api/projects/{projectId:guid}/members/{userId}")]
    [ProjectAccess(ProjectResource.Project, "projectId", ProjectPermission.Owner)]
    public async Task<IActionResult> Remove(Guid projectId, string userId, CancellationToken ct)
    {
        var result = await service.RemoveAsync(projectId, userId, ct);
        return result switch
        {
            MembershipResult.Success => NoContent(),
            MembershipResult.OwnerProtected => Problem(statusCode: 409, title: "The project Owner cannot be removed."),
            _ => NotFound()
        };
    }

    [HttpPut("api/work-items/{id:guid}/assignee")]
    [ProjectAccess(ProjectResource.WorkItem)]
    public async Task<IActionResult> Assign(Guid id, AssignWorkItemRequest request, CancellationToken ct)
    {
        var result = await service.AssignAsync(id, request.UserId, ct);
        return result switch
        {
            MembershipResult.Success => NoContent(),
            MembershipResult.InvalidUser => Problem(statusCode: 400, title: "Assignee must be a member of this project."),
            _ => NotFound()
        };
    }
}
