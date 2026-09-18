using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace IssueTracker.IntegrationTests;

// xUnit constructs a new instance (and database) for every fact/theory case.
public sealed class ProjectApiTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(),
        $"issuetracker-api-tests-{Guid.NewGuid():N}");
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;
    private string DatabasePath => Path.Combine(_directory, "test.db");
    private const string Projects = "/api/projects";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _factory = new ApiFactory(DatabasePath);
        _client = await _factory.CreateReadyClientAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        // This directory is created exclusively by this test; never the app database.
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    // Test-owned response contract: independent of the application's DTO definitions.
    private sealed record ProjectBody(Guid Id, string Name, string? Description,
        DateTimeOffset CreatedAt);

    private async Task<ProjectBody> CreateAsync(string name = "Test project",
        string? description = "Original description")
    {
        using var response = await _client.PostAsJsonAsync(Projects, new { name, description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectBody>())!;
    }

    private async Task<ProjectBody> GetAsync(Guid id)
    {
        using var response = await _client.GetAsync($"{Projects}/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectBody>())!;
    }

    private async Task<List<ProjectBody>> ListAsync()
    {
        using var response = await _client.GetAsync(Projects);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ProjectBody>>())!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response,
        HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.False(body.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task Post_Returns201_CamelCaseContract_AndWorkingLocation()
    {
        var before = DateTimeOffset.UtcNow;
        using var response = await _client.PostAsJsonAsync(Projects,
            new { name = "  Learning C#  ", description = "Practice" });
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "createdAt", "description", "id", "name" },
            json.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        var body = json.Deserialize<ProjectBody>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("Learning C#", body.Name);
        Assert.Equal("Practice", body.Description);
        Assert.Equal(TimeSpan.Zero, body.CreatedAt.Offset);
        Assert.InRange(body.CreatedAt, before, after);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(_client.BaseAddress!, response.Headers.Location);
        Assert.Equal($"{Projects}/{body.Id}", location.AbsolutePath);
        using var fetched = await _client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body, await fetched.Content.ReadFromJsonAsync<ProjectBody>());
    }

    [Fact]
    public async Task GetAll_EmptyDatabase_ReturnsEmptyArray()
        => Assert.Empty(await ListAsync());

    [Fact]
    public async Task GetAll_ReturnsEveryProject_WithoutAssumingOrder()
    {
        var first = await CreateAsync("First");
        var second = await CreateAsync("Second", null);
        var third = await CreateAsync("Third");
        var result = await ListAsync();
        Assert.Equal(3, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
        Assert.Contains(third, result);
    }

    [Fact]
    public async Task Post_ServerGeneratesIdsAndTimestamps_EvenWhenSuppliedInBody()
    {
        var suppliedId = Guid.NewGuid();
        using var response = await _client.PostAsJsonAsync(Projects, new
        {
            name = "Server owned fields", id = suppliedId,
            createdAt = "2000-01-01T00:00:00Z"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ProjectBody>())!;
        Assert.NotEqual(suppliedId, body.Id);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.True(body.CreatedAt.Year > 2000);
        Assert.NotEqual(body.Id, (await CreateAsync()).Id);
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        var bodies = new[]
        {
            "{}", "{\"name\":null}", "{\"name\":\"\"}",
            JsonSerializer.Serialize(new { name = "   " }),
            JsonSerializer.Serialize(new { name = "\t\r\n" }),
            JsonSerializer.Serialize(new { name = new string('x', 101) }),
            JsonSerializer.Serialize(new { name = "Valid", description = new string('x', 2001) }),
            "{\"name\":123}", "{\"name\":\"Valid\",\"description\":123}",
            "{", "null", "[]", ""
        };
        foreach (var method in new[] { "POST", "PUT" })
            foreach (var body in bodies)
                yield return new object[] { method, body };
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidJsonOrFields_Return400ProblemDetails_AndPreserveData(
        string method, string json)
    {
        var original = await CreateAsync();
        var url = method == "POST" ? Projects : $"{Projects}/{original.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var response = await _client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(original, await GetAsync(original.Id));
        Assert.Equal(original, Assert.Single(await ListAsync()));
    }

    [Theory]
    [InlineData("POST", 1, 0)]
    [InlineData("POST", 100, 2000)]
    [InlineData("PUT", 1, 0)]
    [InlineData("PUT", 100, 2000)]
    public async Task BoundaryLengths_AreAccepted(string method, int nameLength, int descriptionLength)
    {
        var name = new string('n', nameLength);
        var description = new string('d', descriptionLength);
        var original = method == "PUT" ? await CreateAsync() : null;
        using var request = new HttpRequestMessage(new HttpMethod(method),
            original is null ? Projects : $"{Projects}/{original.Id}")
        {
            Content = JsonContent.Create(new { name, description })
        };
        using var response = await _client.SendAsync(request);
        Assert.Equal(method == "POST" ? HttpStatusCode.Created : HttpStatusCode.NoContent,
            response.StatusCode);
        var saved = Assert.Single(await ListAsync());
        Assert.Equal(name, saved.Name);
        Assert.Equal(description, saved.Description);
    }

    [Theory]
    [InlineData("{\"name\":\"Optional\"}")]
    [InlineData("{\"name\":\"Optional\",\"description\":null}")]
    public async Task Post_OptionalDescription_IsNull(string json)
    {
        using var response = await _client.PostAsync(Projects,
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(Assert.Single(await ListAsync()).Description);
    }

    [Fact]
    public async Task Put_UpdatesBothFields_PreservesIdentityAndOtherProjects_IsIdempotent()
    {
        var original = await CreateAsync();
        var other = await CreateAsync("Other");
        var body = new { name = "  Updated  ", description = "Changed",
            id = other.Id, createdAt = "2000-01-01T00:00:00Z" };

        // The URL chooses the project even if the body supplies another ID.
        for (var i = 0; i < 2; i++)
        {
            using var response = await _client.PutAsJsonAsync($"{Projects}/{original.Id}", body);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
        Assert.Equal(original with { Name = "Updated", Description = "Changed" },
            await GetAsync(original.Id));
        Assert.Equal(other, await GetAsync(other.Id));
        Assert.Equal(2, (await ListAsync()).Count);
    }

    [Theory]
    [InlineData("{\"name\":\"Updated\"}")]
    [InlineData("{\"name\":\"Updated\",\"description\":null}")]
    public async Task Put_OmittedOrNullDescription_ClearsExistingValue(string json)
    {
        var original = await CreateAsync();
        using var response = await _client.PutAsync($"{Projects}/{original.Id}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(original with { Name = "Updated", Description = null },
            await GetAsync(original.Id));
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("GET", true)]
    [InlineData("PUT", false)]
    [InlineData("PUT", true)]
    public async Task UnknownOrEmptyId_Returns404_WithoutCreatingProject(string method, bool emptyId)
    {
        var id = emptyId ? Guid.Empty : Guid.NewGuid();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{Projects}/{id}");
        if (method == "PUT") request.Content = JsonContent.Create(new { name = "Unknown" });
        using var response = await _client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Empty(await ListAsync());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task MalformedRouteId_DoesNotMatchGuidRoute(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), $"{Projects}/not-a-guid");
        if (method == "PUT") request.Content = JsonContent.Create(new { name = "Unknown" });
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await ListAsync());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task UnsupportedContentType_Returns415_AndPreservesData(string method)
    {
        var original = await CreateAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? Projects : $"{Projects}/{original.Id}")
        {
            Content = new StringContent("name=Updated", Encoding.UTF8, "text/plain")
        };
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(original, Assert.Single(await ListAsync()));
    }

    [Fact]
    public async Task CreatedAndUpdatedProject_SurvivesHostRestart_WithRealMigrations()
    {
        var original = await CreateAsync();
        using var response = await _client.PutAsJsonAsync($"{Projects}/{original.Id}",
            new { name = "Persisted update", description = "Stored in SQLite" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        _client.Dispose();
        await _factory.DisposeAsync();

        _factory = new ApiFactory(DatabasePath);
        _client = await _factory.CreateReadyClientAsync();
        var expected = original with { Name = "Persisted update", Description = "Stored in SQLite" };
        Assert.Equal(expected, await GetAsync(original.Id));
        Assert.Equal(expected, Assert.Single(await ListAsync()));
    }
}
