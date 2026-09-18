using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IssueTracker.Api.Contracts;

public sealed class AddMemberRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
}

public sealed class AssignWorkItemRequest
{
    // Explicit null means unassign; an omitted property is a malformed request.
    [JsonRequired]
    public string? UserId { get; set; }
}
