using System.ComponentModel.DataAnnotations;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;

public record AddItemInProjectResponse
(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    WorkItemStatus Status,
    DateTimeOffset CreatedAt,
    string? AssigneeId = null
);