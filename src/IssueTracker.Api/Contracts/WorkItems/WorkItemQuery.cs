using System.ComponentModel.DataAnnotations;
using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Contracts.WorkItems;

public class WorkItemQuery
{
    [EnumDataType(typeof(WorkItemStatus))]
    public WorkItemStatus? Status { get; set; }

    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}