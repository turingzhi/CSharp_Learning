using System.ComponentModel.DataAnnotations;
namespace IssueTracker.Api.Contracts;

public record CreateProjectRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Description { get; set; }
}