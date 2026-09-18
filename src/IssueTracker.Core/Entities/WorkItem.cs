namespace IssueTracker.Core.Entities;

using System.Diagnostics.Tracing;
using IssueTracker.Core.Enums;

public class WorkItem
{
    private WorkItem() {
        Title = string.Empty;
    }
    public WorkItem(Guid projectId, string title, string? description = null)
    {
        // constructor implementation
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("projectId cannot be empty");
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("title cannot be null or white space");
        }
        string title_trimed = title.Trim();
        if (title_trimed.Length > 200)
        {
            throw new ArgumentException("the length of title is over 200");
        }
        if (!string.IsNullOrEmpty(description) && description.Length > 2000) 
        {
            throw new ArgumentException("The desc is too long");

        }
        Id = Guid.NewGuid();
        ProjectId = projectId;
        Title = title_trimed;
        Description = description;
        Status = WorkItemStatus.Todo;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public string? AssigneeId { get; private set; }

    public void AssignTo(string? userId)
    {
        if (userId is not null && string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("Assignee ID cannot be blank.", nameof(userId));
        AssigneeId = userId;
    }

    public WorkItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Rename(string title)
    {
        Update(title, Description);
    }
    public void Update(string title, string? description)
    {
        // implementation
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("title cannot be empty");
        }
        string title_trimed = title.Trim();
        if (title_trimed.Length > 200)
        {
            throw new ArgumentException("title length cannot be over 200");
        }
        string? desc_trimed = description?.Trim();
        if (desc_trimed?.Length > 2000)
        {
            throw new ArgumentException("desc length cannot be over 2000");
        }
        Title = title_trimed;
        Description = desc_trimed;
    }

    public void ChangeStatus(WorkItemStatus nextStatus)
    {
        // implementation
        if (!Enum.IsDefined(nextStatus))
        {
            throw new ArgumentOutOfRangeException("next status is undefined");
        }
        if (Status == nextStatus)
        {
            return;
        }
        bool isAllowed = (Status == WorkItemStatus.Todo && nextStatus == WorkItemStatus.InProgress) || (Status == WorkItemStatus.InProgress && nextStatus == WorkItemStatus.Done) || (Status == WorkItemStatus.Done && nextStatus == WorkItemStatus.InProgress);

        if (!isAllowed)
        {
            throw new InvalidOperationException("the transmisstion is not allowed");

        }

        Status = nextStatus;
    }

}