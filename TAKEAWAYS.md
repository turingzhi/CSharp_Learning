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

## 19. C# fundamentals: classes, inheritance, and keywords

These examples are independent learning snippets. They do not require changing IssueTracker's design.

### Instance members versus static members

An **instance member** belongs to a particular object. A **static member** belongs to the type itself.

```csharp
public class ProjectLabel
{
    public string Name { get; }

    public ProjectLabel(string name)
    {
        Name = name;
    }

    public string Describe() => $"Project: {Name}";

    public static bool IsValidName(string name)
        => !string.IsNullOrWhiteSpace(name);
}

// Usage inside a method:
var project = new ProjectLabel("IssueTracker");
string description = project.Describe();          // Needs an object.
bool valid = ProjectLabel.IsValidName("My app");   // Called on the type.
```

A static method has no `this` object. It cannot directly read an instance's `Name`; it would need an object passed to it.

### Static classes

A static class groups operations that do not need instances. You cannot create it with `new`, inherit from it, or use instance members inside it.

```csharp
public static class TitleRules
{
    public static bool IsValid(string? title)
        => !string.IsNullOrWhiteSpace(title);
}

// Usage:
bool valid = TitleRules.IsValid("Fix login");
```

Use a static helper for a small operation that needs no object state. Services with dependencies such as a database or logger are usually easier to test and configure as injected instances.

**Static is not the same as singleton:** a DI singleton is still an object, can implement an interface, and is created/managed by the DI container. A static class cannot be injected as an instance. Neither approach makes mutable shared state automatically thread-safe.

### Access modifiers

| Keyword | Who can access it? |
| --- | --- |
| `public` | Any code that can access the containing type |
| `private` | Code inside the containing type |
| `protected` | The containing class and derived classes |
| `internal` | Code in the same assembly, usually the same project output |

```csharp
public string Title { get; private set; }
```

This allows public reading, but setting is private. Access modifiers help enforce which code may change an object's state.

### Inheritance: an “is a” relationship

Inheritance lets a derived class reuse and specialize a base class. A class can inherit from one base class and implement multiple interfaces.

```csharp
public class Notification
{
    public string Message { get; }

    public Notification(string message)
    {
        Message = message;
    }
}

public class EmailNotification : Notification
{
    public string Recipient { get; }

    public EmailNotification(string message, string recipient)
        : base(message)
    {
        Recipient = recipient;
    }
}
```

`EmailNotification` **is a** `Notification`. `base(message)` calls the base constructor. `this` refers to the current object; `base` lets derived code refer to base-class members or constructors.

In IssueTracker, `ApplicationUser : IdentityUser` extends Identity's user type. Inheriting from a framework class gives your class its supported behavior and extension points.

### Virtual and override

`virtual` means a base class supplies behavior that a derived class is allowed to replace. `override` supplies that replacement.

```csharp
public class Notification
{
    public virtual string Format() => "General notification";
}

public class EmailNotification : Notification
{
    public override string Format() => "Email notification";
}

// Usage:
Notification notification = new EmailNotification();
Console.WriteLine(notification.Format()); // Email notification
```

The variable is declared as `Notification`, but the object is an `EmailNotification`. C# chooses the override on the actual object. This is **runtime polymorphism**.

You can override inherited members that support overriding, such as `virtual`, `abstract`, or a non-sealed `override`. An ordinary non-virtual method cannot be overridden. Properties can also be virtual or abstract.

Our EF configuration uses this pattern:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Add our entity mappings here.
}
```

The override adds our configuration. Calling the base implementation preserves Identity's model configuration. Calling `base` is not mandatory in every override; it is necessary here for the intended framework behavior.

### Abstract classes and abstract methods

An abstract class is an incomplete base type. You cannot instantiate it directly. It can contain constructors, state, implemented methods, and abstract members.

An abstract method declares what derived classes must implement, without providing its own method body.

```csharp
public abstract class Notification
{
    public string Message { get; }

    protected Notification(string message)
    {
        Message = message;
    }

    public abstract string Format();

    public string Preview() => $"Preview: {Format()}";
}

public class EmailNotification : Notification
{
    public EmailNotification(string message) : base(message) { }

    public override string Format() => $"Email: {Message}";
}

// Usage:
Notification notification = new EmailNotification("Work item assigned");
Console.WriteLine(notification.Preview());
// Preview: Email: Work item assigned
```

`new Notification(...)` would fail because the class is abstract. A concrete derived class must implement its inherited abstract members. An abstract derived class can leave them for later subclasses.

**Virtual:** “Here is a default implementation; you may replace it.”

**Abstract:** “You must provide an implementation before this can be a concrete class.”

### Interfaces versus abstract classes

An interface describes a capability that implementing types provide.

```csharp
public interface ITitleFormatter
{
    string Format(string title);
}

public class PlainTitleFormatter : ITitleFormatter
{
    public string Format(string title) => title.Trim();
}
```

Implementing this interface method does not require `override`. The class fulfills an interface contract rather than replacing a base-class implementation.

| Choose | When it helps |
| --- | --- |
| Interface | Different types need to provide the same capability |
| Abstract class | Related types share instance state or implementation and need extension points |
| Ordinary class | The type is complete and can be instantiated |
| Static class | Operations need no instances |

Modern interfaces have additional features, including default implementations, but they do not provide ordinary per-object fields or instance constructors like a base class does.

Avoid adding an interface to every class automatically. Add one when a meaningful contract, interchangeable implementation, or dependency boundary helps.

### Composition: a “has a” relationship

Composition means an object uses another object rather than inheriting from it.

```csharp
public class ReportService
{
    private readonly ITitleFormatter _formatter;

    public ReportService(ITitleFormatter formatter)
    {
        _formatter = formatter;
    }

    public string CreateHeading(string title) => _formatter.Format(title);
}
```

A report service **has a** formatter; it **is not a** formatter. Dependency injection is one way to supply that collaborator. Our services use DbContext this way.

### Overload versus override versus hiding

- **Overload:** same method name, different parameter signatures. The compiler selects the applicable signature. Changing only the return type is not enough.
- **Override:** a derived class specializes an inherited virtual/abstract member. Runtime dispatch selects the implementation.
- **Hiding with `new`:** a derived member hides a base member; this does not provide the same polymorphic behavior as overriding. Avoid it unless that distinction is intentional.

```csharp
public string Format(string title) => title.Trim();
public string Format(string title, string prefix) => $"{prefix}: {title.Trim()}";
```

These are overloads intended to be declared inside a class.

### Sealed

`sealed class` prevents other classes from inheriting from that class. A `sealed override` prevents further derived classes from overriding that member again.

```csharp
public sealed class PlainTitleFormatter : ITitleFormatter
{
    public string Format(string title) => title.Trim();
}
```

Sealed does not mean static or immutable. You can still create instances, and those instances may have mutable state.

### Fields, properties, const, readonly, and init

- A **field** stores a value directly, usually as private implementation detail.
- A **property** controls access through `get`, `set`, or `init` accessors. An automatic property has compiler-generated backing storage.
- `const` defines a compile-time constant; a class constant is accessed through its type.
- `readonly` permits field assignment in its declaration or the relevant constructor, but prevents later reassignment.
- `init` permits property assignment during object initialization, but not ordinary later reassignment.

```csharp
public class PagingOptions
{
    public const int MaximumPageSize = 100;
    private readonly string _source;
    public int PageSize { get; init; } = 20;

    public PagingOptions(string source)
    {
        _source = source;
    }
}

// Usage:
var options = new PagingOptions("API") { PageSize = 10 };
```

A readonly field holding a list cannot be reassigned after construction, but the list's contents can still change. `readonly` and `init` do not automatically make an entire object graph immutable.

### Class, struct, and record

| Type | Main idea |
| --- | --- |
| `class` | Reference type; assignment copies the reference to an object |
| `struct` | Value type; assignment copies its value |
| `record` / `record class` | Reference type with generated value-based equality and other data-oriented features |
| `record struct` | Value type with generated record features |

With an ordinary class, two variables can refer to the same object. Changing that object's state through one reference is visible through the other.

Copying a struct copies its fields. If a field itself holds a reference, the referenced object is still shared; this is not an automatic deep copy.

```csharp
public record ProjectSummary(Guid Id, string Name);
```

Records are useful for data carriers such as DTOs. They are not automatically deeply immutable. Record equality compares member values using those members' equality rules; it does not automatically compare every collection element structurally.

### Lambdas and delegates

A **delegate** represents a callable method with a particular signature. A **lambda** is a concise way to write an anonymous function.

```csharp
Func<string, bool> hasText = title => !string.IsNullOrWhiteSpace(title);
bool valid = hasText("Fix login");
```

`Func<string, bool>` takes a string and returns a bool. `Action<string>` takes a string and returns no value.

In `.Where(item => item.ProjectId == projectId)`, the lambda describes the condition. EF queries can capture it as an expression tree for SQL translation instead of simply running it as an in-memory delegate.

### Exceptions and using

`throw` reports a failure; `try`/`catch` handles an exception where useful recovery or translation is possible. `finally` runs when control leaves the try/catch, including during normal exception propagation.

`using` has two different common meanings:

```csharp
using System.Text; // Namespace import.
```

```csharp
using var stream = File.OpenRead("example.txt");
// The stream is disposed when this scope ends.
```

Resource disposal releases things such as file handles or database connections. It is different from garbage collection, which manages memory. `await using` supports asynchronous disposal for types that implement it.

### Quick keyword recap

| Keyword | Remember it as |
| --- | --- |
| `static` | Belongs to the type, not an instance |
| `abstract` | Incomplete base type or member requiring implementation |
| `virtual` | Existing implementation may be overridden |
| `override` | Specialize an inherited overridable member |
| `sealed` | Stop inheritance or further overriding |
| `interface` | Define a capability/contract |
| `this` | The current object |
| `base` | Access base-class behavior or constructor |
| `readonly` | Prevent later field reassignment |
| `const` | Compile-time constant |
| `init` | Allow property assignment during initialization |

## 20. Questions to answer in your own words

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
- How is a static method different from an instance method?
- How is a static class different from a DI singleton?
- When would you choose an interface instead of an abstract class?
- What is the difference between virtual and abstract methods?
- How is overriding different from overloading?
- Why do we call base.OnModelCreating in our DbContext override?
- Why does a readonly list field still allow adding list elements?

## 21. Personal learning log template

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
