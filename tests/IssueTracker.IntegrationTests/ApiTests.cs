using System.Net;
using System.Net.Http.Headers;
using IssueTracker.Api.Security;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using System.Text.Json;
using IssueTracker.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IssueTracker.IntegrationTests;

// Copy into the integration test project at milestone 6.
// At milestone 8, authenticate clients before running protected requests.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly bool _ownsDatabase;

    public ApiFactory(string? databasePath = null)
    {
        _ownsDatabase = databasePath is null;
        _databasePath = databasePath ?? Path.Combine(
            Path.GetTempPath(), $"issuetracker-test-{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:IssueTracker",
            $"Data Source={_databasePath};Pooling=False");
        builder.UseSetting("Database:ApplyMigrations", "false");
    }

    public string TestUserId { get; private set; } = string.Empty;

    public async Task AuthenticateAsync(HttpClient client)
    {
        const string email = "owner@example.test";
        const string password = "TestOnly!482Aa";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email };
            var result = await users.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        TestUserId = user.Id;
        using var login = await client.PostAsJsonAsync("/auth/login?useCookies=false", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
    }

    public async Task<HttpClient> CreateReadyClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await AuthenticateAsync(client);
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // Caller-provided databases may be reused across host restarts.
        if (!_ownsDatabase) return;

        // Only remove files belonging to this factory's unique test database.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

public class ApiTests
{
    [Fact]
    public async Task CreateProject_ReturnsCreatedAndRetrievableResource()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();

        using var created = await client.PostAsJsonAsync("/api/projects",
            new { name = "Learning C#", description = "Practice" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);

        using var fetched = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedBody = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, fetchedBody.GetProperty("id").GetGuid());
        Assert.Equal("Learning C#", fetchedBody.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateProject_BlankNameReturnsBadRequest(string name)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        using var response = await client.PostAsJsonAsync("/api/projects", new { name });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_UnknownIdReturnsNotFound()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        using var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_TodoToDoneReturnsConflictAndPreservesStatus()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        var projectId = await CreateAsync(client, "/api/projects", new { name = "Learning" });
        var itemId = await CreateAsync(client, $"/api/projects/{projectId}/work-items",
            new { title = "Build API" });

        using var response = await client.PatchAsJsonAsync(
            $"/api/work-items/{itemId}/status", new { status = "Done" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var fetched = await client.GetAsync($"/api/work-items/{itemId}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Todo", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DeleteProject_RemovesItsWorkItemsAndComments()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        var projectId = await CreateAsync(client, "/api/projects", new { name = "Learning" });
        var itemId = await CreateAsync(client, $"/api/projects/{projectId}/work-items",
            new { title = "Build API" });
        var commentId = await CreateAsync(client, $"/api/work-items/{itemId}/comments",
            new { body = "First comment" });

        using var deleted = await client.DeleteAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var project = await client.GetAsync($"/api/projects/{projectId}");
        using var item = await client.GetAsync($"/api/work-items/{itemId}");
        using var comment = await client.GetAsync($"/api/comments/{commentId}");
        Assert.Equal(HttpStatusCode.NotFound, project.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, item.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, comment.StatusCode);

        // Verify deletion in storage as well as the HTTP surface.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<IssueTracker.Core.Entities.WorkItem>()
            .AnyAsync(x => x.Id == itemId));
        Assert.False(await db.Set<IssueTracker.Core.Entities.Comment>()
            .AnyAsync(x => x.Id == commentId));
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string route, object payload)
    {
        using var response = await client.PostAsJsonAsync(route, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
    [Fact]
    public async Task Migrations_CreateDatabase_AndProjectCanBeSavedAndRetrieved()
    {
        // The factory generates a unique temporary database path.
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        // Create the database using actual migration files.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.MigrateAsync();

            var applied = await db.Database.GetAppliedMigrationsAsync();
            Assert.NotEmpty(applied);

            var pending = await db.Database.GetPendingMigrationsAsync();
            Assert.Empty(pending);
        }

        await factory.AuthenticateAsync(client);

        // Verify the migrated schema works through the API.
        using var created = await client.PostAsJsonAsync(
            "/api/projects",
            new { name = "Migration test", description = "Created after migration" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);

        using var fetched = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var body = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Migration test", body.GetProperty("name").GetString());
    }
}
