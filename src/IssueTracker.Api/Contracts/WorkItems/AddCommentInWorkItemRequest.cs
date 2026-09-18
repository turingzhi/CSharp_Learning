using System.ComponentModel.DataAnnotations;
using IssueTracker.Api.Contracts;
using IssueTracker.Core.Entities;

public record AddCommentInWorkItemRequest
{
    [Required]
    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;


}