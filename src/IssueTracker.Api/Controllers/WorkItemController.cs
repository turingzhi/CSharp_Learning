using IssueTracker.Api.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IssueTracker.Api.Services;
using IssueTracker.Api.Contracts.WorkItems;
using IssueTracker.Core.Entities;
using IssueTracker.Api.Contracts.Comments;

namespace IssueTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/work-items")]
public class WorkItemController : ControllerBase
{
    private readonly WorkItemService _workItemService;
    private readonly CommentService _commentService;

    public WorkItemController(WorkItemService workItemService, CommentService commentService)
    {
        _workItemService = workItemService;
        _commentService = commentService;
    }





    [HttpGet("{id:guid}")]
    [ProjectAccess(ProjectResource.WorkItem)]
    public async Task<ActionResult<WorkItemResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var workItem = await _workItemService.GetByIdAsync(id, cancellationToken);
        if (workItem is null)
            return NotFound();
        return Ok(new WorkItemResponse(
            workItem.Id,
            workItem.ProjectId,
            workItem.Title,
            workItem.Description,
            workItem.Status,
            workItem.CreatedAt, workItem.AssigneeId));
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkItemResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var WorkItems = await _workItemService.GetAllAsync(userId, cancellationToken);

        var response = WorkItems.Select(WorkItem => new WorkItemResponse(
            WorkItem.Id,
            WorkItem.ProjectId,
            WorkItem.Title,
            WorkItem.Description,
            WorkItem.Status,
            WorkItem.CreatedAt,
            WorkItem.AssigneeId
        )).ToList();
        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    [ProjectAccess(ProjectResource.WorkItem)]
    public async Task<ActionResult> UpdateAsync(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken)
    {
        WorkItem? workItem;
        try
        {
            workItem = await _workItemService.UpdateAsync(id, request.Title, request.Description, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400,
                        title: "Invalid input",
                        detail: ex.Message);
        }
        if (workItem is null)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpDelete("{id:Guid}")]
    [ProjectAccess(ProjectResource.WorkItem, permission: ProjectPermission.Owner)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workItemService.DeleteAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }





    [HttpPatch("{id:guid}/status")]
    [ProjectAccess(ProjectResource.WorkItem)]
    public async Task<ActionResult> ChangeStatusAsync(
        Guid id,
        ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {

        if (request.Status is null) {
            return Problem(
                statusCode: 400,
                title: "Invalid status",
                detail: "Status is required."
            );
        }
        WorkItem? workItem;

        try
        {
            workItem = await _workItemService.ChangeStatusAsync(
                id,
                request.Status.Value,
                cancellationToken
            );
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid status",
                detail: ex.Message
            );
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: 409,
                title: "Invalid status transition",
                detail: ex.Message
            );
        }

        if (workItem is null)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{workItemId:Guid}/comments")]
    [ProjectAccess(ProjectResource.WorkItem, "workItemId")]
    public async Task<ActionResult<AddCommentInWorkItemResponse>> AddItemInProjectAsync(Guid workItemId, AddCommentInWorkItemRequest request, CancellationToken cancellationToken)
    {
        var workItem = await _workItemService.GetByIdAsync(workItemId, cancellationToken);
        if (workItem is null)
        {
            return NotFound();
        }
        Comment? comment;
        try
        {
            comment = await _commentService.CreateAsync(workItemId, request.Body,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid input",
                detail: ex.Message
                );
        }
        var response = new AddCommentInWorkItemResponse(
            comment.Id,
            comment.WorkItemId,
            comment.Body,
            comment.CreatedAt);

        return CreatedAtAction("GetById", "Comment", new { id = comment.Id }, response);

    }

    [HttpGet("{workItemId:Guid}/comments")]
    [ProjectAccess(ProjectResource.WorkItem, "workItemId")]
    public async Task<ActionResult<List<CommentResponse>>> GetAllCommentsByWorkItemId(Guid workItemId, CancellationToken cancellationToken)
    {
        WorkItem? workItem;
        workItem = await _workItemService.GetByIdAsync(workItemId, cancellationToken);
        if (workItem is null)
        {
            return NotFound();
        }
        List<Comment> comments;
        comments = await _commentService.GetAllCommentsByWorkItemIdAsync(workItemId, cancellationToken);
        var response = comments.Select(comment => new CommentResponse(
            comment.Id,
            comment.WorkItemId,
            comment.Body,
            comment.CreatedAt
            )).ToList();

        return Ok(response);

    }
     

}
