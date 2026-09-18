namespace IssueTracker.Core.Entities;

public class Comment
{
    private Comment()
    {
        Body = string.Empty;
    }
    public Comment(Guid workItemId, string body, string? authorId = null)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("workItemId cannot be empty");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Empty body");
        }
        var bodyTrimed = body.Trim();
        if (bodyTrimed.Length > 2000)
        {
            throw new ArgumentException("The comment body is too long");
        }
        Id = Guid.NewGuid();
        // Name = nameTrimed;
        // Description = description;
        WorkItemId = workItemId;
        AuthorId = authorId;
        Body = bodyTrimed;
        CreatedAt = DateTimeOffset.UtcNow;

    }
    public Guid Id { get; private set; }
    public Guid WorkItemId { get; private set; }
    // Older comments have no recorded author; only an Owner can delete them.
    public string? AuthorId { get; private set; }
    public string Body { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

}