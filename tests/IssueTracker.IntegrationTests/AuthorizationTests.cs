using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IssueTracker.Api.Data;
using IssueTracker.Api.Security;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssueTracker.IntegrationTests;

public sealed partial class AuthorizationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _owner = null!;
    private HttpClient _member = null!;
    private HttpClient _outsider = null!;
    private HttpClient _anonymous = null!;
    private Guid _projectId;
    private Guid _itemId;
    private Guid _commentId;
    private string _outsiderId = null!;
    private string _memberId = null!;

    public async Task InitializeAsync()
    {
        _owner = await _factory.CreateReadyClientAsync();
        _anonymous = NewClient();
        (_member, _memberId) = await RegisterAsync("member@example.test");
        (_outsider, _outsiderId) = await RegisterAsync("outsider@example.test");
        _projectId = await CreateAsync(_owner, "/api/projects", new { name = "Private" });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ProjectMembers.Add(new ProjectMember(_projectId, _memberId, ProjectRole.Member));
        await db.SaveChangesAsync();
        _itemId = await CreateAsync(_owner, $"/api/projects/{_projectId}/work-items", new { title = "Original" });
        _commentId = await CreateAsync(_owner, $"/api/work-items/{_itemId}/comments", new { body = "Owner comment" });
    }

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private async Task<(HttpClient, string)> RegisterAsync(string email)
    {
        var client = NewClient();
        var credentials = new { email, password = "TestOnly!482Aa" };
        using var registration = await client.PostAsJsonAsync("/auth/register", credentials);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var login = await client.PostAsJsonAsync("/auth/login?useCookies=false", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", body.GetProperty("accessToken").GetString());
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (client, (await users.FindByEmailAsync(email))!.Id);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string route, object body)
    {
        using var response = await client.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static IEnumerable<object[]> ProtectedRoutes()
    {
        yield return new object[] { "GET", "/api/projects/{p}", "{}", 200 };
        yield return new object[] { "PUT", "/api/projects/{p}", "{\"name\":\"Changed\"}", 403 };
        yield return new object[] { "DELETE", "/api/projects/{p}", "{}", 403 };
        yield return new object[] { "GET", "/api/projects/{p}/work-items", "{}", 200 };
        yield return new object[] { "POST", "/api/projects/{p}/work-items", "{\"title\":\"New\"}", 201 };
        yield return new object[] { "GET", "/api/work-items/{w}", "{}", 200 };
        yield return new object[] { "PUT", "/api/work-items/{w}", "{\"title\":\"Changed\"}", 204 };
        yield return new object[] { "PATCH", "/api/work-items/{w}/status", "{\"status\":\"InProgress\"}", 204 };
        yield return new object[] { "DELETE", "/api/work-items/{w}", "{}", 403 };
        yield return new object[] { "GET", "/api/work-items/{w}/comments", "{}", 200 };
        yield return new object[] { "POST", "/api/work-items/{w}/comments", "{\"body\":\"New\"}", 201 };
        yield return new object[] { "GET", "/api/comments/{c}", "{}", 200 };
        yield return new object[] { "DELETE", "/api/comments/{c}", "{}", 403 };
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task Routes_EnforceAuthenticationMembershipAndRole(
        string method, string template, string json, int memberStatus)
    {
        var route = template.Replace("{p}", _projectId.ToString())
            .Replace("{w}", _itemId.ToString()).Replace("{c}", _commentId.ToString());
        async Task<HttpResponseMessage> Send(HttpClient client)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), route);
            if (method is "POST" or "PUT" or "PATCH")
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            return await client.SendAsync(request);
        }

        using var anonymous = await Send(_anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var outsider = await Send(_outsider);
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Private", (await db.Projects.SingleAsync()).Name);
            Assert.Equal("Original", (await db.WorkItems.SingleAsync()).Title);
            Assert.Equal(WorkItemStatus.Todo, (await db.WorkItems.SingleAsync()).Status);
            Assert.Equal("Owner comment", (await db.Comments.SingleAsync()).Body);
        }
        using var member = await Send(_member);
        Assert.Equal((HttpStatusCode)memberStatus, member.StatusCode);
        using var owner = await Send(_owner);
        Assert.True(owner.IsSuccessStatusCode, $"Owner {method} {route}: {owner.StatusCode}");
    }

    [Fact]
    public async Task Lists_OnlyIncludeMembershipProjects_AndCreationRecordsOwner()
    {
        foreach (var route in new[] { "/api/projects", "/api/work-items" })
        {
            using var anonymous = await _anonymous.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            var outsiders = await _outsider.GetFromJsonAsync<JsonElement>(route);
            Assert.Empty(outsiders.EnumerateArray());
            var members = await _member.GetFromJsonAsync<JsonElement>(route);
            Assert.Single(members.EnumerateArray());
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ProjectRole.Owner, (await db.ProjectMembers.SingleAsync(m =>
            m.ProjectId == _projectId && m.UserId == _factory.TestUserId)).Role);
    }

    [Fact]
    public async Task CommentAuthor_ComesFromAccount_AndMemberCanDeleteOwnComment()
    {
        var id = await CreateAsync(_member, $"/api/work-items/{_itemId}/comments",
            new { body = "My comment", authorId = _factory.TestUserId });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(_memberId, (await db.Comments.SingleAsync(c => c.Id == id)).AuthorId);
        }
        using var deleted = await _member.DeleteAsync($"/api/comments/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task RemovedMembership_ImmediatelyRemovesAccessWithSameToken()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ProjectMembers.Remove(await db.ProjectMembers.SingleAsync(m => m.UserId == _memberId));
        await db.SaveChangesAsync();
        using var response = await _member.GetAsync($"/api/work-items/{_itemId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async Task DisposeAsync()
    {
        _owner?.Dispose(); _member?.Dispose(); _outsider?.Dispose(); _anonymous?.Dispose();
        await _factory.DisposeAsync();
    }
}
