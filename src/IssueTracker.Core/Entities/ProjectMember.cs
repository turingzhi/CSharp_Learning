using IssueTracker.Core.Enums;

namespace IssueTracker.Core.Entities;

public class ProjectMember
{
    private ProjectMember()
    {
        UserId = string.Empty;
    }

    public ProjectMember(
        Guid projectId,
        string userId,
        ProjectRole role)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "Project ID cannot be empty.",
                nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        ProjectId = projectId;
        UserId = userId;
        Role = role;
    }

    public Guid ProjectId { get; private set; }

    public string UserId { get; private set; }

    public ProjectRole Role { get; private set; }
}