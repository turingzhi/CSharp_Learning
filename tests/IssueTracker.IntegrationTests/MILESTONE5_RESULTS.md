# Milestone 5 API test results

Run on 2026-09-18: **110 failed, 0 passed, 0 skipped** (48 existing project cases plus 62 new milestone 5 cases). The API and integration-test project compiled. No application implementation was changed during this review.

All tests use isolated temporary SQLite databases initialized from the checked-in migration. The development database is not used.

## Confirmed blockers from the test run

1. **Missing dependency-injection registrations: 108 failures.** `Program.cs` registers only `ProjectService`. `ProjectController` now also requires `WorkItemService`; `WorkItemController` requires `WorkItemService` and `CommentService`; `CommentController` requires `CommentService`. Add both scoped registrations before `builder.Build()`. Until then, most requests cannot activate their controllers, including previously passing project endpoints.
2. **Wrong comment-list route: 2 failures.** The guide requires `GET /api/work-items/{workItemId}/comments`, but the controller maps `GET /api/work-items/{workItemId}/work-items`. A known item receives 405 instead of 200; the missing-parent case also receives 405 instead of 404.

These are shared blockers, not 110 independent bugs. Fix them first and rerun to reveal the behavior of the remaining tests.

## Additional issues found by code inspection (blocked from full HTTP verification)

- `Program.cs` has no string enum converter. Configure controller JSON with `JsonStringEnumConverter` and `allowIntegerValues: false`; the guide expects named string statuses on requests and responses.
- Both `AddItemInProjectRequest.Title` and `UpdateWorkItemRequest.Title` have `[StringLength(100)]`; work-item titles must allow 200 characters.
- `Comment` calculates `bodyTrimed` but assigns `Body = body`; assign the trimmed value.
- `ChangeStatusRequest.Status` is a non-nullable enum without required-field validation. Missing `status` defaults to Todo; use a validation strategy that rejects an absent value. The new suite explicitly checks missing, null, unknown and numeric values.
- A stray `[HttpPost]` remains above the commented-out creation method in `WorkItemController`. Attributes attach to the next actual declaration, so this also decorates `GetById`. Remove the stray attribute; it introduces an unintended route and may affect link generation.
- The guide asks for `AddProblemDetails()` and `UseExceptionHandler()` for unexpected errors; neither is configured yet. POST project also still returns a plain error string from its catch block instead of Problem Details.

## Rerun

```bash
dotnet test tests/IssueTracker.IntegrationTests
```

For a machine-readable report:

```bash
dotnet test tests/IssueTracker.IntegrationTests --logger "trx;LogFileName=milestone5.trx"
```

The latest raw results are under `TestResults/milestone5.trx` (ignored build output). Do not weaken assertions or add service registrations in the test factory to conceal application failures.
