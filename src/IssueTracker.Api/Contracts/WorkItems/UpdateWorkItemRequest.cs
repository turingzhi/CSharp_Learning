using System.ComponentModel.DataAnnotations;
namespace IssueTracker.Api.Contracts.WorkItems;

public record UpdateWorkItemRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Description { get; set; }
}