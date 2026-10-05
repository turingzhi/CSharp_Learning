# IssueTracker

A learning backend built with C#, .NET 10, ASP.NET Core controllers, EF Core, SQLite, and xUnit. It manages projects, work items, comments, accounts, and project membership.

## Documentation

- **This README:** run the existing application, use its API, and understand its current limitations.
- [Learning guide](LEARNING_GUIDE.md): rebuild the application milestone by milestone; early milestones intentionally describe simpler behavior.
- [Learning takeaways](TAKEAWAYS.md): explanations of C#, HTTP, EF Core, authentication, testing, and Docker.
- [Integration tests](tests/IssueTracker.IntegrationTests/README.md): coverage and test isolation.

## Run locally

Install the .NET 10 SDK. Run commands from this directory (`test/IssueTracker` in the parent repository). Package and tool restore require access to NuGet.

```bash
dotnet restore IssueTracker.sln
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet dev-certs https --trust
```

Use an explicit database path so the EF CLI and running API use the same file. Keep the environment variable set in the terminal used for the following commands:

```bash
export ConnectionStrings__IssueTracker="Data Source=$PWD/issuetracker.db"
dotnet ef database update --project src/IssueTracker.Api --startup-project src/IssueTracker.Api
dotnet run --project src/IssueTracker.Api --launch-profile https
```

The checked-in HTTPS profile uses `https://localhost:7050` and HTTP port `5028`. Check the startup output if you change the profile. The API does not provide a homepage or Swagger UI.

- `GET /health`: application liveness; does not check database readiness.
- `GET /openapi/v1.json`: OpenAPI document, available in Development only.
- `/auth`: ASP.NET Core Identity endpoints.
- `/api`: authenticated application endpoints.

## Try the API

In a second terminal, register a local practice account and log in:

```bash
API_URL=https://localhost:7050
curl -i "$API_URL/auth/register" \
  -H 'Content-Type: application/json' \
  -d '{"email":"learner@example.test","password":"Practice-Only42!"}'
curl -sS "$API_URL/auth/login?useCookies=false" \
  -H 'Content-Type: application/json' \
  -d '{"email":"learner@example.test","password":"Practice-Only42!"}'
```

Register once; use login for an existing account. Copy `accessToken` from the login response. These Identity bearer tokens are not JWTs.

```bash
TOKEN='paste-accessToken-here'
curl -i "$API_URL/api/projects" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Learning C#","description":"Practice project"}'
```

Copy the returned project's `id`, then create and list work items:

```bash
PROJECT_ID='paste-project-id-here'
curl -i "$API_URL/api/projects/$PROJECT_ID/work-items" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"title":"Build my first controller"}'
curl -sS "$API_URL/api/projects/$PROJECT_ID/work-items?status=Todo&page=1&pageSize=20" \
  -H "Authorization: Bearer $TOKEN"
```

The project work-item list returns `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": ... }`. It supports `status`, case-sensitive title substring `search`, `page` (minimum 1), and `pageSize` (1–100, default 20). Results are ordered by creation time, then ID. The project list and global work-item list return arrays limited to the caller's memberships.

## API and permissions

All routes below require authentication. Missing credentials return 401; resources outside the caller's projects return 404; a member attempting an owner-only action receives 403.

| Method | Route | Access / successful response |
| --- | --- | --- |
| POST | `/api/projects` | Signed-in user; 201, creator becomes Owner |
| GET | `/api/projects` | Signed-in user; 200, own memberships only |
| GET | `/api/projects/{id}` | Member; 200 |
| PUT / DELETE | `/api/projects/{id}` | Owner; 204 |
| POST | `/api/projects/{projectId}/work-items` | Member; 201 |
| GET | `/api/projects/{projectId}/work-items` | Member; 200, paged |
| GET | `/api/work-items` | Signed-in user; 200, own memberships only |
| GET / PUT | `/api/work-items/{id}` | Member; 200 / 204 |
| DELETE | `/api/work-items/{id}` | Owner; 204 |
| PATCH | `/api/work-items/{id}/status` | Member; 204 |
| POST / GET | `/api/work-items/{id}/comments` | Member; 201 / 200 |
| GET | `/api/comments/{id}` | Member; 200 |
| DELETE | `/api/comments/{id}` | Comment author or Owner; 204 |
| POST | `/api/projects/{projectId}/members` | Owner; 201 |
| DELETE | `/api/projects/{projectId}/members/{userId}` | Owner; 204 |
| PUT | `/api/work-items/{id}/assignee` | Member; 204 |

Request bodies:

- Project POST/PUT: `name`, optional `description`.
- Work-item POST/PUT: `title`, optional `description`. PUT preserves project and status; omitted description clears it.
- Status PATCH: `{"status":"InProgress"}`. Allowed transitions are `Todo → InProgress`, `InProgress → Done`, and `Done → InProgress`. Repeating the current status is a successful no-op; other transitions return 409. Invalid/missing statuses return 400.
- Comment POST: `body`; the server derives the author from the account.
- Membership POST: `userId` of an existing Identity user. Duplicate membership returns 409; unknown user returns 400. There is no user-directory endpoint in this API.
- Assignee PUT: `userId` of a project member, or `null` to unassign. A nonmember returns 400.

The Owner cannot be removed (409). Removing a Member clears their assignments in that project. Deleting a project cascades to work items, comments, and memberships; deleting a work item cascades to comments.

## Structure

```text
src/IssueTracker.Core/         Entities, status and role enums, domain rules
src/IssueTracker.Playground/   Console experiments
src/IssueTracker.Api/          Controllers, contracts, services, EF data/migrations, security
tests/IssueTracker.UnitTests/  Entity tests
tests/IssueTracker.IntegrationTests/  HTTP, persistence, and permission tests
```

Requests pass through middleware and the project-access filter to controllers, services, domain rules, and `AppDbContext`. Core has no EF dependency. Services and DbContext use scoped lifetimes. Work-item timestamps are stored as UTC ticks for SQLite ordering.

## Tests and publishing

```bash
dotnet build IssueTracker.sln
dotnet test IssueTracker.sln
dotnet test tests/IssueTracker.IntegrationTests --filter 'FullyQualifiedName~MembershipTests'
dotnet publish src/IssueTracker.Api -c Release -o publish
```

Tests create disposable SQLite databases; they do not require a running API. See the [test guide](tests/IssueTracker.IntegrationTests/README.md) for the distinction between schema creation and migration coverage. A Release build configuration is separate from the Production runtime environment.

After changing entities or EF mappings, generate and review a migration before applying it:

```bash
dotnet ef migrations add DescribeYourChange --project src/IssueTracker.Api --startup-project src/IssueTracker.Api
dotnet ef database update --project src/IssueTracker.Api --startup-project src/IssueTracker.Api
```

## Docker and configuration

From this directory, with Docker running:

```bash
docker build -t issuetracker .
docker volume create issuetracker-data
docker run --rm --name issuetracker -p 127.0.0.1:8080:8080 \
  -e 'ConnectionStrings__IssueTracker=Data Source=/data/issuetracker.db' \
  -e 'Database__ApplyMigrations=true' \
  -v issuetracker-data:/data \
  issuetracker
```

Check `http://localhost:8080/health`. The named volume preserves the database when the container is replaced. The image listens on HTTP; HTTPS redirection does not configure a certificate or HTTPS listener.

| Setting | Behavior |
| --- | --- |
| `ConnectionStrings__IssueTracker` | Overrides the SQLite connection string; default JSON value is `Data Source=issuetracker.db` |
| `Database__ApplyMigrations` | Opt-in startup migrations; false when unset |
| `ASPNETCORE_ENVIRONMENT` | Local launch profiles set Development; container defaults to Production |
| `ASPNETCORE_HTTP_PORTS` | Dockerfile sets 8080 |

## Current limitations and remaining exercises

- Error responses are not fully consistent: project creation can return a plain error string, and `AddProblemDetails()` / `UseExceptionHandler()` are not configured. The learning guide describes the intended improvement.
- The Docker command persists the database only. Data Protection keys are not explicitly persisted, so existing login credentials may become invalid after container replacement; log in again. Durable key storage and HTTPS termination remain deployment work.
- Startup migrations are a convenience for this single-instance learning app. Multiple replicas and public hosting need a separate deployment plan.
- Historical milestone test failures are archived in [MILESTONE5_RESULTS.md](tests/IssueTracker.IntegrationTests/MILESTONE5_RESULTS.md); use a fresh test run to assess the current code.
