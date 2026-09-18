using System.Security.Claims;
using IssueTracker.Api.Data;
using IssueTracker.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace IssueTracker.Api.Security;

public enum ProjectResource { Project, WorkItem, Comment }
public enum ProjectPermission { Member, Owner, CommentAuthorOrOwner }

// An endpoint declares its resource and rule; the filter performs the shared check.
public sealed class ProjectAccessAttribute : TypeFilterAttribute
{
    public ProjectAccessAttribute(ProjectResource resource, string routeParameter = "id",
        ProjectPermission permission = ProjectPermission.Member)
        : base(typeof(ProjectAccessFilter))
    {
        Arguments = new object[] { resource, routeParameter, permission };
    }
}

public sealed class ProjectAccessFilter(
    AppDbContext db, ProjectResource resource, string routeParameter,
    ProjectPermission permission) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!Guid.TryParse(context.RouteData.Values[routeParameter]?.ToString(), out var id))
        {
            context.Result = new NotFoundResult();
            return;
        }

        var ct = context.HttpContext.RequestAborted;
        Guid? projectId = null;
        string? authorId = null;
        switch (resource)
        {
            case ProjectResource.Project:
                projectId = id;
                break;
            case ProjectResource.WorkItem:
                projectId = await db.WorkItems.Where(w => w.Id == id)
                    .Select(w => (Guid?)w.ProjectId).SingleOrDefaultAsync(ct);
                break;
            case ProjectResource.Comment:
                var comment = await (from c in db.Comments
                    join w in db.WorkItems on c.WorkItemId equals w.Id
                    where c.Id == id
                    select new { w.ProjectId, c.AuthorId }).SingleOrDefaultAsync(ct);
                projectId = comment?.ProjectId;
                authorId = comment?.AuthorId;
                break;
        }

        var role = await db.ProjectMembers
            .Where(m => m.ProjectId == projectId && m.UserId == userId)
            .Select(m => (ProjectRole?)m.Role).SingleOrDefaultAsync(ct);

        // Missing and inaccessible resources deliberately have the same response.
        if (role is null)
        {
            context.Result = new NotFoundResult();
            return;
        }

        var allowed = permission switch
        {
            ProjectPermission.Member => true,
            ProjectPermission.Owner => role == ProjectRole.Owner,
            ProjectPermission.CommentAuthorOrOwner => role == ProjectRole.Owner || authorId == userId,
            _ => false
        };
        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
