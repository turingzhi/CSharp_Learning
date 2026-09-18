using System.ComponentModel.DataAnnotations;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;

public record AddCommentInWorkItemResponse
(
    Guid Id,
    Guid WorkItemId,
    string Body,
    DateTimeOffset CreatedAt
);