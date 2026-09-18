# Project API integration tests

Run from the solution root:

```bash
dotnet test tests/IssueTracker.IntegrationTests
```

Run all unit and integration tests:

```bash
dotnet test
```

Run a single behavior while debugging:

```bash
dotnet test tests/IssueTracker.IntegrationTests --filter "FullyQualifiedName~Put_UpdatesBothFields"
```

`ProjectApiTests.cs` tests POST, GET list, GET by ID, and PUT project routes. `Milestone5ApiTests.cs` adds project deletion and the work-item/comment routes, covering all 15 milestone 5 endpoints together. These requests go through the real ASP.NET Core pipeline, controllers, services, and SQLite database. They do not mock the service or require a separately running API.

Coverage includes:

- Creation status, Location header, JSON property names, generated IDs and UTC timestamps.
- Empty and populated lists, retrieval, unknown and malformed IDs.
- Name trimming and exact length boundaries; missing, null, blank and oversized values.
- Malformed JSON, wrong JSON value types, missing bodies and unsupported content types.
- Validation Problem Details and rejected requests preserving existing data.
- Updates of both fields, omitted/null description clearing, repeat updates, preserved identity and creation time.
- URL ID taking precedence over extra body fields; another project remaining unchanged.
- Real migrations and saved data surviving disposal and recreation of the API host.

Each test case creates its own uniquely named temporary directory and SQLite database. `ApiFactory` overrides the connection string, checks that the resolved database path is correct, and applies the real migrations. Pooling is disabled so disposal can release and remove the test files. The restart test reuses only its own database across two hosts. Tests never use the development `issuetracker.db`.

The milestone 5 tests additionally cover work-item title boundaries (1–200), comment body boundaries (1–2,000), parent-scoped lists, string enum JSON, all status transitions and no-ops, invalid/missing statuses, server-owned fields, missing parents/resources, and all three DELETE endpoints. Cascade tests inspect database rows and confirm unrelated projects/items/comments survive. Seed data is inserted through EF so failures in POST do not prevent independent testing of other routes.

Run milestone 5 additions alone:

```bash
dotnet test tests/IssueTracker.IntegrationTests --filter "FullyQualifiedName~Milestone5ApiTests"
```

Tests intentionally assert the learning guide's contract, so unfinished or incorrect routes should fail. The test host does not supply missing production service registrations or override enum serialization. See `MILESTONE5_RESULTS.md` for the latest recorded run and blockers.

These tests do not cover future authentication, pagination, or TLS/network deployment. HTTPS client URLs avoid redirects in the in-process test server; they do not test certificates.

The existing `IssueTracker.UnitTests` project remains separate and tests entity behavior directly.
