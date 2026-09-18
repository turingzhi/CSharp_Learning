# Build IssueTracker from scratch: C#, .NET, and ASP.NET Core

This is a practical course for someone who knows another programming language and some basic C#. You will write the application. This guide supplies the design, setup commands, implementation order, starter tests, and completion criteria.

Work one milestone at a time. Do not paste every future code block into the project on day one. Expect roughly 40–70 hours, depending on how much experimentation you do. Progress matters more than a deadline.

## 1. What you are building

IssueTracker is a backend for managing projects and their work items. Its first version runs locally and supports projects, issues, status changes, comments, filtering, and persistent storage. Later you add accounts, project membership, authorization, and deployment.

- **C#**: the language—types, classes, interfaces, generics, LINQ, exceptions, and asynchronous methods.
- **.NET**: the runtime, standard libraries, SDK, CLI, build system, configuration, logging, and testing ecosystem.
- **ASP.NET Core**: the web framework—controllers, routing, middleware, dependency injection, authentication, and HTTP responses.
- **Entity Framework Core (EF Core)**: the library that maps your C# objects to database tables.

We use .NET 10, controllers, SQLite, and xUnit. .NET 10 is an LTS release; install its latest servicing SDK when setting up. [Official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy).

This project teaches the core backend stack, not every C# feature or every .NET application type. A browser interface is an optional extension after the API works.

## 2. How to learn with this guide

For each milestone:

1. Read its concepts and requirements.
2. Explain the intended behavior in your own words.
3. Implement the smallest working part.
4. Run a test or send an HTTP request.
5. Read errors carefully; use breakpoints to inspect values.
6. Refactor once the behavior works.
7. Record what you learned and commit the milestone.

Use the provided tests as behavioral specifications. A missing class causes a compilation error; after you add the public interface, an unimplemented method can fail a test. Both are normal stages of this exercise. Do not weaken an assertion just to get a green test.

If stuck, ask for a hint, then an explanation, then a small example. Ask for a complete solution only after attempting the task.

## 3. Prerequisites and first setup

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), Git, and a C# editor. The SDK includes tools needed to build applications; installing only a runtime is insufficient. Your current machine already has SDK 10.0.103.

The commands below use macOS/Linux shell syntax and run from the solution root unless a step says otherwise. Package restore needs internet access.

Check the tools:

```bash
dotnet --info
dotnet --list-sdks
git --version
```

From the folder where you want to keep the project, create a new directory:

```bash
mkdir IssueTracker
cd IssueTracker
dotnet new sln -n IssueTracker --format sln
dotnet new classlib -n IssueTracker.Core -o src/IssueTracker.Core -f net10.0
dotnet new console -n IssueTracker.Playground -o src/IssueTracker.Playground -f net10.0
dotnet new xunit -n IssueTracker.UnitTests -o tests/IssueTracker.UnitTests -f net10.0

dotnet sln IssueTracker.sln add src/IssueTracker.Core/IssueTracker.Core.csproj
dotnet sln IssueTracker.sln add src/IssueTracker.Playground/IssueTracker.Playground.csproj
dotnet sln IssueTracker.sln add tests/IssueTracker.UnitTests/IssueTracker.UnitTests.csproj

dotnet add src/IssueTracker.Playground reference src/IssueTracker.Core
dotnet add tests/IssueTracker.UnitTests reference src/IssueTracker.Core

dotnet new gitignore
git init
dotnet build
dotnet test
```

The generated placeholder test does not verify your application yet. Delete `Class1.cs` and `UnitTest1.cs` when you replace them with real files.

We explicitly select `.sln` so commands use a consistent filename across SDK versions. Template options can be inspected with `dotnet new <template> --help`. [CLI documentation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new).

Add these lines to `.gitignore`:

```gitignore
*.db
*.db-shm
*.db-wal
TestResults/
publish/
```

Commit source files and database migrations, but not local databases, build output, passwords, or tokens.

## 4. Structure you will grow into

Create folders only when you need them. Start with the three projects above; add the API and integration tests later.

```text
IssueTracker/
├── IssueTracker.sln
├── README.md
├── LEARNING_LOG.md
├── .gitignore
├── .config/dotnet-tools.json          # added with EF tools
├── src/
│   ├── IssueTracker.Core/
│   │   ├── Entities/
│   │   │   ├── Project.cs
│   │   │   ├── WorkItem.cs
│   │   │   └── Comment.cs
│   │   └── Enums/WorkItemStatus.cs
│   ├── IssueTracker.Playground/
│   │   └── Program.cs
│   └── IssueTracker.Api/
│       ├── Controllers/
│       ├── Contracts/                 # request and response DTOs
│       ├── Services/                  # workflows and business orchestration
│       ├── Data/AppDbContext.cs
│       ├── Migrations/
│       ├── Security/                  # added with accounts
│       ├── Program.cs
│       └── appsettings.json
└── tests/
    ├── IssueTracker.UnitTests/
    └── IssueTracker.IntegrationTests/
```

References: Playground → Core; API → Core; UnitTests → Core; IntegrationTests → API. Core must not reference API or Entity Framework.

The request flow is:

```text
HTTP request → controller → service → entity rules + DbContext → SQLite
HTTP response ← response DTO ← service result
```

Controllers deal with HTTP. Services coordinate operations. Entities protect their own rules. `AppDbContext` reads and writes data. Request DTOs define what a caller may send; response DTOs define what the API reveals.

Use the name `WorkItem` instead of `Task` to avoid confusion with `System.Threading.Tasks.Task`.

## 5. Data model and fixed rules

Use these rules consistently; the starter tests depend on them.

| Entity | Properties |
| --- | --- |
| Project | `Guid Id`, `string Name`, `string? Description`, `DateTimeOffset CreatedAt` |
| WorkItem | `Guid Id`, `Guid ProjectId`, `string Title`, `string? Description`, `WorkItemStatus Status`, `DateTimeOffset CreatedAt` |
| Comment | `Guid Id`, `Guid WorkItemId`, `string Body`, `DateTimeOffset CreatedAt` |

Later add `ProjectMember(ProjectId, UserId, Role)`, `WorkItem.AssigneeId`, and `Comment.AuthorId`. Identity user IDs are strings; do not confuse them with project GUIDs.

Validation:

- Project names: trim whitespace; then require 1–100 characters.
- Work item titles: trim whitespace; then require 1–200 characters.
- Optional descriptions: maximum 2,000 characters.
- Comment bodies: trim whitespace; then require 1–2,000 characters.
- IDs are generated by the application. Reject an empty project ID when constructing a work item.
- Use UTC timestamps. Do not accept creation times or IDs from creation requests.
- A work item starts in `Todo`.
- Allowed changes: `Todo → InProgress`, `InProgress → Done`, `Done → InProgress`.
- Setting the current status again is a successful no-op.
- Other transitions fail; an undefined enum value fails validation.
- Deleting a project deletes its work items and their comments.
- Deleting a work item deletes its comments.

This is our chosen workflow, not a universal rule for issue trackers.

## 6. Milestone 1 — C# entities and meaningful unit tests

**Learn:** namespaces, constructors, properties, access modifiers, enums, nullable references, exceptions, and xUnit.

Create `Enums/WorkItemStatus.cs` in Core:

```csharp
namespace IssueTracker.Core.Enums;

public enum WorkItemStatus
{
    Todo,
    InProgress,
    Done
}
```

Implement `Entities/WorkItem.cs` in namespace `IssueTracker.Core.Entities`. Its public interface must include:

```csharp
public WorkItem(Guid projectId, string title)
public Guid Id { get; private set; }
public Guid ProjectId { get; private set; }
public string Title { get; private set; }
public WorkItemStatus Status { get; private set; }
public DateTimeOffset CreatedAt { get; private set; }
public void Rename(string title)
public void ChangeStatus(WorkItemStatus nextStatus)
```

These lines describe members of the class, not a complete file. Supply the constructor and method bodies yourself. Initialize non-nullable properties properly; do not disable nullable warnings to hide mistakes.

Use `ArgumentException` for invalid titles and empty project IDs. Use `ArgumentOutOfRangeException` for undefined status values. Use `InvalidOperationException` for a defined but disallowed status transition. Validate before changing state.

Implement `Project` and `Comment` with the same approach. Reuse title validation between the constructor and `Rename` without introducing an inheritance hierarchy.

Copy the supplied `starter-tests/WorkItemTests.cs` into `tests/IssueTracker.UnitTests/`. If you created `IssueTracker` directly under the guide folder:

```bash
cp ../starter-tests/WorkItemTests.cs tests/IssueTracker.UnitTests/
dotnet test tests/IssueTracker.UnitTests
```

Write additional tests for project names, comment bodies, a successful rename, and rejected operations leaving state unchanged. Use `[Theory]` for multiple inputs with the same expected behavior.

**Done when:** the real tests pass and you can explain why callers cannot set `Status` directly.

## 7. Milestone 2 — Collections, LINQ, and debugging

**Learn:** `List<T>`, `IEnumerable<T>`, lambdas, `Where`, `Select`, `GroupBy`, ordering, and debugger stepping.

In Playground:

1. Create one project and five work items in a list.
2. Print each title and status using string interpolation.
3. Move two items to `InProgress` and one of those to `Done`.
4. Filter the unfinished items with LINQ.
5. Group by status and print counts.
6. Find an item by ID and rename it; handle an unknown ID without crashing.
7. Put a breakpoint inside `ChangeStatus`, attempt an invalid transition, and inspect state.

```bash
dotnet run --project src/IssueTracker.Playground
```

Optional exercise: add a simple menu using `Console.ReadLine`, a loop, and `Guid.TryParse`. Keep console input out of Core.

**Done when:** the output matches your expected counts, and you can explain the difference between a query and materializing it with `ToList()`.

## 8. Milestone 3 — Your first ASP.NET Core controller

**Learn:** HTTP methods, routing, controllers, JSON, dependency injection, DTOs, and status codes.

Create and reference the API:

```bash
dotnet new webapi -n IssueTracker.Api -o src/IssueTracker.Api -f net10.0 --use-controllers
dotnet sln IssueTracker.sln add src/IssueTracker.Api/IssueTracker.Api.csproj
dotnet add src/IssueTracker.Api reference src/IssueTracker.Core
dotnet dev-certs https --trust
dotnet run --project src/IssueTracker.Api --launch-profile https
```

Use the HTTPS URL printed by the application. `/` may return 404 because you have not mapped that route. Test the generated `/weatherforecast` endpoint first. Then remove the sample controller and model.

Keep the template's `AddControllers`, `MapControllers`, and HTTPS setup. Retain `AddOpenApi` and the development-only `MapOpenApi`. OpenAPI JSON is available at `/openapi/v1.json`; a visual Swagger UI is a separate addition. [Controller tutorial](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0).

Implement only `POST /api/projects`, `GET /api/projects`, and `GET /api/projects/{id}` initially:

1. Create `CreateProjectRequest` and `ProjectResponse` DTOs.
2. Add `[ApiController]`, `[Route("api/projects")]`, and derive the controller from `ControllerBase`.
3. Temporarily keep projects in a singleton `ProjectService` backed by `ConcurrentDictionary<Guid, Project>`. This first service only creates and reads projects.
4. Register the service using `AddSingleton<ProjectService>()`; inject it into the controller constructor.
5. Validate the request name using required/length annotations, and enforce the trimmed-name rule in Core.
6. Return `201 Created` with `CreatedAtAction` for creation, including a `Location` pointing to the get-by-ID route.
7. Return `200 OK` with a response DTO when found and `404 Not Found` when missing.

Temporary storage disappears on restart. Replace it in the next milestone before adding more mutation operations.

Example creation request and response:

```json
{ "name": "Learning C#", "description": "My first backend" }
```

```json
{ "id": "58c1c1bb-9c66-4a45-a84a-50265f3dd4f8", "name": "Learning C#", "description": "My first backend" }
```

The ID above is an example, not a value to hardcode. Use camelCase JSON and named enum strings for later work item DTOs. Configure `JsonStringEnumConverter` with integer values disabled in controller JSON options.

**Done when:** valid creation returns 201 and a working Location, blank names return 400, and missing IDs return 404. Explain why a singleton collection survives requests but not process restarts.

## 9. Milestone 4 — SQLite and EF Core

**Learn:** relational keys, `DbContext`, entity mapping, migrations, scoped services, `async`/`await`, and cancellation.

Add compatible packages and a local EF tool. These commands select the latest available stable 10.0 patch; project files and the tool manifest record the resolved versions.

```bash
dotnet add src/IssueTracker.Api package Microsoft.EntityFrameworkCore.Sqlite --version '10.0.*'
dotnet add src/IssueTracker.Api package Microsoft.EntityFrameworkCore.Design --version '10.0.*'
dotnet new tool-manifest
dotnet tool install dotnet-ef --version '10.0.*'
```

Implement these steps in order:

1. Create `Data/AppDbContext.cs`, accepting `DbContextOptions<AppDbContext>` in its constructor.
2. Expose `DbSet<Project>`, `DbSet<WorkItem>`, and `DbSet<Comment>`.
3. Configure keys, required fields, maximum lengths, and foreign keys in `OnModelCreating`.
4. Explicitly configure cascade deletion for project → work items → comments.
5. Ensure EF can construct your entities. A private parameterless constructor is one option; keep the validated public constructors for application code.
6. Add `"ConnectionStrings": { "IssueTracker": "Data Source=issuetracker.db" }` to `appsettings.json`, preserving its existing JSON entries.
7. Register `AppDbContext` with `AddDbContext` and `UseSqlite(builder.Configuration.GetConnectionString("IssueTracker"))`.
8. Replace the dictionary service with database queries. Register that service as scoped with `AddScoped`, replacing the singleton registration.
9. Await EF operations such as `ToListAsync`, `SingleOrDefaultAsync`, and `SaveChangesAsync`. Pass the action's cancellation token through the service into EF.
10. Use `AsNoTracking` for read-only queries and map entities into response DTOs. Avoid returning database entities directly.

Do not register `DbContext` as a singleton or use one context concurrently. Do not use `.Result`, `.Wait()`, or `Task.Run` to wrap database calls.

Create the schema:

```bash
dotnet ef migrations add InitialCreate --project src/IssueTracker.Api --startup-project src/IssueTracker.Api
dotnet ef database update --project src/IssueTracker.Api --startup-project src/IssueTracker.Api
```

Use migrations for your development database. Do not mix `EnsureCreated` and migrations on the same database. Relative SQLite paths can depend on the process working directory; if the schema seems missing, inspect the resolved database path first. [EF Core setup documentation](https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=netcore-cli).

**Done when:** create a project, stop the API, restart it, and retrieve that same ID. Inspect the migration and explain its tables and foreign keys.

## 10. Milestone 5 — Complete the first API

Implement one resource at a time: projects, then work items, then comments. Keep controller methods short. Services check whether referenced resources exist; entity methods enforce entity rules.

Before accounts are added, implement this contract:

| Method and route | Successful response | Important failure |
| --- | --- | --- |
| `POST /api/projects` | 201 + project + Location | invalid input: 400 |
| `GET /api/projects` | 200 + project array | — |
| `GET /api/projects/{id}` | 200 + project | missing: 404 |
| `PUT /api/projects/{id}` | 204 | missing: 404; invalid: 400 |
| `DELETE /api/projects/{id}` | 204 | missing: 404 |
| `POST /api/projects/{projectId}/work-items` | 201 + work item + Location | project missing: 404 |
| `GET /api/projects/{projectId}/work-items` | 200 + work item array | project missing: 404 |
| `GET /api/work-items/{id}` | 200 + work item | missing: 404 |
| `PUT /api/work-items/{id}` | 204 | missing: 404; invalid: 400 |
| `PATCH /api/work-items/{id}/status` | 204 | invalid transition: 409 |
| `DELETE /api/work-items/{id}` | 204 | missing: 404 |
| `POST /api/work-items/{id}/comments` | 201 + comment + Location | item missing: 404 |
| `GET /api/work-items/{id}/comments` | 200 + comment array | item missing: 404 |
| `GET /api/comments/{id}` | 200 + comment | missing: 404 |
| `DELETE /api/comments/{id}` | 204 | missing: 404 |

Request fields:

- Project POST/PUT: `name`, optional `description`.
- Work item POST/PUT: `title`, optional `description`; PUT does not change project or status.
- Status PATCH: `status`, for example `{"status":"InProgress"}`.
- Comment POST: `body`.

Responses include the corresponding entity fields from the data model. Status is a JSON string. Server-managed fields must not be writable through these request contracts. An absent optional description in PUT clears it.

Apply 400 for malformed input and undefined statuses, 404 for absent resources, and 409 for disallowed transitions. Repeated setting of the same status returns 204. Use Problem Details for errors. Register `AddProblemDetails()` and `UseExceptionHandler()` for unexpected failures; explicitly map known failures instead of converting every exception into 400. Do not return exception stacks to callers.

Manually exercise the API in a second terminal. Substitute the actual HTTPS port:

```bash
API_URL=https://localhost:7001
curl -i "$API_URL/api/projects" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Learning C#","description":"Practice project"}'
```

Copy the returned ID:

```bash
PROJECT_ID=replace-with-returned-id
curl -i "$API_URL/api/projects/$PROJECT_ID"
curl -i "$API_URL/api/projects/$PROJECT_ID/work-items" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Build my first controller"}'
```

**Done when:** all listed routes meet the contract, invalid transitions preserve the previous status, and cascade deletion removes dependent records.

## 11. Milestone 6 — Integration tests

**Learn:** real HTTP pipeline tests, test hosts, database isolation, and arranging test data.

```bash
dotnet new xunit -n IssueTracker.IntegrationTests -o tests/IssueTracker.IntegrationTests -f net10.0
dotnet sln IssueTracker.sln add tests/IssueTracker.IntegrationTests/IssueTracker.IntegrationTests.csproj
dotnet add tests/IssueTracker.IntegrationTests reference src/IssueTracker.Api
dotnet add tests/IssueTracker.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version '10.0.*'
```

At the very end of the API's `Program.cs`, after all top-level statements, add:

```csharp
public partial class Program { }
```

Copy `starter-tests/ApiTests.cs` into the integration test project. It expects `IssueTracker.Api.Data.AppDbContext`, the connection-string name above, and the API contract from milestone 5. The factory uses a unique temporary SQLite file per test. It initializes that disposable database with `EnsureCreated`; your development database still uses migrations. It does not start a server on a network port. [ASP.NET Core integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).

```bash
cp ../starter-tests/ApiTests.cs tests/IssueTracker.IntegrationTests/
dotnet test tests/IssueTracker.IntegrationTests
dotnet test
```

The supplied integration tests cover project creation/readback, rejected names, missing resources, a rejected status transition, and cascade deletion. Add these yourself:

- Work item creation under a missing project returns 404.
- Full valid status sequence succeeds; repeating the current status succeeds.
- A malformed or unknown status returns 400.
- Updating a work item preserves its project and creation time.
- Adding an empty comment returns 400.
- Deleting a work item also deletes its comments.
- A separate test applies `Database.Migrate()` to a new temporary database, then creates and retrieves a project. This checks migration correctness, which `EnsureCreated()` cannot establish.

Each test must create its own data. It must pass alone, in a suite, and regardless of execution order. Do not assert random GUID values or exact wall-clock timestamps.

**Done when:** the tests pass twice with no dependence on your development database. The starter tests are a foundation, not complete coverage of every rule.

## 12. Milestone 7 — Filtering, pagination, and logging

**Learn:** composing `IQueryable`, deferred execution, query validation, structured logs, and service lifetimes.

Extend the work item list route:

```text
GET /api/projects/{projectId}/work-items?status=Todo&search=controller&page=1&pageSize=20
```

Rules:

- `page` starts at 1; `pageSize` defaults to 20 and must be 1–100.
- Invalid page values and unknown statuses return 400.
- Search matches a substring of the title; define the first version as case-sensitive and test that contract.
- Apply project, status, and search filters before counting and pagination.
- Order by `CreatedAt`, then `Id`, so results have a deterministic order.
- Return `{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0 }`.
- Existing project with no matches returns an empty page; missing project returns 404.

Construct the database query before executing it. Do not load the whole table and paginate in memory. Keep sort operations compatible with SQLite: for example, map UTC `DateTimeOffset` values to UTC ticks with an EF value converter before ordering on `CreatedAt`, then create and apply a migration. Alternatively, switch persisted timestamps consistently to UTC `DateTime` and update the contracts/tests.

Inject `ILogger<YourService>` and log key events with message templates, such as `"Created work item {WorkItemId}"`. Keep passwords and tokens out of logs. Explain transient, scoped, and singleton lifetimes using examples from your application.

Tests: seed 25 matching items, check pages of 20 and 5, verify total count, verify filters do not include another project's items, and reject pageSize 101.

**Done when:** pagination happens in the database and filtered counts remain correct. Update tests that assumed the old array response.

## 13. Milestone 8 — Accounts and project permissions

**Learn:** authentication versus authorization, claims, Identity, password hashing, ownership, and resource-level permission checks.

Use ASP.NET Core Identity rather than implementing password storage or token generation yourself. Follow the [official Identity API guide](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) as the implementation reference for this milestone.

```bash
dotnet add src/IssueTracker.Api package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version '10.0.*'
```

Implementation order:

1. Create `ApplicationUser : IdentityUser` in the API project.
2. Change `AppDbContext` to inherit from `IdentityDbContext<ApplicationUser>`. Call the base implementation of `OnModelCreating` before your mappings.
3. Register `AddIdentityApiEndpoints<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>()`, plus authorization.
4. Add authentication middleware before authorization middleware; map Identity API endpoints under `/auth` with `MapGroup("/auth").MapIdentityApi<ApplicationUser>()`.
5. Create/apply a migration for Identity tables and the new membership/author/assignee fields. For this practice app, delete pre-auth sample projects through your API before making ownership required, or write a deliberate data backfill migration.
6. Register two test users and use `/auth/login?useCookies=false` for the learning API. Send the returned access token as `Authorization: Bearer <token>`. Identity's built-in bearer tokens are not JWTs.
7. Protect project, work item, and comment controllers with `[Authorize]`.
8. Creating a project also creates an Owner membership for the current user, atomically.
9. Restrict project listing to projects the current user belongs to.
10. Check membership whenever accessing a project, work item, or comment, including by direct ID. Derive a child's project on the server.

Permission contract:

| Action | Member | Owner |
| --- | --- | --- |
| Read project, work items, comments | Yes | Yes |
| Create/update work items and statuses | Yes | Yes |
| Assign a work item to an existing project member | Yes | Yes |
| Add comments | Yes | Yes |
| Delete a comment | Own comments only | Any comment |
| Delete work items | No | Yes |
| Rename/delete project and manage membership | No | Yes |

Add `POST /api/projects/{id}/members` with `userId`; it adds an existing user as a Member. Add `DELETE /api/projects/{id}/members/{userId}`; the Owner cannot be removed in this version. Removing a Member clears their assignments in that project. Add `PUT /api/work-items/{id}/assignee` with nullable `userId`; null unassigns, and a nonmember is rejected with 400. Prevent duplicate memberships using a composite database key.

Use 401 for unauthenticated requests, 403 for a project member lacking an action's permission, and 404 for resources outside the user's projects. Derive `AuthorId` from the authenticated user, never from the request body.

Update the integration test helper: register a unique test account and log in for each client before the existing CRUD tests. Read the `accessToken` from the response and set the authorization header. Add two-user tests for every permission boundary; do not globally bypass authorization to make the old tests pass.

**Done when:** unauthenticated calls fail, users cannot access another project's data by guessing IDs, nonmembers cannot be assigned, and the permission matrix has automated tests.

## 14. Milestone 9 — Configuration, publishing, and Docker

**Learn:** environments, configuration precedence, publishing, containers, and durable storage.

1. Register health checks and map `/health`. Keep the response minimal; this first check reports application liveness, not database readiness.
2. Keep OpenAPI and detailed error information development-only.
3. Set production configuration through environment variables. For example, `ConnectionStrings__IssueTracker` maps to the nested connection-string setting.
4. Use user secrets for local sensitive settings when needed: `dotnet user-secrets init --project src/IssueTracker.Api`. Never commit real credentials.
5. Build, test, and publish:

```bash
dotnet build -c Release
dotnet test -c Release
dotnet publish src/IssueTracker.Api -c Release -o publish
```

6. Create `Dockerfile` in the solution root:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/IssueTracker.Core/IssueTracker.Core.csproj src/IssueTracker.Core/
COPY src/IssueTracker.Api/IssueTracker.Api.csproj src/IssueTracker.Api/
RUN dotnet restore src/IssueTracker.Api/IssueTracker.Api.csproj
COPY src/ src/
RUN dotnet publish src/IssueTracker.Api/IssueTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "IssueTracker.Api.dll"]
```

7. Create `.dockerignore` containing `**/bin`, `**/obj`, `.git`, `**/*.db`, `**/*.db-shm`, `**/*.db-wal`, and `publish`, each on its own line.
8. Install/start Docker if needed, build the image, and inspect startup logs:

```bash
docker build -t issuetracker .
docker volume create issuetracker-data
```

The container needs an initialized database in its persistent volume. For a single-instance local learning deployment, add an explicit startup migration option to `Program.cs`, before `app.Run()`:

```csharp
if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

Add the `Microsoft.EntityFrameworkCore` and your Data namespace imports. Leave the option false by default and in integration tests. For a multi-instance deployment, apply migrations as a separate controlled deployment step instead.

Run the local container:

```bash
docker run --rm --name issuetracker -p 127.0.0.1:8080:8080 \
  -e 'ConnectionStrings__IssueTracker=Data Source=/data/issuetracker.db' \
  -e 'Database__ApplyMigrations=true' \
  -v issuetracker-data:/data \
  issuetracker
```

Use `http://localhost:8080/health` for this local container smoke check. The image listens on HTTP; HTTPS redirection alone does not create a certificate or HTTPS listener. Before public hosting, configure HTTPS termination and trusted forwarded headers for your host, run with appropriate file permissions/non-root identity, and persist Data Protection keys so authentication survives restarts. SQLite suits this single-instance exercise; a multi-instance system needs a separate database plan.

**Done when:** the image starts, health returns 200, data survives container replacement using the same volume, and a newly authenticated user can access their saved project after restart.

## 15. Final acceptance checklist

- [ ] A fresh clone builds using documented prerequisites.
- [ ] A fresh database can be created from migrations.
- [ ] Project, work item, and comment operations behave as documented.
- [ ] Validation and status rules are tested.
- [ ] Filtering and pagination execute against the database.
- [ ] Data survives application restarts.
- [ ] Accounts and project permissions are enforced on every protected route.
- [ ] Tests use isolated databases and pass in any order.
- [ ] Errors are consistent and logs help diagnose failures.
- [ ] Release build, tests, and container smoke checks pass.
- [ ] README explains setup, configuration, migration, tests, and example requests.
- [ ] You can trace one request from HTTP through service, entity, database, and back.

Optional extensions after completion: a Blazor UI, PostgreSQL migration, optimistic concurrency, notification background services, rate limiting, CI, and external-service calls through `IHttpClientFactory`. Pick one at a time.

## 16. Troubleshooting

| Symptom | Check |
| --- | --- |
| `dotnet` not found | Install the SDK, then reopen the terminal. |
| Target framework unsupported | Check `dotnet --version` and any parent `global.json`. |
| Namespace/type not found | Check namespace, project references, accessibility, and spelling. |
| NuGet restore fails | Check internet/proxy access and the first restore error. |
| HTTPS certificate error | Run `dotnet dev-certs https --trust`; restart the client. |
| Port already in use | Stop the previous API process or choose another launch-settings port. |
| Root route returns 404 | Request an actual controller route. |
| SQLite reports no such table | Apply migrations and verify the database file path. |
| EF cannot instantiate entity | Review constructor binding and property mappings. |
| A test expects 201 but gets 401 | Authenticate the test client after milestone 8. |
| Tests interfere with each other | Use independent databases and seed data per test. |
| SQLite cannot translate ordering | Review the timestamp mapping from milestone 7. |

When requesting debugging help, share the smallest relevant code, the exact error, the request you sent, expected behavior, and the actual result. Redact credentials.

## 17. Reusable mentor prompt

Copy this into a future conversation along with this guide or your current code:

```text
I am learning C#, .NET 10, and ASP.NET Core by building IssueTracker.
I know another programming language and some basic C#.

Use controllers, EF Core with SQLite, xUnit, and the attached learning guide.
My current milestone is: [number and name].
Completed work: [summary].
Current issue or next goal: [description].

Teach me one small step at a time:
1. Explain the concept and why it matters here.
2. Tell me which file to create/change and the required behavior.
3. Give me a small implementation task with a completion check.
4. Let me write the application code before showing a full solution.
5. You may generate scaffolding and meaningful tests.
6. Review my code for correctness, readability, and idiomatic C#.
7. Help me understand errors rather than only patching them.
8. Avoid introducing frameworks or patterns without a concrete need.

Start by checking my current code and help me complete the next unfinished step.
```

## 18. Your first session

Do section 3, create the enum and `WorkItem` public interface from milestone 1, then copy and run the unit tests. Focus first on construction: valid title, generated ID, initial status, and rejected blank title. Implement status transitions after those pass. Stop there and review what you learned before moving on.

The supplied starter tests are exercise files, not a claim that an application has already been implemented or that the suite has passed.
