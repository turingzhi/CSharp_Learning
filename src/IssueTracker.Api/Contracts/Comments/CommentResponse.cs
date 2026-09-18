using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Contracts.Comments;

public record CommentResponse(
    Guid Id,
    Guid workItemId,
    string body,
    DateTimeOffset CreatedAt
);