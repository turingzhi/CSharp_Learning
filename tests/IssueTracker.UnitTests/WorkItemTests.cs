using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;
using Xunit;

namespace IssueTracker.UnitTests;

public class WorkItemTests
{
    [Fact]
    public void Constructor_TrimsTitleAndInitializesItem()
    {
        var projectId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var item = new WorkItem(projectId, "  Learn C#  ");

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(projectId, item.ProjectId);
        Assert.Equal("Learn C#", item.Title);
        Assert.Equal(WorkItemStatus.Todo, item.Status);
        Assert.InRange(item.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, item.CreatedAt.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingTitle(string? title)
    {
        Assert.Throws<ArgumentException>(() => new WorkItem(Guid.NewGuid(), title!));
    }

    [Fact]
    public void Constructor_RejectsEmptyProjectId()
    {
        Assert.Throws<ArgumentException>(() => new WorkItem(Guid.Empty, "Learn C#"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Constructor_AcceptsTitleAtLengthBoundaries(int length)
    {
        var item = new WorkItem(Guid.NewGuid(), new string('a', length));
        Assert.Equal(length, item.Title.Length);
    }

    [Fact]
    public void Constructor_RejectsTitleOverLimit()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkItem(Guid.NewGuid(), new string('a', 201)));
    }

    [Fact]
    public void Rename_InvalidTitlePreservesExistingTitle()
    {
        var item = new WorkItem(Guid.NewGuid(), "Original");

        Assert.Throws<ArgumentException>(() => item.Rename("  "));

        Assert.Equal("Original", item.Title);
    }

    [Theory]
    [InlineData(WorkItemStatus.Todo, WorkItemStatus.InProgress)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.Done)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.InProgress)]
    public void ChangeStatus_AllowsWorkflowTransitions(
        WorkItemStatus initial, WorkItemStatus next)
    {
        var item = CreateAt(initial);
        item.ChangeStatus(next);
        Assert.Equal(next, item.Status);
    }

    [Theory]
    [InlineData(WorkItemStatus.Todo)]
    [InlineData(WorkItemStatus.InProgress)]
    [InlineData(WorkItemStatus.Done)]
    public void ChangeStatus_CurrentStatusIsNoOp(WorkItemStatus status)
    {
        var item = CreateAt(status);
        item.ChangeStatus(status);
        Assert.Equal(status, item.Status);
    }

    [Theory]
    [InlineData(WorkItemStatus.Todo, WorkItemStatus.Done)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.Todo)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.Todo)]
    public void ChangeStatus_RejectsInvalidTransitionAndPreservesStatus(
        WorkItemStatus initial, WorkItemStatus next)
    {
        var item = CreateAt(initial);
        Assert.Throws<InvalidOperationException>(() => item.ChangeStatus(next));
        Assert.Equal(initial, item.Status);
    }

    [Fact]
    public void ChangeStatus_RejectsUndefinedEnumValue()
    {
        var item = CreateAt(WorkItemStatus.Todo);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            item.ChangeStatus((WorkItemStatus)999));
        Assert.Equal(WorkItemStatus.Todo, item.Status);
    }

    private static WorkItem CreateAt(WorkItemStatus status)
    {
        var item = new WorkItem(Guid.NewGuid(), "Learn C#");
        if (status is WorkItemStatus.InProgress or WorkItemStatus.Done)
            item.ChangeStatus(WorkItemStatus.InProgress);
        if (status is WorkItemStatus.Done)
            item.ChangeStatus(WorkItemStatus.Done);
        return item;
    }
}
