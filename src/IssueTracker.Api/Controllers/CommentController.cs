using IssueTracker.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IssueTracker.Api.Services;
using IssueTracker.Api.Contracts.Comments;
using IssueTracker.Core.Entities;

namespace IssueTracker.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/comments")]
public class CommentController : ControllerBase
{
    private readonly CommentService _commentService;

    public CommentController(CommentService commentService)
    {
        _commentService = commentService;
    }


    [HttpGet("{id:guid}")]
    [ProjectAccess(ProjectResource.Comment)]
    public async Task<ActionResult<CommentResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var comment = await _commentService.GetByIdAsync(id, cancellationToken);
        if (comment is null)
            return NotFound();
        return Ok(new CommentResponse(
            comment.Id,
            comment.WorkItemId,
            comment.Body,
            comment.CreatedAt));
    }

 
    [HttpDelete("{id:Guid}")]
    [ProjectAccess(ProjectResource.Comment, permission: ProjectPermission.CommentAuthorOrOwner)]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _commentService.DeleteAsync(id, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

}
