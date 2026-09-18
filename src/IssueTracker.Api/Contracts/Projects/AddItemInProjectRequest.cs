using System.ComponentModel.DataAnnotations;
using IssueTracker.Api.Contracts;
using IssueTracker.Core.Entities;

public record AddItemInProjectRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

}