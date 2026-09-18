# IssueTracker learning takeaways

A plain-language reference for the C#, .NET, and backend concepts practiced in this project. Examples are learning snippets, not extra code you need to paste into the application.

## 1. How the technologies fit together

| Technology | What it does | Example in IssueTracker |
| --- | --- | --- |
| C# | The programming language | Classes, methods, conditions, `async` |
| .NET runtime | Runs the compiled application | Executes `IssueTracker.Api.dll` |
| .NET SDK | Tools for building and developing | `dotnet build`, `dotnet test` |
| ASP.NET Core | Framework for web applications | Routes, controllers, authentication |
| EF Core | Maps objects and queries to a database | `AppDbContext`, LINQ queries, migrations |
| SQLite | Stores persistent data | Projects, work items, users |
| xUnit | Runs automated tests | Unit and integration tests |
| Docker | Packages and runs the application in a container | Published API plus its runtime |

C# does not require a database, EF Core, or Docker. These tools solve separate problems in this application.

## 2. Solution, projects, and files

- A **solution** (`.sln`) groups related projects.
- A **project** (`.csproj`) defines its target framework, package dependencies, and build settings.
- A **project reference** lets one project use another project's types.
- A **NuGet package** adds an external library.
- A **namespace** organizes type names. A `using` directive lets you use those names without writing the full namespace; it does not install a library.
- `bin/` contains build output. `obj/` contains intermediate build files.

Our responsibilities:

```text
IssueTracker.Core              Domain objects and business rules
IssueTracker.Playground        Console experiments
IssueTracker.Api               HTTP, services, database, security
IssueTracker.UnitTests         Small isolated behavior tests
IssueTracker.IntegrationTests  Tests across application components
```

Keep generated build output out of Git. Keep source code and migration files in Git.

## 3. C# types and objects

A **class** describes a type of object. An **object** is one instance of that class.

```csharp
var item = new WorkItem(projectId, "Write documentation");
```

The constructor establishes the object's initial state. Properties expose its data; methods describe its behavior.

```csharp
public string Title { get; private set; }
```

Other code can read `Title`, but only code inside the class can set it directly. A method such as `Rename()` can validate a change before applying it. This is **encapsulation**.

Useful types:

| Type or syntax | Meaning |
| --- | --- |
| `string` | Text |
| `int` | Whole number |
| `bool` | True or false |
| `Guid` | Identifier |
| `DateTimeOffset` | Date/time with a UTC offset |
| `enum` | Named choices, such as Todo or Done |
| `List<WorkItem>` | A list whose elements are work items |
| `Task<WorkItem>` | An asynchronous operation that produces a work item |
| `var` | Let the compiler infer the type; it is still statically typed |

An **interface** describes a contract that implementations must satisfy. **Generics**, such as `List<T>`, let a type or method work with different types while retaining type checking.

## 4. Nullability and validation

`string?` means a reference is allowed to be null. `string` expresses that it should not be null when nullable reference checking is enabled.

Nullable annotations help the compiler warn you. They do not automatically validate incoming HTTP data at runtime.

```csharp
if (string.IsNullOrWhiteSpace(title))
{
    throw new ArgumentException("Title is required.", nameof(title));
}
```

Validate at two useful boundaries:

- **Request validation:** Is the incoming payload well formed and complete?
- **Domain validation:** Does this change obey the object's business rules?

Remember the errors you encountered:

- `CS1061`: The type does not expose the member you called, such as a missing `Rename` method.
- `CS8604`: A possibly null value is being passed to a parameter that expects a non-null value.

The null-forgiving operator (`!`) suppresses a compiler warning. It does not prevent null at runtime. Prefer validating the value when null is possible.

## 5. Entities and DTOs

An **entity** represents something with identity and behavior, such as a project or work item.

A **DTO** (data transfer object) describes data crossing the API boundary:

- Request DTO: fields a client is allowed to submit.
- Response DTO: fields the API chooses to return.

Separate DTOs prevent clients from directly controlling internal fields. For example, derive a comment's author from the authenticated account, not a user-supplied author ID.

## 6. How a request moves through the API

```text
HTTP request
    → middleware (including authentication and authorization)
    → MVC filters and controller action
    → service and domain logic
    → AppDbContext / EF Core
    → SQLite
    → HTTP response
```

- **Routing** selects an endpoint using the URL and HTTP method.
- **Middleware** handles requests as they pass through the application pipeline.
- **Controllers** translate HTTP input into application operations and return HTTP responses.
- **Services** coordinate application work.
- **Entities** enforce their own domain rules.
- **DbContext** is EF Core's entry point for querying and saving data.

Our project-access filter also checks permissions against the actual project associated with a requested resource.

## 7. HTTP essentials

| Method | Typical purpose |
| --- | --- |
| GET | Read data |
| POST | Create a resource or perform an operation |
| PUT | Replace/update the resource represented by the endpoint |
| DELETE | Remove a resource |

| Status | Meaning in our API |
| --- | --- |
| 200 | Request succeeded, usually with a response body |
| 201 | Resource created |
| 204 | Succeeded without a response body |
| 400 | Invalid request |
| 401 | Authentication is missing or invalid |
| 403 | Authenticated, but not permitted to perform this action |
| 404 | Resource missing, or deliberately hidden from this caller |
| 409 | Conflict, such as duplicate membership or removing an owner |
| 500 | Unexpected server failure |

`Content-Type: application/json` describes the request body's format. `Authorization: Bearer ...` supplies an access token.

Route values identify resources; query parameters commonly control filtering and pagination:

```text
/api/projects/{projectId}/work-items?page=1&pageSize=2
```

## 8. Dependency injection and lifetimes

**Dependency injection (DI)** means the framework supplies an object's dependencies instead of that object constructing all of them itself.

```csharp
builder.Services.AddScoped<WorkItemService>();
```

This registration lets ASP.NET Core create and inject `WorkItemService` when needed.

| Lifetime | Instance sharing | Example |
| --- | --- | --- |
| Transient | A new instance each time it is resolved | A small stateless formatter, if registered this way |
| Scoped | One instance within a scope; normally one HTTP request | Our services and `AppDbContext` |
| Singleton | One instance for the application's lifetime | A thread-safe shared utility, if registered this way |

A singleton must be safe for concurrent requests. Do not store request-specific user data in it or directly capture a scoped DbContext. DbContext is not thread-safe.

## 9. Async, await, and cancellation

```csharp
var item = await db.WorkItems.FindAsync(id);
```

An asynchronous database operation can release the calling thread while waiting for I/O. `await` resumes the method when the operation completes.

- `async` does not mean every operation runs on a new thread.
- Await work before using its result.
- Avoid blocking asynchronous code with `.Result` or `.Wait()` in request handling.
- Pass `CancellationToken` through supported service/database calls so abandoned requests can stop unnecessary work.
- Do not run concurrent database operations on the same DbContext.

## 10. LINQ, queries, and saving

LINQ expresses filtering, ordering, and projection in C#:

```csharp
var items = await db.WorkItems
    .Where(item => item.ProjectId == projectId)
    .OrderBy(item => item.CreatedAt)
    .ThenBy(item => item.Id)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync(cancellationToken);
```

With an EF-backed query, EF translates supported expressions into database queries. `ToListAsync()` executes the query and collects the results. LINQ over an in-memory list works in memory instead.

- Filter before fetching unnecessary data.
- Use stable ordering before pagination; a unique tie-breaker helps.
- Validate page numbers and limit page size.
- `AsNoTracking()` is useful for read-only entity queries.
- Changes to tracked entities are persisted by `SaveChangesAsync()`.

A **transaction** groups operations so they commit together or roll back together. Removing a member and clearing that member's assignments should succeed as one operation.

## 11. Database structure and migrations

**C# itself does not need migrations. A database needs schema changes when the application's storage requirements change.**

Changing this C# property does not add a column to an existing SQLite file:

```csharp
public string? AssigneeId { get; private set; }
```

A migration records the database change needed to match the new EF model.

```text
Change entity or EF mapping
    → generate migration
    → review migration
    → apply migration
    → database structure matches the new model
```

Migration files have different jobs:

- `Up()`: applies the schema change.
- `Down()`: describes the reverse change; reversing a migration can still lose data.
- Model snapshot: records the EF model used to calculate the next migration. It is not a copy of your database rows.
- `__EFMigrationsHistory`: records migrations applied to a particular database.

From the **IssueTracker solution folder**:

```bash
# Generate instructions; this does not update the database.
dotnet ef migrations add DescribeYourChange \
  --project src/IssueTracker.Api \
  --startup-project src/IssueTracker.Api

# Apply pending instructions to the configured database.
dotnet ef database update \
  --project src/IssueTracker.Api \
  --startup-project src/IssueTracker.Api
```

Review generated migrations before applying them. Back up meaningful data before changes that might transform or remove it. A new row does not require a migration; a schema change usually does.

### Why we changed CreatedAt storage

SQLite has limitations when ordering some .NET date/time representations. We configured a conversion to integer ticks to support the required query behavior. The model still exposes a date/time value; the stored representation is different.

The migration changes the column/storage schema. Existing values may need explicit data conversion; changing a column type alone does not guarantee old date strings become valid ticks. Our earlier check showed the table was empty before that change.

### Why Docker used migrations

The container's new database volume started empty. Startup `MigrateAsync()` created its schema by applying migrations. Later starts apply only pending migrations.

`Database__ApplyMigrations=true` enabled this in our single-container learning setup. For multiple application instances, prefer a separate controlled deployment migration step.

## 12. Authentication and authorization

- **Authentication:** Who are you?
- **Authorization:** What are you allowed to do?

ASP.NET Core Identity handles account infrastructure and password hashing. Never store plain-text passwords or implement password hashing yourself for this application.

Login produces credentials the client sends with later requests. The server validates them and builds a `ClaimsPrincipal` describing the authenticated user. A claim is a piece of information about that identity, such as the user ID.

Do not assume every bearer token is a JWT. The built-in Identity API bearer tokens used here are not standard JWTs.

Our permission rules:

- Creating a project makes the creator its Owner.
- Owners manage membership; adding a member does not grant ownership.
- Members access resources belonging to their projects.
- Only eligible project members can be assigned a work item.
- Removing a member clears their assignments in that project.
- Owners cannot remove the project's owner membership through the member-removal endpoint.
- Comment deletion checks the actual author or project owner.

Knowing a resource ID is not permission. Check access on the server for each request, including nested resources and list endpoints. Membership checks also allow removal to take effect even while an old login token remains valid.

## 13. Logging and errors

Inject a logger into the class that needs it:

```csharp
public sealed class ExampleService(ILogger<ExampleService> logger)
{
    public void RecordCreation(Guid workItemId)
    {
        logger.LogInformation("Created work item {WorkItemId}", workItemId);
    }
}
```

The message template preserves `WorkItemId` as a structured value that logging tools can search.

- Log useful events and unexpected failures.
- Never log passwords or access tokens.
- Use appropriate levels: Debug, Information, Warning, Error.
- Return useful client errors without exposing internal exception details in production.

## 14. Testing

**Unit tests** verify small pieces of behavior, such as rejecting an empty title.

**Integration tests** verify components working together, such as HTTP routing, Identity, permission checks, EF Core, and SQLite.

A useful test follows **Arrange → Act → Assert**:

1. Set up the required data and caller.
2. Perform one behavior.
3. Check the observable result.

Important cases from our application:

- Anonymous caller receives 401.
- Outsider cannot access another project's data.
- Member cannot perform an owner-only action.
- Assignment rejects a nonmember.
- Removing a member clears only that project's assignments.
- Invalid requests do not silently change stored data.

Passing tests provide evidence for the cases covered, not proof that no bugs exist. Use an isolated test database so tests do not modify your personal development data.

## 15. Configuration and environments

With the default application setup, configuration commonly comes from JSON files, development user secrets, environment variables, and command-line arguments. Later providers can override earlier values; custom provider setup can change this order.

```text
appsettings.json
    → appsettings.{Environment}.json
    → development user secrets
    → environment variables
    → command-line arguments
```

Environment variables use `__` for nested keys:

```text
ConnectionStrings__IssueTracker
           ↓
ConnectionStrings:IssueTracker
```

- Development enables development-specific behavior such as our OpenAPI endpoint.
- Production should avoid exposing detailed internal errors.
- User secrets keep local sensitive settings out of source files; they are not an encrypted production secret store.
- A relative SQLite path depends on where the process runs. An explicit path helps avoid accidentally creating a second database.

Our `/health` endpoint is a **liveness** check: the application can respond. It does not currently prove database readiness.

## 16. Build, run, test, and publish

Run these from the **IssueTracker solution folder**:

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/IssueTracker.Api --launch-profile https
dotnet build -c Release
dotnet test -c Release
dotnet publish src/IssueTracker.Api -c Release -o publish
```

- **Restore:** obtains project dependencies.
- **Build:** compiles the code.
- **Run:** builds by default, then starts the selected application.
- **Test:** runs automated tests, building by default.
- **Publish:** gathers deployable application output and dependencies.

Release is a build configuration; Production is a runtime environment. They are separate settings.

Our framework-dependent published output needs a compatible .NET runtime. The Docker runtime image supplies it.

If your terminal is already inside `src/IssueTracker.Api`, use:

```bash
dotnet run --launch-profile https
```

The error “provided file path does not exist” can simply mean you used a path relative to the wrong working directory. Check `pwd` first.

## 17. Docker and persistence

| Term | Meaning |
| --- | --- |
| Dockerfile | Instructions for building an image |
| Image | Packaged application and runtime files |
| Container | An instance of an image |
| Volume | Storage with a lifetime separate from a container |
| Port mapping | Makes a container port reachable through a host port |

Our multi-stage Dockerfile uses the SDK image to build and the ASP.NET runtime image to run. The final image does not need the entire SDK or source tree.

```text
Container can be removed and replaced
    ├── issuetracker-data volume → SQLite database survives
    └── issuetracker-keys volume → authentication protection keys survive
```

Persistent protection keys let the replacement application validate still-valid protected credentials, provided its other protection settings remain compatible. They do not stop tokens from expiring.

- `docker build -t issuetracker .` builds an image using the current folder as context.
- `.dockerignore` excludes files from that build context.
- `-p 127.0.0.1:8080:8080` exposes container port 8080 on the local machine only.
- `--rm` removes the container when it exits; it does not remove the named volumes used here.
- `-d` starts a container in the background.
- `docker logs issuetracker` shows its output.
- A named volume is persistent storage, not a backup by itself.

Our loopback HTTP container exercise is local testing. A public deployment also needs an appropriate HTTPS setup and production operations planning.

## 18. Debugging checklist

1. Read the first meaningful error, including its file and line number.
2. Check the terminal's current folder and the API's actual listening port.
3. Separate compilation failures, runtime exceptions, HTTP errors, and database errors.
4. Check which database file and environment the process is using.
5. Inspect logs without printing credentials.
6. Reproduce with the smallest request or test.
7. Fix the cause and rerun the relevant tests.

Common reminders:

| Symptom | Check |
| --- | --- |
| Method not found at compile time | Actual method name, signature, type, project reference |
| Possible null argument warning | Validate the input or intentionally support null |
| No such table/column | Correct database path and applied migrations |
| 401 | Token supplied, valid, and not expired |
| 403 | Caller has the required permission |
| 404 | Route, resource existence, and project access |
| Connection refused | App running and correct port |
| Old behavior after editing | Rebuild/restart; rebuild Docker image when needed |

## 19. Questions to answer in your own words

- What is the difference between C#, .NET, and ASP.NET Core?
- Why expose a Rename method instead of a public Title setter?
- Why separate request DTOs from entities?
- What happens between an HTTP request and a database write?
- Why does DbContext normally have a scoped lifetime?
- Why does adding a C# property not update an existing database?
- How does EF know which migrations already ran?
- Why is a valid login token insufficient for accessing every project?
- What does an integration test check that a unit test might miss?
- Why can a container disappear while its data remains?

## 20. Personal learning log template

Copy this section when you learn something new:

```markdown
### Topic and date

**What I learned:**

**Explanation in my own words:**

**Example from IssueTracker:**

**Mistake I made and how I fixed it:**

**How I verified it:**

**Question I still have:**
```
