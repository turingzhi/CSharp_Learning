using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Contracts.WorkItems;

public record WorkItemResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    WorkItemStatus Status,
    DateTimeOffset CreatedAt,
    string? AssigneeId = null
);