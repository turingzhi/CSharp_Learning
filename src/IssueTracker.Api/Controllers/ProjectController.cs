using IssueTracker.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IssueTracker.Api.Services;
using IssueTracker.Api.Contracts;
using IssueTracker.Core.Entities;
using IssueTracker.Api.Contracts.WorkItems;
using System.Security.Claims;
using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly WorkItemService _workItemService;

    public ProjectController(ProjectService projectService, WorkItemService workItemService)
    {
        _projectService = projectService;
        _workItemService = workItemService;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }
        Project project;
        try
        {
            project = await _projectService.CreateAsync(
                request.Name, request.Description, userId, cancellationToken
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        var response = new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.CreatedAt
        );

        return CreatedAtAction(nameof(GetById), new { id = project.Id }, response);

    }

    [HttpGet("{id:guid}")]
    [ProjectAccess(ProjectResource.Project)]
    public async Task<ActionResult<ProjectResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(
            id,
            cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        return Ok(new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.CreatedAt));
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var projects = await _projectService.GetAllAsync(
            userId,
            cancellationToken);

        var response = projects.Select(project => new ProjectResponse(
            project.Id,
            project.Name,
            project.Description,
            project.CreatedAt
        )).ToList();
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [ProjectAccess(ProjectResource.Project, permission: ProjectPermission.Owner)]
    public async Task<ActionResult> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        Project? project;
        try
        {
            project = await _projectService.UpdateAsync(id, request.Name, request.Description, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400,
                        title: "Invalid input",
                        detail: ex.Message);
        }
        if (project is null)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpDelete("{id:Guid}")]
    [ProjectAccess(ProjectResource.Project, permission: ProjectPermission.Owner)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.DeleteAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{projectId:Guid}/work-items")]
    [ProjectAccess(ProjectResource.Project, "projectId")]
    public async Task<ActionResult<WorkItemResponse>> AddItemInProjectAsync(Guid projectId, AddItemInProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }
        WorkItem workItem;
        try
        {
            workItem = await _workItemService.CreateAsync(projectId, request.Title, request.Description, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid input",
                detail: ex.Message
                );
        }
        var response = new AddItemInProjectResponse(
            workItem.Id,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            workItem.Status,
            workItem.CreatedAt, workItem.AssigneeId);

        return CreatedAtAction("GetById", "WorkItem", new { id = workItem.Id }, response);

    }



    [HttpGet("{projectId:guid}/work-items")]
    [ProjectAccess(ProjectResource.Project, "projectId")]
    public async Task<ActionResult<PagedResponse<WorkItemResponse>>>
        GetAllWorkItemsByProjectId(
            Guid projectId,
            [FromQuery] WorkItemQuery query,
            CancellationToken cancellationToken)
    {
        var project = await _projectService.GetByIdAsync(
            projectId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        var result = await _workItemService.GetAllByProjectIdAsync(
            projectId, query, cancellationToken);

        var items = result.Items.Select(workItem => new WorkItemResponse(
            workItem.Id,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            workItem.Status,
            workItem.CreatedAt,
            workItem.AssigneeId
        )).ToList();

        return Ok(new PagedResponse<WorkItemResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

}
