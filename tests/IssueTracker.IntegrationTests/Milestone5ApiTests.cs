using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IssueTracker.Api.Data;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssueTracker.IntegrationTests;

// Together with ProjectApiTests, covers all 15 milestone 5 routes.
// Seed directly through EF so a broken creation endpoint does not mask other routes.
public sealed class Milestone5ApiTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        $"issuetracker-milestone5-{Guid.NewGuid():N}");
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Project _project = null!;
    private WorkItem _item = null!;
    private Comment _comment = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _factory = new ApiFactory(Path.Combine(_directory, "test.db"));
        _client = await _factory.CreateReadyClientAsync();
        _project = new Project("Seed project");
        _item = new WorkItem(_project.Id, "Seed item", "Original description");
        _comment = new Comment(_item.Id, "Seed comment");
        await InDatabase(async db =>
        {
            db.AddRange(_project, _item, _comment,
                new ProjectMember(_project.Id, _factory.TestUserId, ProjectRole.Owner));
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private async Task InDatabase(Func<AppDbContext, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<HttpResponseMessage> Send(string method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) request.Content = JsonContent.Create(body);
        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        var json = await Json(response, status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task CreateWorkItem_Returns201_StringTodo_ServerFields_AndWorkingLocation()
    {
        var before = DateTimeOffset.UtcNow;
        using var response = await Send("POST", $"/api/projects/{_project.Id}/work-items", new
        {
            title = "  New item  ", description = "Details", id = Guid.NewGuid(),
            projectId = Guid.NewGuid(), status = "Done", createdAt = "2000-01-01T00:00:00Z"
        });
        var json = await Json(response, HttpStatusCode.Created);
        var id = json.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(_project.Id, json.GetProperty("projectId").GetGuid());
        Assert.Equal("New item", json.GetProperty("title").GetString());
        Assert.Equal("Details", json.GetProperty("description").GetString());
        Assert.Equal("Todo", json.GetProperty("status").GetString());
        var timestamp = json.GetProperty("createdAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.InRange(timestamp, before, DateTimeOffset.UtcNow);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/api/work-items/{id}", new Uri(_client.BaseAddress!, response.Headers.Location).AbsolutePath);
        using var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(json.GetRawText(), (await Json(fetched, HttpStatusCode.OK)).GetRawText());
    }

    [Theory]
    [InlineData("POST", 1)]
    [InlineData("POST", 200)]
    [InlineData("PUT", 1)]
    [InlineData("PUT", 200)]
    public async Task WorkItem_ValidTitleBoundaries_AndMaximumDescription(string method, int length)
    {
        var title = new string('x', length);
        var description = new string('d', 2000);
        var url = method == "POST" ? $"/api/projects/{_project.Id}/work-items" : $"/api/work-items/{_item.Id}";
        using var response = await Send(method, url, new { title, description });
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.NoContent, response.StatusCode);
        await InDatabase(async db => Assert.True(await db.WorkItems.AnyAsync(w => w.Title == title && w.Description == description)));
    }

    [Fact]
    public async Task ListWorkItems_ContainsOnlyRequestedProjectsItems_AndEmptyListForExistingProject()
    {
        var other = new Project("Other");
        var otherItem = new WorkItem(other.Id, "Unrelated");
        var empty = new Project("Empty");
        await InDatabase(async db => { db.AddRange(other, otherItem, empty, new ProjectMember(empty.Id, _factory.TestUserId, ProjectRole.Owner)); await db.SaveChangesAsync(); });
        using var response = await Send("GET", $"/api/projects/{_project.Id}/work-items");
        var page = await Json(response, HttpStatusCode.OK);
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(_item.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("Todo", item.GetProperty("status").GetString());
        using var emptyResponse = await Send("GET", $"/api/projects/{empty.Id}/work-items");
        var emptyPage = await Json(emptyResponse, HttpStatusCode.OK);
        Assert.Empty(emptyPage.GetProperty("items").EnumerateArray());
        Assert.Equal(0, emptyPage.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetWorkItem_ReturnsItsFieldsAndStringStatus()
    {
        using var response = await Send("GET", $"/api/work-items/{_item.Id}");
        var json = await Json(response, HttpStatusCode.OK);
        Assert.Equal(_item.Id, json.GetProperty("id").GetGuid());
        Assert.Equal(_project.Id, json.GetProperty("projectId").GetGuid());
        Assert.Equal(_item.Title, json.GetProperty("title").GetString());
        Assert.Equal(_item.Description, json.GetProperty("description").GetString());
        Assert.Equal(_item.CreatedAt, json.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("Todo", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateWorkItem_PreservesProjectStatusAndTimestamp_AndClearsOmittedDescription()
    {
        await InDatabase(async db =>
        {
            (await db.WorkItems.SingleAsync(w => w.Id == _item.Id)).ChangeStatus(WorkItemStatus.InProgress);
            await db.SaveChangesAsync();
        });
        using var response = await Send("PUT", $"/api/work-items/{_item.Id}", new
        {
            title = "  Updated  ", projectId = Guid.NewGuid(), status = "Done", createdAt = "2000-01-01T00:00:00Z"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        await InDatabase(async db =>
        {
            var saved = await db.WorkItems.SingleAsync(w => w.Id == _item.Id);
            Assert.Equal("Updated", saved.Title);
            Assert.Null(saved.Description);
            Assert.Equal(_item.ProjectId, saved.ProjectId);
            Assert.Equal(_item.CreatedAt, saved.CreatedAt);
            Assert.Equal(WorkItemStatus.InProgress, saved.Status);
        });
    }

    public static IEnumerable<object[]> InvalidWorkItems()
    {
        foreach (var method in new[] { "POST", "PUT" })
            foreach (var title in new string?[] { null, "", "   ", new string('x', 201) })
                yield return new object[] { method, JsonSerializer.Serialize(new { title }) };
        foreach (var method in new[] { "POST", "PUT" })
            foreach (var json in new[] { "{}", "{", "{\"title\":123}", JsonSerializer.Serialize(new { title = "Valid", description = new string('d', 2001) }) })
                yield return new object[] { method, json };
    }

    [Theory]
    [MemberData(nameof(InvalidWorkItems))]
    public async Task WorkItem_InvalidInput_Returns400_AndDoesNotChangeData(string method, string json)
    {
        var url = method == "POST" ? $"/api/projects/{_project.Id}/work-items" : $"/api/work-items/{_item.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        using var response = await _client.SendAsync(request);
        await Problem(response, HttpStatusCode.BadRequest);
        await InDatabase(async db =>
        {
            var saved = Assert.Single(await db.WorkItems.ToListAsync());
            Assert.Equal(_item.Title, saved.Title);
            Assert.Equal(_item.Description, saved.Description);
        });
    }

    [Theory]
    [InlineData("Todo", "Todo", 204)]
    [InlineData("Todo", "InProgress", 204)]
    [InlineData("InProgress", "InProgress", 204)]
    [InlineData("InProgress", "Done", 204)]
    [InlineData("Done", "Done", 204)]
    [InlineData("Done", "InProgress", 204)]
    [InlineData("Todo", "Done", 409)]
    [InlineData("InProgress", "Todo", 409)]
    [InlineData("Done", "Todo", 409)]
    public async Task Status_AllTransitions_AndNoOps(string initial, string next, int expected)
    {
        await InDatabase(async db =>
        {
            var item = await db.WorkItems.SingleAsync();
            if (initial != "Todo") item.ChangeStatus(WorkItemStatus.InProgress);
            if (initial == "Done") item.ChangeStatus(WorkItemStatus.Done);
            await db.SaveChangesAsync();
        });
        using var response = await Send("PATCH", $"/api/work-items/{_item.Id}/status", new { status = next });
        if (expected == 409) await Problem(response, HttpStatusCode.Conflict);
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
        await InDatabase(async db => Assert.Equal(expected == 204 ? next : initial,
            (await db.WorkItems.SingleAsync()).Status.ToString()));
    }

    [Theory]
    [InlineData("{\"status\":\"Unknown\"}")]
    [InlineData("{\"status\":999}")]
    [InlineData("{\"status\":1}")]
    [InlineData("{\"status\":null}")]
    [InlineData("{}")]
    [InlineData("{")]
    public async Task Status_InvalidOrMissingValue_Returns400_AndPreservesState(string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/work-items/{_item.Id}/status")
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        using var response = await _client.SendAsync(request);
        await Problem(response, HttpStatusCode.BadRequest);
        await InDatabase(async db => Assert.Equal(WorkItemStatus.Todo, (await db.WorkItems.SingleAsync()).Status));
    }

    [Fact]
    public async Task CreateComment_TrimsBody_Returns201_AndWorkingLocation()
    {
        using var response = await Send("POST", $"/api/work-items/{_item.Id}/comments", new
        { body = "  New comment  ", workItemId = Guid.NewGuid(), id = Guid.NewGuid(), createdAt = "2000-01-01T00:00:00Z" });
        var json = await Json(response, HttpStatusCode.Created);
        var id = json.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(_item.Id, json.GetProperty("workItemId").GetGuid());
        Assert.Equal("New comment", json.GetProperty("body").GetString());
        Assert.True(json.GetProperty("createdAt").GetDateTimeOffset().Year > 2000);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/api/comments/{id}", new Uri(_client.BaseAddress!, response.Headers.Location).AbsolutePath);
        using var fetched = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(json.GetRawText(), (await Json(fetched, HttpStatusCode.OK)).GetRawText());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2000)]
    public async Task Comment_ValidBodyBoundaries(int length)
    {
        using var response = await Send("POST", $"/api/work-items/{_item.Id}/comments", new { body = new string('x', length) });
        var json = await Json(response, HttpStatusCode.Created);
        Assert.Equal(length, json.GetProperty("body").GetString()!.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("oversized")]
    public async Task Comment_InvalidBody_Returns400_WithoutSaving(string? body)
    {
        if (body == "oversized") body = new string('x', 2001);
        using var response = await Send("POST", $"/api/work-items/{_item.Id}/comments", new { body });
        await Problem(response, HttpStatusCode.BadRequest);
        await InDatabase(async db => Assert.Equal(_comment.Id, Assert.Single(await db.Comments.ToListAsync()).Id));
    }

    [Fact]
    public async Task GetComment_ReturnsStoredFields()
    {
        using var response = await Send("GET", $"/api/comments/{_comment.Id}");
        var json = await Json(response, HttpStatusCode.OK);
        Assert.Equal(_comment.Id, json.GetProperty("id").GetGuid());
        Assert.Equal(_item.Id, json.GetProperty("workItemId").GetGuid());
        Assert.Equal(_comment.Body, json.GetProperty("body").GetString());
        Assert.Equal(_comment.CreatedAt, json.GetProperty("createdAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ListComments_FiltersByWorkItem_AndReturnsEmptyArrayForExistingItem()
    {
        var other = new WorkItem(_project.Id, "Other");
        var empty = new WorkItem(_project.Id, "Empty");
        await InDatabase(async db => { db.AddRange(other, empty, new Comment(other.Id, "Unrelated")); await db.SaveChangesAsync(); });
        using var response = await Send("GET", $"/api/work-items/{_item.Id}/comments");
        var comment = Assert.Single((await Json(response, HttpStatusCode.OK)).EnumerateArray());
        Assert.Equal(_comment.Id, comment.GetProperty("id").GetGuid());
        using var emptyResponse = await Send("GET", $"/api/work-items/{empty.Id}/comments");
        Assert.Empty((await Json(emptyResponse, HttpStatusCode.OK)).EnumerateArray());
    }

    [Theory]
    [InlineData("DELETE", "/api/projects/{id}")]
    [InlineData("POST", "/api/projects/{id}/work-items")]
    [InlineData("GET", "/api/projects/{id}/work-items")]
    [InlineData("GET", "/api/work-items/{id}")]
    [InlineData("PUT", "/api/work-items/{id}")]
    [InlineData("PATCH", "/api/work-items/{id}/status")]
    [InlineData("DELETE", "/api/work-items/{id}")]
    [InlineData("POST", "/api/work-items/{id}/comments")]
    [InlineData("GET", "/api/work-items/{id}/comments")]
    [InlineData("GET", "/api/comments/{id}")]
    [InlineData("DELETE", "/api/comments/{id}")]
    public async Task MissingResource_Returns404(string method, string route)
    {
        object? body = method switch
        {
            "PATCH" => new { status = "InProgress" },
            "POST" when route.EndsWith("comments") => new { body = "Valid" },
            "POST" or "PUT" => new { title = "Valid" },
            _ => null
        };
        using var response = await Send(method, route.Replace("{id}", Guid.NewGuid().ToString()), body);
        await Problem(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("projects")]
    [InlineData("work-items")]
    [InlineData("comments")]
    public async Task Delete_CascadesOnlyWithinTarget_AndRepeatedDeleteReturns404(string resource)
    {
        var otherProject = new Project("Unaffected");
        var otherItem = new WorkItem(otherProject.Id, "Unaffected");
        var otherComment = new Comment(otherItem.Id, "Unaffected");
        await InDatabase(async db => { db.AddRange(otherProject, otherItem, otherComment); await db.SaveChangesAsync(); });
        var id = resource == "projects" ? _project.Id : resource == "work-items" ? _item.Id : _comment.Id;
        var url = $"/api/{resource}/{id}";
        using var response = await Send("DELETE", url);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        // Verify actual database rows, so a broken GET cannot hide missing cascade behavior.
        await InDatabase(async db =>
        {
            Assert.Equal(resource != "projects", await db.Projects.AnyAsync(p => p.Id == _project.Id));
            Assert.Equal(resource == "comments", await db.WorkItems.AnyAsync(w => w.Id == _item.Id));
            Assert.False(await db.Comments.AnyAsync(c => c.Id == _comment.Id));
            Assert.True(await db.Projects.AnyAsync(p => p.Id == otherProject.Id));
            Assert.True(await db.WorkItems.AnyAsync(w => w.Id == otherItem.Id));
            Assert.True(await db.Comments.AnyAsync(c => c.Id == otherComment.Id));
        });
        using var repeated = await Send("DELETE", url);
        await Problem(repeated, HttpStatusCode.NotFound);
    }
}
