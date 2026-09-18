using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssueTracker.Api.Data;
using IssueTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssueTracker.IntegrationTests;

public sealed partial class AuthorizationTests
{
    [Fact]
    public async Task Membership_OnlyOwnerCanAdd_RejectsDuplicatesAndUnknownUsers()
    {
        var route = $"/api/projects/{_projectId}/members";
        foreach (var (client, expected) in new[] {
            (_anonymous, HttpStatusCode.Unauthorized), (_outsider, HttpStatusCode.NotFound),
            (_member, HttpStatusCode.Forbidden) })
        {
            using var denied = await client.PostAsJsonAsync(route, new { userId = _outsiderId });
            Assert.Equal(expected, denied.StatusCode);
        }
        foreach (var userId in new[] { "unknown", " " })
        {
            using var invalid = await _owner.PostAsJsonAsync(route, new { userId });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var added = await _owner.PostAsJsonAsync(route, new { userId = _outsiderId, role = "Owner" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        using var duplicate = await _owner.PostAsJsonAsync(route, new { userId = _outsiderId });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var visible = await _outsider.GetAsync($"/api/projects/{_projectId}");
        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ProjectRole.Member, (await db.ProjectMembers.SingleAsync(m =>
            m.ProjectId == _projectId && m.UserId == _outsiderId)).Role);
    }

    [Fact]
    public async Task Assignment_EnforcesMembership_AndSupportsExplicitUnassignment()
    {
        var route = $"/api/work-items/{_itemId}/assignee";
        foreach (var (client, expected) in new[] {
            (_anonymous, HttpStatusCode.Unauthorized), (_outsider, HttpStatusCode.NotFound) })
        {
            using var denied = await client.PutAsJsonAsync(route, new { userId = _memberId });
            Assert.Equal(expected, denied.StatusCode);
        }
        using var assigned = await _member.PutAsJsonAsync(route, new { userId = _memberId });
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        foreach (var userId in new[] { _outsiderId, "missing", " " })
        {
            using var invalid = await _owner.PutAsJsonAsync(route, new { userId });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var omitted = await _owner.PutAsJsonAsync(route, new { });
        Assert.Equal(HttpStatusCode.BadRequest, omitted.StatusCode);
        var item = await _owner.GetFromJsonAsync<JsonElement>($"/api/work-items/{_itemId}");
        Assert.Equal(_memberId, item.GetProperty("assigneeId").GetString());
        var page = await _owner.GetFromJsonAsync<JsonElement>($"/api/projects/{_projectId}/work-items");
        Assert.Equal(_memberId, page.GetProperty("items")[0].GetProperty("assigneeId").GetString());
        using var cleared = await _member.PutAsJsonAsync(route, new { userId = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        item = await _owner.GetFromJsonAsync<JsonElement>($"/api/work-items/{_itemId}");
        Assert.Equal(JsonValueKind.Null, item.GetProperty("assigneeId").ValueKind);
        using var missing = await _owner.PutAsJsonAsync($"/api/work-items/{Guid.NewGuid()}/assignee", new { userId = _memberId });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Removal_ProtectsOwner_ClearsOnlyThisProjectsAssignments_RevokesAccess()
    {
        var route = $"/api/projects/{_projectId}/members/{_memberId}";
        foreach (var (client, expected) in new[] {
            (_anonymous, HttpStatusCode.Unauthorized), (_outsider, HttpStatusCode.NotFound),
            (_member, HttpStatusCode.Forbidden) })
        {
            using var denied = await client.DeleteAsync(route);
            Assert.Equal(expected, denied.StatusCode);
        }
        using var protectedOwner = await _owner.DeleteAsync($"/api/projects/{_projectId}/members/{_factory.TestUserId}");
        Assert.Equal(HttpStatusCode.Conflict, protectedOwner.StatusCode);
        var otherProject = await CreateAsync(_owner, "/api/projects", new { name = "Other" });
        using var added = await _owner.PostAsJsonAsync($"/api/projects/{otherProject}/members", new { userId = _memberId });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var otherItem = await CreateAsync(_owner, $"/api/projects/{otherProject}/work-items", new { title = "Other" });
        foreach (var id in new[] { _itemId, otherItem })
        {
            using var assigned = await _owner.PutAsJsonAsync($"/api/work-items/{id}/assignee", new { userId = _memberId });
            Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        }
        using var removed = await _owner.DeleteAsync(route);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var repeated = await _owner.DeleteAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, repeated.StatusCode);
        using var deniedRead = await _member.GetAsync($"/api/work-items/{_itemId}");
        Assert.Equal(HttpStatusCode.NotFound, deniedRead.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await db.WorkItems.SingleAsync(w => w.Id == _itemId)).AssigneeId);
        Assert.Equal(_memberId, (await db.WorkItems.SingleAsync(w => w.Id == otherItem)).AssigneeId);
    }
}
