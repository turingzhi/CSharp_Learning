namespace IssueTracker.Api.Contracts;

public record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt
);