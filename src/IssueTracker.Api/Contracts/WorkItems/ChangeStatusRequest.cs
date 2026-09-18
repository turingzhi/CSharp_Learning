using System.ComponentModel.DataAnnotations;
using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Contracts.WorkItems;

public record ChangeStatusRequest {
    [Required]
    public WorkItemStatus? Status { get; set; }
}