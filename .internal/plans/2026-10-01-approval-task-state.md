# Approval Task (State pattern) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use beads-superpowers:subagent-driven-development (recommended) or beads-superpowers:executing-plans to implement this plan task-by-task. Each Task becomes a bead (`bd create -t task --parent <epic-id>`). Steps within tasks use checkbox (`- [ ]`) syntax for human readability.

**Goal:** Implement the three approval-task endpoints from `docs/TEST_TASK.md` (create, execute action, get) with a State pattern for per-status rules, an in-memory repository, per-task locking, and tests for every business rule.

**Architecture:** `ApprovalTasksController` (Api) → `IApprovalTaskService` (Core) → `ApprovalTaskService` orchestrator (Infrastructure), which always runs its own `Validate(...)` on the command first, then asks `ApprovalTaskStateFactory` for the `IApprovalTaskState` of the task's current status, calls `state.Validate(...)` and `state.Apply(...)`, and saves through `IApprovalTaskRepository` (`InMemoryApprovalTaskRepository`, returns copies). A per-task `Lock` serializes actions on the same task. All client-facing messages and error codes are constants in `ApprovalTaskMessages`.

**Tech Stack:** .NET 10, ASP.NET Core controllers, xUnit 2.9, `WebApplicationFactory<Program>` (already introduced by the JWT plan). No new NuGet packages.

**Spec:** `.internal/specs/2026-10-01-approval-task-state-design.md`

**Prerequisite:** `.internal/plans/2026-10-01-jwt-auth.md` is fully executed. This plan uses from it: `EdocsTestTask.Core.Constants.UserRoles` (`Author`, `Approver`), `EdocsTestTask.Api.Authentication.ClaimsPrincipalExtensions.GetUserId(this ClaimsPrincipal)`, and the test helpers `EdocsTestTask.Tests.Api.Auth.AuthApiFactory` (`JwtOptions JwtOptions`), `TestJwtFactory(JwtOptions)` with `string Create(string? subject, string? role, ...)`, `AuthTestHelpers.AssertErrorAsync(HttpResponseMessage, string expectedCode)`; the `.http` variables `@authorToken`, `@approverToken`, `@otherApproverToken`; and `README.md` with an «Автентифікація» section. Check with `git log --oneline` that the auth commits exist and `dotnet test` is green before Task 1.

## Global Constraints

- Code style (match existing files): block-scoped `namespace X { ... }`, `#region` blocks (`Constants`, `Fields`, `Properties`, `Constructors`, `Methods`, `Private Methods`, `Nested Types`, `Helpers`, `Tests`), one class per file, XML `<summary>` on public types, `ArgumentNullException.ThrowIfNull` on public entry points, braces on every `if`.
- Layering: Shared ← Core ← Infrastructure ← Api. Core never references Infrastructure or Api; Shared references nothing.
- Every client-facing message is a `const string` in `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`; every error code is a `const string` in its nested `Codes` class. No message literals in services, states or controllers. Templates are filled with `string.Format(CultureInfo.InvariantCulture, ...)`.
- Title rule: required (not null/empty/whitespace) and trimmed length within `ValidationConstants.TitleMinLength..TitleMaxLength` = `3..200`, checked through `TextValidationHelper.IsTitleValid(command.Title, out var error)`.
- Status mapping (existing `ResultExtensions.ToStatusCode`): `Validation` → 400, `Forbidden` → 403, `NotFound` → 404, `Conflict` → 409.
- Check order in `ExecuteAction`: command validation (400) → task exists (404) → actor is assignee (403) → `state.Validate` (409 or 400).
- A failed action changes nothing: status, `UpdatedAtUtc` and `History` stay identical.
- All timestamps come from an injected `TimeProvider` and are stored as `DateTime` with `DateTimeKind.Utc`.
- Run all tests with: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj`.

## File Structure

| File | Task | Responsibility |
|---|---|---|
| `EdocsTestTask.Shared/Validation/ValidationResult.cs` | 1 | + `AddError(Error)` for non-Validation errors |
| `EdocsTestTask.Shared/Results/ResultOfT.cs` | 1 | + `Map<TOut>(Func<T, TOut>)` |
| `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs` | 2 | All messages + nested `Codes` |
| `EdocsTestTask.Core/Constants/ValidationConstants.cs` | 2 | `TitleMaxLength` 30 → 200 |
| `EdocsTestTask.Core/Helpers/TextValidationHelper.cs` | 2 | `IsTitleValid`, `IsRequiredTextValid` |
| `EdocsTestTask.Core/Enums/ApprovalStatus.cs`, `ApprovalAction.cs` | 3 | Enums |
| `EdocsTestTask.Core/Entities/ApprovalTask.cs`, `ApprovalHistoryEntry.cs` | 3 | Entity POCO + `Clone()`, immutable history record |
| `EdocsTestTask.Core/Interfaces/Repositories/IApprovalTaskRepository.cs` | 4 | Repository contract |
| `EdocsTestTask.Infrastructure/Repositories/InMemoryApprovalTaskRepository.cs` | 4 | `ConcurrentDictionary` storage, copies in and out |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/*.cs` | 5 | `IApprovalTaskState`, context, base classes, four states, factory |
| `EdocsTestTask.Core/Commands/CreateApprovalTaskCommand.cs`, `ExecuteActionCommand.cs` | 6 | Service inputs |
| `EdocsTestTask.Core/Interfaces/Services/IApprovalTaskService.cs` | 6 | Service contract |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs` | 6 | Command validation + action parsing |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs` | 6, 7 | Orchestrator (Task 7 adds the per-task lock) |
| `EdocsTestTask.Infrastructure/DependencyInjection.cs` | 6 | Registrations |
| `EdocsTestTask.Api/Contracts/ApprovalTasks/*.cs`, `EdocsTestTask.Api/Mappings/ApprovalTaskMappings.cs`, `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs` | 8 | HTTP surface |
| `EdocsTestTask.Api/EdocsTestTask.Api.http`, `README.md` | 9 | Examples and reviewer docs |
| `EdocsTestTask.Tests/...` | 1–8 | Tests next to each task |

---

### Task 1: Shared — `ValidationResult.AddError(Error)` and `Result<T>.Map`

**Files:**
- Modify: `EdocsTestTask.Shared/Validation/ValidationResult.cs` (add method in `#region Methods`, after `AddError(string, string)`)
- Modify: `EdocsTestTask.Shared/Results/ResultOfT.cs` (add `#region Methods` after `#region Factories`)
- Test: `EdocsTestTask.Tests/Shared/ValidationResultTests.cs` (create)
- Test: `EdocsTestTask.Tests/Shared/ResultMapTests.cs` (create)

**Interfaces:**
- Consumes: existing `Error`, `ErrorType`, `Result<T>`, `ValidationResult`.
- Produces: `void ValidationResult.AddError(Error error)`; `Result<TOut> Result<T>.Map<TOut>(Func<T, TOut> map)`.

**Acceptance Criteria:**
- `AddError(Error)` keeps the error's code, message and type (e.g. `Conflict`); `IsValid` becomes false; null throws `ArgumentNullException`.
- `Map` on success returns a successful `Result<TOut>` with the mapped value; on failure returns a failed `Result<TOut>` with the same errors and never calls the mapper; null mapper throws.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Shared/ValidationResultTests.cs`:

```csharp
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Tests.Shared
{
    public class ValidationResultTests
    {
        #region Tests

        [Fact]
        public void AddError_PrebuiltError_KeepsCodeMessageAndType()
        {
            var validation = new ValidationResult();
            var error = Error.Conflict("Test.Conflict", "Conflict happened");

            validation.AddError(error);

            Assert.False(validation.IsValid);
            Assert.Same(error, Assert.Single(validation.Errors));
            Assert.Equal(ErrorType.Conflict, validation.ToResult().Error!.Type);
        }

        [Fact]
        public void AddError_NullError_Throws()
        {
            var validation = new ValidationResult();

            Assert.Throws<ArgumentNullException>(() => validation.AddError((Error)null!));
        }

        #endregion
    }
}
```

Create `EdocsTestTask.Tests/Shared/ResultMapTests.cs`:

```csharp
using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Tests.Shared
{
    public class ResultMapTests
    {
        #region Tests

        [Fact]
        public void Map_Success_ReturnsMappedValue()
        {
            var result = Result<int>.Success(21).Map(value => (value * 2).ToString());

            Assert.True(result.IsSuccess);
            Assert.Equal("42", result.Value);
        }

        [Fact]
        public void Map_Failure_KeepsErrorsAndSkipsMapper()
        {
            var error = Error.NotFound("Test.NotFound", "Missing");
            var called = false;

            var result = Result<int>.Failure(error).Map(value =>
            {
                called = true;
                return value.ToString();
            });

            Assert.True(result.IsFailure);
            Assert.Same(error, Assert.Single(result.Errors));
            Assert.False(called);
        }

        [Fact]
        public void Map_NullMapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Map<string>(null!));
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~EdocsTestTask.Tests.Shared"`
Expected: build FAILS — `CS1503` (cannot convert `Error` to `string`) for `AddError(error)` and `CS1061` (`Result<int>` has no `Map`).

- [ ] **Step 3: Implement `AddError(Error)`**

In `EdocsTestTask.Shared/Validation/ValidationResult.cs`, directly after the existing `AddError(string message, string code = ValidationErrorCodes.Invalid)` method, add:

```csharp
        /// <summary>
        /// Adds a pre-built error, e.g. a Conflict raised by a business rule rather than an input check.
        /// </summary>
        public void AddError(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);

            _errors.Add(error);
        }
```

- [ ] **Step 4: Implement `Map`**

In `EdocsTestTask.Shared/Results/ResultOfT.cs`, after the closing `#endregion` of `#region Factories`, add:

```csharp

        #region Methods

        /// <summary>
        /// Projects the value of a successful result; a failed result keeps its errors and the mapper is not called.
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(map);

            return IsSuccess
                ? Result<TOut>.Success(map(_value!))
                : Result<TOut>.Failure(Errors);
        }

        #endregion
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~EdocsTestTask.Tests.Shared"`
Expected: PASS (new and existing Shared tests).

- [ ] **Step 6: Commit**

```bash
git add EdocsTestTask.Shared/Validation/ValidationResult.cs EdocsTestTask.Shared/Results/ResultOfT.cs EdocsTestTask.Tests/Shared/ValidationResultTests.cs EdocsTestTask.Tests/Shared/ResultMapTests.cs
git commit -m "feat(shared): add ValidationResult.AddError(Error) and Result<T>.Map"
```

---

### Task 2: Messages, title limit and `TextValidationHelper`

**Files:**
- Create: `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`
- Modify: `EdocsTestTask.Core/Constants/ValidationConstants.cs` (`TitleMaxLength`)
- Create: `EdocsTestTask.Core/Helpers/TextValidationHelper.cs`
- Test: `EdocsTestTask.Tests/Core/TextValidationHelperTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `ApprovalTaskMessages` constants: `TitleRequired`, `TitleLength` (`{0}` min, `{1}` max), `FieldRequired` (`{0}` field), `ActionRequired`, `UnknownAction` (`{0}` action), `TaskNotFound` (`{0}` id), `NotAssignee`, `InvalidTransition` (`{0}` action, `{1}` status), `RejectCommentRequired`, `TaskAlreadyFinalized` (`{0}` status).
  - `ApprovalTaskMessages.Codes`: `InvalidTitle`, `InvalidDocumentNumber`, `InvalidAssignee`, `InvalidActor`, `ActionRequired`, `UnknownAction`, `NotFound`, `NotAssignee`, `InvalidTransition`, `RejectCommentRequired`, `AlreadyFinalized` — values `"ApprovalTask.<Name>"`.
  - `static bool TextValidationHelper.IsTitleValid(string? title, [NotNullWhen(false)] out string? error)`
  - `static bool TextValidationHelper.IsRequiredTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)`

**Acceptance Criteria:**
- `IsTitleValid`: null / `""` / whitespace → false with `TitleRequired`; trimmed length 2 or 201 → false with `"Title must be between 3 and 200 characters."`; trimmed length 3 and 200 → true with `error == null`; `"  abc  "` → true.
- `IsRequiredTextValid`: null / `""` / whitespace → false with `"<fieldName> is required and cannot be empty or whitespace."`; any non-whitespace → true; blank `fieldName` throws `ArgumentException`.
- `ValidationConstants.TitleMaxLength == 200`.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Core/TextValidationHelperTests.cs`:

```csharp
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Helpers;

namespace EdocsTestTask.Tests.Core
{
    public class TextValidationHelperTests
    {
        #region Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsTitleValid_MissingTitle_ReturnsRequiredError(string? title)
        {
            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.False(isValid);
            Assert.Equal(ApprovalTaskMessages.TitleRequired, error);
        }

        [Theory]
        [InlineData(2, "")]
        [InlineData(2, "   ")]
        [InlineData(201, "")]
        public void IsTitleValid_LengthOutOfRange_ReturnsLengthError(int length, string padding)
        {
            var title = padding + new string('a', length) + padding;

            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.False(isValid);
            Assert.Equal("Title must be between 3 and 200 characters.", error);
        }

        [Theory]
        [InlineData(3, "")]
        [InlineData(200, "")]
        [InlineData(3, "  ")]
        public void IsTitleValid_LengthInRange_ReturnsTrue(int length, string padding)
        {
            var title = padding + new string('a', length) + padding;

            var isValid = TextValidationHelper.IsTitleValid(title, out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void IsRequiredTextValid_Missing_ReturnsFieldError(string? value)
        {
            var isValid = TextValidationHelper.IsRequiredTextValid(value, "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber is required and cannot be empty or whitespace.", error);
        }

        [Fact]
        public void IsRequiredTextValid_Present_ReturnsTrue()
        {
            var isValid = TextValidationHelper.IsRequiredTextValid("DOC-1", "DocumentNumber", out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void IsRequiredTextValid_BlankFieldName_Throws()
        {
            Assert.ThrowsAny<ArgumentException>(() => TextValidationHelper.IsRequiredTextValid("x", " ", out _));
        }

        [Fact]
        public void TitleMaxLength_Is200()
        {
            Assert.Equal(200, ValidationConstants.TitleMaxLength);
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~TextValidationHelperTests"`
Expected: build FAILS — `CS0234` namespace `EdocsTestTask.Core.Helpers` not found.

- [ ] **Step 3: Create `ApprovalTaskMessages`**

Create `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`:

```csharp
namespace EdocsTestTask.Core.Constants
{
    /// <summary>
    /// Client-facing messages and error codes for approval tasks.
    /// Templates take arguments via <c>string.Format(CultureInfo.InvariantCulture, ...)</c>.
    /// </summary>
    public static class ApprovalTaskMessages
    {
        #region Messages

        public const string TitleRequired = "Title is required and cannot be empty or whitespace.";

        /// <summary>{0} = minimum length, {1} = maximum length.</summary>
        public const string TitleLength = "Title must be between {0} and {1} characters.";

        /// <summary>{0} = field name.</summary>
        public const string FieldRequired = "{0} is required and cannot be empty or whitespace.";

        public const string ActionRequired = "Action is required.";

        /// <summary>{0} = action as sent by the client.</summary>
        public const string UnknownAction = "Unknown action '{0}'. Allowed: Start, Approve, Reject.";

        /// <summary>{0} = task id.</summary>
        public const string TaskNotFound = "Approval task '{0}' was not found.";

        public const string NotAssignee = "Only the assigned user can perform actions on this task.";

        /// <summary>{0} = action, {1} = current status.</summary>
        public const string InvalidTransition = "Action '{0}' is not allowed when task is in status '{1}'.";

        public const string RejectCommentRequired = "A non-empty comment is required to reject a task.";

        /// <summary>{0} = final status.</summary>
        public const string TaskAlreadyFinalized = "Task is already {0}; no further actions are allowed.";

        #endregion

        #region Nested Types

        /// <summary>
        /// Stable machine-readable error codes.
        /// </summary>
        public static class Codes
        {
            public const string InvalidTitle = "ApprovalTask.InvalidTitle";

            public const string InvalidDocumentNumber = "ApprovalTask.InvalidDocumentNumber";

            public const string InvalidAssignee = "ApprovalTask.InvalidAssignee";

            public const string InvalidActor = "ApprovalTask.InvalidActor";

            public const string ActionRequired = "ApprovalTask.ActionRequired";

            public const string UnknownAction = "ApprovalTask.UnknownAction";

            public const string NotFound = "ApprovalTask.NotFound";

            public const string NotAssignee = "ApprovalTask.NotAssignee";

            public const string InvalidTransition = "ApprovalTask.InvalidTransition";

            public const string RejectCommentRequired = "ApprovalTask.RejectCommentRequired";

            public const string AlreadyFinalized = "ApprovalTask.AlreadyFinalized";
        }

        #endregion
    }
}
```

- [ ] **Step 4: Raise the title limit**

In `EdocsTestTask.Core/Constants/ValidationConstants.cs` replace

```csharp
        public const int TitleMaxLength = 30;
```

with

```csharp
        /// <summary>
        /// Raised from the 30-character default: approval task titles are document names.
        /// </summary>
        public const int TitleMaxLength = 200;
```

- [ ] **Step 5: Create `TextValidationHelper`**

Create `EdocsTestTask.Core/Helpers/TextValidationHelper.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using EdocsTestTask.Core.Constants;

namespace EdocsTestTask.Core.Helpers
{
    /// <summary>
    /// Reusable checks for required text fields. Each returns false with a client-facing message.
    /// </summary>
    public static class TextValidationHelper
    {
        #region Methods

        /// <summary>
        /// Title must not be null or whitespace, and its trimmed length must be within
        /// <see cref="ValidationConstants.TitleMinLength"/>..<see cref="ValidationConstants.TitleMaxLength"/>.
        /// </summary>
        public static bool IsTitleValid(string? title, [NotNullWhen(false)] out string? error)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                error = ApprovalTaskMessages.TitleRequired;
                return false;
            }

            var length = title.Trim().Length;
            if (length < ValidationConstants.TitleMinLength || length > ValidationConstants.TitleMaxLength)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    ApprovalTaskMessages.TitleLength,
                    ValidationConstants.TitleMinLength,
                    ValidationConstants.TitleMaxLength);
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Value must not be null, empty or whitespace.
        /// </summary>
        public static bool IsRequiredTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

            if (string.IsNullOrWhiteSpace(value))
            {
                error = string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.FieldRequired, fieldName);
                return false;
            }

            error = null;
            return true;
        }

        #endregion
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~TextValidationHelperTests"`
Expected: PASS.

Then run the full suite: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj` — Expected: PASS (existing tests use their own literals, not `TitleMaxLength`).

- [ ] **Step 7: Commit**

```bash
git add EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs EdocsTestTask.Core/Constants/ValidationConstants.cs EdocsTestTask.Core/Helpers/TextValidationHelper.cs EdocsTestTask.Tests/Core/TextValidationHelperTests.cs
git commit -m "feat(core): add approval task messages and TextValidationHelper"
```

---

### Task 3: Domain model — enums, `ApprovalTask`, `ApprovalHistoryEntry`

**Files:**
- Create: `EdocsTestTask.Core/Enums/ApprovalStatus.cs`
- Create: `EdocsTestTask.Core/Enums/ApprovalAction.cs`
- Create: `EdocsTestTask.Core/Entities/ApprovalHistoryEntry.cs`
- Create: `EdocsTestTask.Core/Entities/ApprovalTask.cs`
- Delete: `EdocsTestTask.Core/Entities/.gitkeep`
- Create (test data, reused by Tasks 4–7): `EdocsTestTask.Tests/TestData/ApprovalTaskTestData.cs`
- Test: `EdocsTestTask.Tests/Core/ApprovalTaskTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `enum ApprovalStatus { Assigned = 1, InProgress = 2, Approved = 3, Rejected = 4 }` (namespace `EdocsTestTask.Core.Enums`)
  - `enum ApprovalAction { Start = 1, Approve = 2, Reject = 3 }`
  - `sealed record ApprovalHistoryEntry(ApprovalAction Action, string ActorId, string? Comment, DateTime AtUtc)` (namespace `EdocsTestTask.Core.Entities`)
  - `sealed class ApprovalTask` — `Guid Id { get; init; }`, `required string DocumentNumber { get; init; }`, `required string Title { get; init; }`, `required string AssigneeId { get; init; }`, `ApprovalStatus Status { get; set; }`, `DateTime CreatedAtUtc { get; init; }`, `DateTime UpdatedAtUtc { get; set; }`, `List<ApprovalHistoryEntry> History { get; init; } = []`, `ApprovalTask Clone()`.
  - Tests: `ApprovalTaskTestData` — `const string AssigneeId = "approver-1"`, `const string OtherUserId = "approver-2"`, `static readonly DateTime CreatedAtUtc`, `static ApprovalTask Create(ApprovalStatus status = ApprovalStatus.Assigned)`.

**Acceptance Criteria:**
- `Clone()` copies every property; the copy's `History` is a different list with equal entries.
- Changing the clone's `Status`, `UpdatedAtUtc` or `History` does not change the original.

- [ ] **Step 1: Write the test data helper and failing tests**

Create `EdocsTestTask.Tests/TestData/ApprovalTaskTestData.cs`:

```csharp
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Tests.TestData
{
    /// <summary>
    /// Builds approval tasks in a given status without going through the service.
    /// </summary>
    public static class ApprovalTaskTestData
    {
        #region Constants

        public const string AssigneeId = "approver-1";

        public const string OtherUserId = "approver-2";

        #endregion

        #region Fields

        public static readonly DateTime CreatedAtUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        #endregion

        #region Methods

        public static ApprovalTask Create(ApprovalStatus status = ApprovalStatus.Assigned) => new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "DOC-001",
            Title = "Supply contract",
            AssigneeId = AssigneeId,
            Status = status,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
            History = []
        };

        #endregion
    }
}
```

Create `EdocsTestTask.Tests/Core/ApprovalTaskTests.cs`:

```csharp
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Core
{
    public class ApprovalTaskTests
    {
        #region Tests

        [Fact]
        public void Clone_CopiesAllProperties()
        {
            var original = ApprovalTaskTestData.Create(ApprovalStatus.InProgress);
            original.History.Add(new ApprovalHistoryEntry(ApprovalAction.Start, "approver-1", null, original.CreatedAtUtc));

            var clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.DocumentNumber, clone.DocumentNumber);
            Assert.Equal(original.Title, clone.Title);
            Assert.Equal(original.AssigneeId, clone.AssigneeId);
            Assert.Equal(original.Status, clone.Status);
            Assert.Equal(original.CreatedAtUtc, clone.CreatedAtUtc);
            Assert.Equal(original.UpdatedAtUtc, clone.UpdatedAtUtc);
            Assert.NotSame(original.History, clone.History);
            Assert.Equal(original.History, clone.History);
        }

        [Fact]
        public void Clone_MutatingCopy_LeavesOriginalUntouched()
        {
            var original = ApprovalTaskTestData.Create();

            var clone = original.Clone();
            clone.Status = ApprovalStatus.InProgress;
            clone.UpdatedAtUtc = clone.UpdatedAtUtc.AddMinutes(1);
            clone.History.Add(new ApprovalHistoryEntry(ApprovalAction.Start, "approver-1", null, clone.UpdatedAtUtc));

            Assert.Equal(ApprovalStatus.Assigned, original.Status);
            Assert.Equal(ApprovalTaskTestData.CreatedAtUtc, original.UpdatedAtUtc);
            Assert.Empty(original.History);
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskTests"`
Expected: build FAILS — `CS0234` namespaces `EdocsTestTask.Core.Entities` / `EdocsTestTask.Core.Enums` not found.

- [ ] **Step 3: Create the enums**

Create `EdocsTestTask.Core/Enums/ApprovalStatus.cs`:

```csharp
namespace EdocsTestTask.Core.Enums
{
    /// <summary>
    /// Lifecycle status of an approval task. Approved and Rejected are final.
    /// </summary>
    public enum ApprovalStatus
    {
        Assigned = 1,
        InProgress = 2,
        Approved = 3,
        Rejected = 4
    }
}
```

Create `EdocsTestTask.Core/Enums/ApprovalAction.cs`:

```csharp
namespace EdocsTestTask.Core.Enums
{
    /// <summary>
    /// Action the assignee performs on an approval task.
    /// </summary>
    public enum ApprovalAction
    {
        Start = 1,
        Approve = 2,
        Reject = 3
    }
}
```

- [ ] **Step 4: Create the entities**

Create `EdocsTestTask.Core/Entities/ApprovalHistoryEntry.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Core.Entities
{
    /// <summary>
    /// One successful action on an approval task. Immutable.
    /// </summary>
    public sealed record ApprovalHistoryEntry(ApprovalAction Action, string ActorId, string? Comment, DateTime AtUtc);
}
```

Create `EdocsTestTask.Core/Entities/ApprovalTask.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Core.Entities
{
    /// <summary>
    /// A document waiting for a decision by its assignee. Behaviour per status lives in the state classes.
    /// </summary>
    public sealed class ApprovalTask
    {
        #region Properties

        public Guid Id { get; init; }

        public required string DocumentNumber { get; init; }

        public required string Title { get; init; }

        public required string AssigneeId { get; init; }

        public ApprovalStatus Status { get; set; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; set; }

        public List<ApprovalHistoryEntry> History { get; init; } = [];

        #endregion

        #region Methods

        /// <summary>
        /// Returns an independent copy. History entries are immutable, so copying the list is enough.
        /// </summary>
        public ApprovalTask Clone() => new()
        {
            Id = Id,
            DocumentNumber = DocumentNumber,
            Title = Title,
            AssigneeId = AssigneeId,
            Status = Status,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
            History = [.. History]
        };

        #endregion
    }
}
```

Delete the placeholder: `git rm EdocsTestTask.Core/Entities/.gitkeep`

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EdocsTestTask.Core/Enums EdocsTestTask.Core/Entities EdocsTestTask.Tests/TestData/ApprovalTaskTestData.cs EdocsTestTask.Tests/Core/ApprovalTaskTests.cs
git commit -m "feat(core): add ApprovalTask entity, history entry and enums"
```

---

### Task 4: In-memory repository

**Files:**
- Create: `EdocsTestTask.Core/Interfaces/Repositories/IApprovalTaskRepository.cs`
- Create: `EdocsTestTask.Infrastructure/Repositories/InMemoryApprovalTaskRepository.cs`
- Delete: `EdocsTestTask.Core/Interfaces/Repositories/.gitkeep`, `EdocsTestTask.Infrastructure/Repositories/.gitkeep`
- Test: `EdocsTestTask.Tests/Infrastructure/InMemoryApprovalTaskRepositoryTests.cs`

**Interfaces:**
- Consumes (Task 3): `ApprovalTask`, `ApprovalTask.Clone()`, `ApprovalTaskTestData.Create`.
- Produces: `IApprovalTaskRepository` (namespace `EdocsTestTask.Core.Interfaces.Repositories`) with `ApprovalTask? GetById(Guid id)`, `void Add(ApprovalTask task)`, `void Update(ApprovalTask task)`; `InMemoryApprovalTaskRepository : IApprovalTaskRepository` (namespace `EdocsTestTask.Infrastructure.Repositories`).

**Acceptance Criteria:**
- `GetById` returns null for an unknown id, otherwise a copy (never the stored instance).
- Mutating the object passed to `Add`/`Update`, or the object returned by `GetById`, never changes stored state.
- `Add` with an existing id and `Update` with an unknown id throw `InvalidOperationException`; null argument throws `ArgumentNullException`.
- `Update` replaces the stored task.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Infrastructure/InMemoryApprovalTaskRepositoryTests.cs`:

```csharp
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure
{
    public class InMemoryApprovalTaskRepositoryTests
    {
        #region Fields

        private readonly InMemoryApprovalTaskRepository _repository = new();

        #endregion

        #region Tests

        [Fact]
        public void GetById_Unknown_ReturnsNull()
        {
            Assert.Null(_repository.GetById(Guid.NewGuid()));
        }

        [Fact]
        public void Add_ThenGetById_ReturnsEqualCopy()
        {
            var task = ApprovalTaskTestData.Create();

            _repository.Add(task);
            var stored = _repository.GetById(task.Id);

            Assert.NotNull(stored);
            Assert.NotSame(task, stored);
            Assert.Equal(task.Title, stored.Title);
            Assert.Equal(task.Status, stored.Status);
        }

        [Fact]
        public void Add_MutatingSourceAfterAdd_DoesNotChangeStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            task.Status = ApprovalStatus.Approved;

            Assert.Equal(ApprovalStatus.Assigned, _repository.GetById(task.Id)!.Status);
        }

        [Fact]
        public void GetById_MutatingReturnedTask_DoesNotChangeStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            var copy = _repository.GetById(task.Id)!;
            copy.Status = ApprovalStatus.Rejected;
            copy.History.Add(new(ApprovalAction.Reject, "approver-1", "x", DateTime.UtcNow));

            var stored = _repository.GetById(task.Id)!;
            Assert.Equal(ApprovalStatus.Assigned, stored.Status);
            Assert.Empty(stored.History);
        }

        [Fact]
        public void Add_DuplicateId_Throws()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            Assert.Throws<InvalidOperationException>(() => _repository.Add(task));
        }

        [Fact]
        public void Update_ReplacesStoredTask()
        {
            var task = ApprovalTaskTestData.Create();
            _repository.Add(task);

            var changed = _repository.GetById(task.Id)!;
            changed.Status = ApprovalStatus.InProgress;
            _repository.Update(changed);

            Assert.Equal(ApprovalStatus.InProgress, _repository.GetById(task.Id)!.Status);
        }

        [Fact]
        public void Update_UnknownId_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _repository.Update(ApprovalTaskTestData.Create()));
        }

        [Fact]
        public void AddAndUpdate_Null_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _repository.Add(null!));
            Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~InMemoryApprovalTaskRepositoryTests"`
Expected: build FAILS — `CS0234` namespace `EdocsTestTask.Infrastructure.Repositories` has no `InMemoryApprovalTaskRepository`.

- [ ] **Step 3: Create the interface**

Create `EdocsTestTask.Core/Interfaces/Repositories/IApprovalTaskRepository.cs`:

```csharp
using EdocsTestTask.Core.Entities;

namespace EdocsTestTask.Core.Interfaces.Repositories
{
    /// <summary>
    /// Storage for approval tasks. Implementations store and return copies,
    /// so callers never hold a reference to stored state.
    /// </summary>
    public interface IApprovalTaskRepository
    {
        #region Methods

        /// <summary>
        /// Returns a copy of the task, or null when it does not exist.
        /// </summary>
        ApprovalTask? GetById(Guid id);

        /// <exception cref="InvalidOperationException">A task with the same id already exists.</exception>
        void Add(ApprovalTask task);

        /// <summary>
        /// Replaces the stored task with a copy of <paramref name="task"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">No task with this id exists.</exception>
        void Update(ApprovalTask task);

        #endregion
    }
}
```

- [ ] **Step 4: Create the implementation**

Create `EdocsTestTask.Infrastructure/Repositories/InMemoryApprovalTaskRepository.cs`:

```csharp
using System.Collections.Concurrent;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Interfaces.Repositories;

namespace EdocsTestTask.Infrastructure.Repositories
{
    /// <summary>
    /// Process-local storage. Register as a singleton; data is lost on restart.
    /// </summary>
    public sealed class InMemoryApprovalTaskRepository : IApprovalTaskRepository
    {
        #region Fields

        private readonly ConcurrentDictionary<Guid, ApprovalTask> _tasks = new();

        #endregion

        #region Methods

        public ApprovalTask? GetById(Guid id) =>
            _tasks.TryGetValue(id, out var task) ? task.Clone() : null;

        public void Add(ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            if (!_tasks.TryAdd(task.Id, task.Clone()))
            {
                throw new InvalidOperationException($"Approval task '{task.Id}' already exists.");
            }
        }

        public void Update(ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            // Tasks are never deleted, so an id seen here cannot disappear between the check and the write.
            if (!_tasks.ContainsKey(task.Id))
            {
                throw new InvalidOperationException($"Approval task '{task.Id}' does not exist.");
            }

            _tasks[task.Id] = task.Clone();
        }

        #endregion
    }
}
```

Delete placeholders: `git rm EdocsTestTask.Core/Interfaces/Repositories/.gitkeep EdocsTestTask.Infrastructure/Repositories/.gitkeep`

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~InMemoryApprovalTaskRepositoryTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add EdocsTestTask.Core/Interfaces/Repositories EdocsTestTask.Infrastructure/Repositories EdocsTestTask.Tests/Infrastructure/InMemoryApprovalTaskRepositoryTests.cs
git commit -m "feat(infrastructure): add in-memory approval task repository"
```

---

### Task 5: State pattern — states and factory

**Files (all in `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/`, namespace `EdocsTestTask.Infrastructure.Services.ApprovalTasks.States`):**
- Create: `ApprovalTaskStateContext.cs`, `IApprovalTaskState.cs`, `ApprovalTaskStateBase.cs`, `FinalApprovalTaskState.cs`, `AssignedState.cs`, `InProgressState.cs`, `ApprovedState.cs`, `RejectedState.cs`, `ApprovalTaskStateFactory.cs`
- Test: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs`

**Interfaces:**
- Consumes (Tasks 1–3): `ValidationResult.AddError(Error)`, `Error.Conflict/Validation`, `ApprovalTaskMessages` + `Codes`, `ApprovalTask`, `ApprovalHistoryEntry`, `ApprovalStatus`, `ApprovalAction`, `ApprovalTaskTestData`.
- Produces:
  - `sealed record ApprovalTaskStateContext(ApprovalAction Action, string ActorId, string? Comment, DateTime NowUtc)`
  - `interface IApprovalTaskState { ApprovalStatus Status { get; } ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context); void Apply(ApprovalTask task, ApprovalTaskStateContext context); }`
  - `sealed class ApprovalTaskStateFactory { IApprovalTaskState Get(ApprovalStatus status); }` (parameterless constructor)

**Acceptance Criteria:**
- Validation matrix:

  | Status | Start | Approve | Reject (non-empty comment) | Reject (null/empty/whitespace comment) |
  |---|---|---|---|---|
  | Assigned | valid → InProgress | Conflict `InvalidTransition` | Conflict `InvalidTransition` | Conflict `InvalidTransition` |
  | InProgress | Conflict `InvalidTransition` | valid → Approved | valid → Rejected | Validation `RejectCommentRequired` |
  | Approved / Rejected | Conflict `AlreadyFinalized` | Conflict `AlreadyFinalized` | Conflict `AlreadyFinalized` | Conflict `AlreadyFinalized` |

- `Validate` returns exactly one error on failure and never modifies the task.
- `Apply` sets the next status, `UpdatedAtUtc = context.NowUtc`, and appends one `ApprovalHistoryEntry(action, actorId, trimmed comment or null when blank, NowUtc)`.
- `Apply` throws `InvalidOperationException` on a final state, on an action the state does not allow, or when `task.Status` differs from the state's `Status`; in each case the task is unchanged.
- `ApprovalTaskStateFactory.Get` returns the state whose `Status` equals the argument; an undefined status throws `InvalidOperationException`.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs`:

```csharp
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskStatesTests
    {
        #region Fields

        private static readonly DateTime NowUtc = ApprovalTaskTestData.CreatedAtUtc.AddMinutes(5);

        private readonly ApprovalTaskStateFactory _factory = new();

        #endregion

        #region Helpers

        private static ApprovalTaskStateContext Context(ApprovalAction action, string? comment = null) =>
            new(action, ApprovalTaskTestData.AssigneeId, comment, NowUtc);

        #endregion

        #region Tests

        [Theory]
        [InlineData(ApprovalStatus.Assigned)]
        [InlineData(ApprovalStatus.InProgress)]
        [InlineData(ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.Rejected)]
        public void Factory_ReturnsStateForStatus(ApprovalStatus status)
        {
            Assert.Equal(status, _factory.Get(status).Status);
        }

        [Fact]
        public void Factory_UndefinedStatus_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _factory.Get((ApprovalStatus)99));
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Start, null, ApprovalStatus.InProgress)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Approve, null, ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "Missing signature", ApprovalStatus.Rejected)]
        public void ValidateAndApply_AllowedAction_MovesToNextStatus(
            ApprovalStatus from, ApprovalAction action, string? comment, ApprovalStatus expected)
        {
            var task = ApprovalTaskTestData.Create(from);
            var state = _factory.Get(from);
            var context = Context(action, comment);

            Assert.True(state.Validate(task, context).IsValid);
            state.Apply(task, context);

            Assert.Equal(expected, task.Status);
            Assert.Equal(NowUtc, task.UpdatedAtUtc);
            Assert.Equal(new ApprovalHistoryEntry(action, ApprovalTaskTestData.AssigneeId, comment, NowUtc), Assert.Single(task.History));
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, null, ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "   ", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        public void Validate_NotAllowed_ReturnsSingleErrorAndKeepsTask(
            ApprovalStatus from, ApprovalAction action, string? comment, ErrorType expectedType, string expectedCode)
        {
            var task = ApprovalTaskTestData.Create(from);
            var before = task.Clone();

            var validation = _factory.Get(from).Validate(task, Context(action, comment));

            var error = Assert.Single(validation.Errors);
            Assert.Equal(expectedType, error.Type);
            Assert.Equal(expectedCode, error.Code);
            Assert.Equal(before.Status, task.Status);
            Assert.Equal(before.UpdatedAtUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
        }

        [Fact]
        public void Validate_InvalidTransition_MessageNamesActionAndStatus()
        {
            var task = ApprovalTaskTestData.Create(ApprovalStatus.Assigned);

            var error = _factory.Get(ApprovalStatus.Assigned).Validate(task, Context(ApprovalAction.Approve)).Errors[0];

            Assert.Equal("Action 'Approve' is not allowed when task is in status 'Assigned'.", error.Message);
        }

        [Fact]
        public void Validate_Finalized_MessageNamesStatus()
        {
            var task = ApprovalTaskTestData.Create(ApprovalStatus.Rejected);

            var error = _factory.Get(ApprovalStatus.Rejected).Validate(task, Context(ApprovalAction.Start)).Errors[0];

            Assert.Equal("Task is already Rejected; no further actions are allowed.", error.Message);
        }

        [Theory]
        [InlineData("  Looks good  ", "Looks good")]
        [InlineData("   ", null)]
        [InlineData(null, null)]
        public void Apply_StoresTrimmedCommentOrNull(string? comment, string? expected)
        {
            var task = ApprovalTaskTestData.Create(ApprovalStatus.InProgress);

            _factory.Get(ApprovalStatus.InProgress).Apply(task, Context(ApprovalAction.Approve, comment));

            Assert.Equal(expected, Assert.Single(task.History).Comment);
        }

        [Theory]
        [InlineData(ApprovalStatus.Approved, ApprovalStatus.Approved, ApprovalAction.Approve)]
        [InlineData(ApprovalStatus.Rejected, ApprovalStatus.Rejected, ApprovalAction.Start)]
        [InlineData(ApprovalStatus.Assigned, ApprovalStatus.Assigned, ApprovalAction.Approve)]
        [InlineData(ApprovalStatus.Assigned, ApprovalStatus.InProgress, ApprovalAction.Start)]
        public void Apply_InvalidCall_ThrowsAndKeepsTask(ApprovalStatus stateStatus, ApprovalStatus taskStatus, ApprovalAction action)
        {
            var task = ApprovalTaskTestData.Create(taskStatus);

            Assert.Throws<InvalidOperationException>(() => _factory.Get(stateStatus).Apply(task, Context(action, "c")));

            Assert.Equal(taskStatus, task.Status);
            Assert.Equal(ApprovalTaskTestData.CreatedAtUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskStatesTests"`
Expected: build FAILS — `CS0234` namespace `EdocsTestTask.Infrastructure.Services.ApprovalTasks.States` not found.

- [ ] **Step 3: Create context and interface**

Create `ApprovalTaskStateContext.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Input of a single action, already validated by the service (known action, non-empty actor).
    /// </summary>
    public sealed record ApprovalTaskStateContext(ApprovalAction Action, string ActorId, string? Comment, DateTime NowUtc);
}
```

Create `IApprovalTaskState.cs`:

```csharp
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Rules for one <see cref="ApprovalStatus"/>: which actions are allowed and where they lead.
    /// Implementations are stateless and shared.
    /// </summary>
    public interface IApprovalTaskState
    {
        #region Properties

        ApprovalStatus Status { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Checks whether the action is allowed in this state. Never modifies <paramref name="task"/>.
        /// </summary>
        ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context);

        /// <summary>
        /// Moves the task to the next status, sets UpdatedAtUtc and appends a history entry.
        /// Call only after <see cref="Validate"/> succeeded.
        /// </summary>
        /// <exception cref="InvalidOperationException">The action is not allowed or the task is in another status.</exception>
        void Apply(ApprovalTask task, ApprovalTaskStateContext context);

        #endregion
    }
}
```

- [ ] **Step 4: Create the base classes**

Create `ApprovalTaskStateBase.cs`:

```csharp
using System.Globalization;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Shared <see cref="Apply"/>: every successful action updates status, UpdatedAtUtc and history together.
    /// </summary>
    public abstract class ApprovalTaskStateBase : IApprovalTaskState
    {
        #region Properties

        public abstract ApprovalStatus Status { get; }

        #endregion

        #region Methods

        public abstract ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context);

        public void Apply(ApprovalTask task, ApprovalTaskStateContext context)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(context);

            if (task.Status != Status)
            {
                throw new InvalidOperationException($"State '{Status}' cannot apply an action to a task in status '{task.Status}'.");
            }

            // Resolve first: it throws for a disallowed action before anything is changed.
            var nextStatus = GetNextStatus(context.Action);

            task.Status = nextStatus;
            task.UpdatedAtUtc = context.NowUtc;
            task.History.Add(new ApprovalHistoryEntry(context.Action, context.ActorId, NormalizeComment(context.Comment), context.NowUtc));
        }

        #endregion

        #region Private Methods

        /// <exception cref="InvalidOperationException">The action is not allowed in this state.</exception>
        protected abstract ApprovalStatus GetNextStatus(ApprovalAction action);

        protected Error InvalidTransitionError(ApprovalAction action) =>
            Error.Conflict(
                ApprovalTaskMessages.Codes.InvalidTransition,
                string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.InvalidTransition, action, Status));

        protected InvalidOperationException NotAllowed(ApprovalAction action) =>
            new($"Action '{action}' is not allowed in state '{Status}'. Validate must be called before Apply.");

        private static string? NormalizeComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        #endregion
    }
}
```

Create `FinalApprovalTaskState.cs`:

```csharp
using System.Globalization;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// A final status: every action is rejected with Conflict.
    /// </summary>
    public abstract class FinalApprovalTaskState : ApprovalTaskStateBase
    {
        #region Methods

        public override ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(context);

            var validation = new ValidationResult();
            validation.AddError(Error.Conflict(
                ApprovalTaskMessages.Codes.AlreadyFinalized,
                string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.TaskAlreadyFinalized, Status)));
            return validation;
        }

        #endregion

        #region Private Methods

        protected override ApprovalStatus GetNextStatus(ApprovalAction action) => throw NotAllowed(action);

        #endregion
    }
}
```

- [ ] **Step 5: Create the four states**

Create `AssignedState.cs`:

```csharp
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Assigned: only Start is allowed and leads to InProgress.
    /// </summary>
    public sealed class AssignedState : ApprovalTaskStateBase
    {
        #region Properties

        public override ApprovalStatus Status => ApprovalStatus.Assigned;

        #endregion

        #region Methods

        public override ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(context);

            var validation = new ValidationResult();
            if (context.Action != ApprovalAction.Start)
            {
                validation.AddError(InvalidTransitionError(context.Action));
            }

            return validation;
        }

        #endregion

        #region Private Methods

        protected override ApprovalStatus GetNextStatus(ApprovalAction action) => action switch
        {
            ApprovalAction.Start => ApprovalStatus.InProgress,
            _ => throw NotAllowed(action)
        };

        #endregion
    }
}
```

Create `InProgressState.cs`:

```csharp
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// InProgress: Approve leads to Approved; Reject (with a non-empty comment) leads to Rejected.
    /// </summary>
    public sealed class InProgressState : ApprovalTaskStateBase
    {
        #region Properties

        public override ApprovalStatus Status => ApprovalStatus.InProgress;

        #endregion

        #region Methods

        public override ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext context)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(context);

            var validation = new ValidationResult();
            if (context.Action == ApprovalAction.Start)
            {
                validation.AddError(InvalidTransitionError(context.Action));
            }
            else if (context.Action == ApprovalAction.Reject && string.IsNullOrWhiteSpace(context.Comment))
            {
                validation.AddError(Error.Validation(
                    ApprovalTaskMessages.Codes.RejectCommentRequired,
                    ApprovalTaskMessages.RejectCommentRequired));
            }

            return validation;
        }

        #endregion

        #region Private Methods

        protected override ApprovalStatus GetNextStatus(ApprovalAction action) => action switch
        {
            ApprovalAction.Approve => ApprovalStatus.Approved,
            ApprovalAction.Reject => ApprovalStatus.Rejected,
            _ => throw NotAllowed(action)
        };

        #endregion
    }
}
```

Create `ApprovedState.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Approved: final, no actions allowed.
    /// </summary>
    public sealed class ApprovedState : FinalApprovalTaskState
    {
        #region Properties

        public override ApprovalStatus Status => ApprovalStatus.Approved;

        #endregion
    }
}
```

Create `RejectedState.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Rejected: final, no actions allowed.
    /// </summary>
    public sealed class RejectedState : FinalApprovalTaskState
    {
        #region Properties

        public override ApprovalStatus Status => ApprovalStatus.Rejected;

        #endregion
    }
}
```

- [ ] **Step 6: Create the factory**

Create `ApprovalTaskStateFactory.cs`:

```csharp
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks.States
{
    /// <summary>
    /// Maps a status to its stateless <see cref="IApprovalTaskState"/>. Safe to share as a singleton.
    /// </summary>
    public sealed class ApprovalTaskStateFactory
    {
        #region Fields

        private readonly IReadOnlyDictionary<ApprovalStatus, IApprovalTaskState> _states;

        #endregion

        #region Constructors

        public ApprovalTaskStateFactory()
        {
            IApprovalTaskState[] states = [new AssignedState(), new InProgressState(), new ApprovedState(), new RejectedState()];
            _states = states.ToDictionary(state => state.Status);
        }

        #endregion

        #region Methods

        /// <exception cref="InvalidOperationException">No state exists for <paramref name="status"/> (a bug, not a client error).</exception>
        public IApprovalTaskState Get(ApprovalStatus status) =>
            _states.TryGetValue(status, out var state)
                ? state
                : throw new InvalidOperationException($"No state is registered for status '{status}'.");

        #endregion
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskStatesTests"`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add EdocsTestTask.Infrastructure/Services/ApprovalTasks/States EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs
git commit -m "feat(infrastructure): add approval task states and state factory"
```

---

### Task 6: Orchestrator service — `ApprovalTaskService`

**Files:**
- Create: `EdocsTestTask.Core/Commands/CreateApprovalTaskCommand.cs`
- Create: `EdocsTestTask.Core/Commands/ExecuteActionCommand.cs`
- Create: `EdocsTestTask.Core/Interfaces/Services/IApprovalTaskService.cs`
- Delete: `EdocsTestTask.Core/Interfaces/Services/.gitkeep`, `EdocsTestTask.Infrastructure/Services/.gitkeep`
- Create: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs`
- Create: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`
- Modify: `EdocsTestTask.Infrastructure/DependencyInjection.cs`
- Create: `EdocsTestTask.Tests/TestData/TestTimeProvider.cs`
- Test: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs`

**Interfaces:**
- Consumes (Tasks 1–5): `Result<T>`, `Error.*`, `ValidationResult.ToFailure<T>()`, `ValidationResult.AddError(string message, string code)`, `TextValidationHelper.*`, `ApprovalTaskMessages.*`, `ApprovalTask`, `ApprovalStatus`, `ApprovalAction`, `IApprovalTaskRepository`, `InMemoryApprovalTaskRepository`, `ApprovalTaskStateFactory.Get`, `IApprovalTaskState.Validate/Apply`, `ApprovalTaskStateContext`, `ApprovalTaskTestData`.
- Produces:
  - `sealed record CreateApprovalTaskCommand(string? DocumentNumber, string? Title, string? AssigneeId)` (namespace `EdocsTestTask.Core.Commands`)
  - `sealed record ExecuteActionCommand(string? ActorId, string? Action, string? Comment)`
  - `IApprovalTaskService` (namespace `EdocsTestTask.Core.Interfaces.Services`): `Result<ApprovalTask> Create(CreateApprovalTaskCommand command)`, `Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand command)`, `Result<ApprovalTask> GetById(Guid id)`
  - `ApprovalTaskService(IApprovalTaskRepository repository, ApprovalTaskStateFactory stateFactory, TimeProvider timeProvider)`
  - `ApprovalTaskServiceHelper.ValidateCreate(CreateApprovalTaskCommand)`, `ValidateAction(ExecuteActionCommand)` → `ValidationResult`; `TryParseAction(string? value, out ApprovalAction action)` → `bool`
  - Tests: `TestTimeProvider(DateTimeOffset start)` with `GetUtcNow()` and `Advance(TimeSpan)`.

**Acceptance Criteria:**
- Create: valid command → success; task `Status = Assigned`, `CreatedAtUtc == UpdatedAtUtc ==` time provider's now (UTC kind), empty history, trimmed fields, readable via `GetById`. Invalid title (null/blank/2/201 chars), blank `DocumentNumber` or blank `AssigneeId` → `Validation` with codes `InvalidTitle`/`InvalidDocumentNumber`/`InvalidAssignee`; all failing fields are reported together.
- Full paths Start → Approve and Start → Reject (with comment) succeed; history has two entries with correct action, actor, trimmed comment and timestamps; `CreatedAtUtc` unchanged; `UpdatedAtUtc` equals last action time; `GetById` returns the same state.
- Not-allowed transitions (including any action after Approved/Rejected) → `Conflict`; another actor → `Forbidden` `NotAssignee`; Reject with null/empty/whitespace comment → `Validation` `RejectCommentRequired`; missing/unknown action (`null`, `""`, `"Delete"`, `"1"`, `"Start,Approve"`) → `Validation` `ActionRequired`/`UnknownAction`; unknown task id → `NotFound`; command validation runs before the 404 check.
- After every failure the stored status, `UpdatedAtUtc` and history are unchanged.
- Action names are accepted case-insensitively (`"start"` works).
- `AddInfrastructure` registers `IApprovalTaskRepository`, `ApprovalTaskStateFactory`, `IApprovalTaskService` as singletons and `TimeProvider.System` via `TryAddSingleton`.

- [ ] **Step 1: Write `TestTimeProvider`**

Create `EdocsTestTask.Tests/TestData/TestTimeProvider.cs`:

```csharp
namespace EdocsTestTask.Tests.TestData
{
    /// <summary>
    /// Deterministic clock for tests. Only advances when told to.
    /// </summary>
    public sealed class TestTimeProvider : TimeProvider
    {
        #region Fields

        private DateTimeOffset _utcNow;

        #endregion

        #region Constructors

        public TestTimeProvider(DateTimeOffset start)
        {
            _utcNow = start;
        }

        #endregion

        #region Methods

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);

        #endregion
    }
}
```

- [ ] **Step 2: Write the failing service tests**

Create `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs`:

```csharp
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceTests
    {
        #region Constants

        private const string Assignee = ApprovalTaskTestData.AssigneeId;

        #endregion

        #region Fields

        private static readonly DateTime StartUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        private readonly TestTimeProvider _time = new(new DateTimeOffset(StartUtc));

        private readonly InMemoryApprovalTaskRepository _repository = new();

        private readonly ApprovalTaskService _service;

        #endregion

        #region Constructors

        public ApprovalTaskServiceTests()
        {
            _service = new ApprovalTaskService(_repository, new ApprovalTaskStateFactory(), _time);
        }

        #endregion

        #region Helpers

        private ApprovalTask CreateTask() =>
            _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", Assignee)).Value;

        /// <summary>
        /// Advances the clock by one minute before each action, so every change gets a distinct timestamp.
        /// </summary>
        private Result<ApprovalTask> Act(Guid id, string? action, string? comment = null, string? actor = Assignee)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            return _service.ExecuteAction(id, new ExecuteActionCommand(actor, action, comment));
        }

        private ApprovalTask MoveTo(ApprovalStatus status)
        {
            var task = CreateTask();
            if (status != ApprovalStatus.Assigned)
            {
                Assert.True(Act(task.Id, "Start").IsSuccess);
            }

            if (status == ApprovalStatus.Approved)
            {
                Assert.True(Act(task.Id, "Approve").IsSuccess);
            }
            else if (status == ApprovalStatus.Rejected)
            {
                Assert.True(Act(task.Id, "Reject", "Missing signature").IsSuccess);
            }

            return _repository.GetById(task.Id)!;
        }

        private void AssertUnchanged(ApprovalTask before)
        {
            var after = _repository.GetById(before.Id)!;
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.UpdatedAtUtc, after.UpdatedAtUtc);
            Assert.Equal(before.History, after.History);
        }

        private static void AssertError(Result result, ErrorType type, string code)
        {
            Assert.True(result.IsFailure);
            Assert.Equal(type, result.Error!.Type);
            Assert.Equal(code, result.Error.Code);
        }

        #endregion

        #region Tests

        [Fact]
        public void Create_ValidCommand_ReturnsAssignedTaskWithEqualUtcDatesAndEmptyHistory()
        {
            var result = _service.Create(new CreateApprovalTaskCommand("  DOC-001 ", " Supply contract ", " approver-1 "));

            Assert.True(result.IsSuccess);
            var task = result.Value;
            Assert.NotEqual(Guid.Empty, task.Id);
            Assert.Equal("DOC-001", task.DocumentNumber);
            Assert.Equal("Supply contract", task.Title);
            Assert.Equal("approver-1", task.AssigneeId);
            Assert.Equal(ApprovalStatus.Assigned, task.Status);
            Assert.Equal(StartUtc, task.CreatedAtUtc);
            Assert.Equal(DateTimeKind.Utc, task.CreatedAtUtc.Kind);
            Assert.Equal(task.CreatedAtUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
            Assert.Equal(ApprovalStatus.Assigned, _service.GetById(task.Id).Value.Status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("ab")]
        public void Create_InvalidTitle_ReturnsValidationError(string? title)
        {
            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", title, Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public void Create_TooLongTitle_ReturnsValidationError()
        {
            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", new string('a', 201), Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public void Create_AllFieldsMissing_ReportsEveryField()
        {
            var result = _service.Create(new CreateApprovalTaskCommand(" ", null, ""));

            Assert.True(result.IsFailure);
            Assert.Equal(
                new[] { ApprovalTaskMessages.Codes.InvalidTitle, ApprovalTaskMessages.Codes.InvalidDocumentNumber, ApprovalTaskMessages.Codes.InvalidAssignee },
                result.Errors.Select(e => e.Code));
            Assert.All(result.Errors, e => Assert.Equal(ErrorType.Validation, e.Type));
        }

        [Fact]
        public void ExecuteAction_StartThenApprove_CompletesWithHistory()
        {
            var created = CreateTask();

            Assert.True(Act(created.Id, "Start").IsSuccess);
            var result = Act(created.Id, "Approve", "  Looks good  ");

            Assert.True(result.IsSuccess);
            var task = result.Value;
            Assert.Equal(ApprovalStatus.Approved, task.Status);
            Assert.Equal(StartUtc, task.CreatedAtUtc);
            Assert.Equal(StartUtc.AddMinutes(2), task.UpdatedAtUtc);
            Assert.Collection(task.History,
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Start, Assignee, null, StartUtc.AddMinutes(1)), e),
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Approve, Assignee, "Looks good", StartUtc.AddMinutes(2)), e));

            var stored = _service.GetById(created.Id).Value;
            Assert.Equal(task.Status, stored.Status);
            Assert.Equal(task.UpdatedAtUtc, stored.UpdatedAtUtc);
            Assert.Equal(task.History, stored.History);
        }

        [Fact]
        public void ExecuteAction_StartThenReject_CompletesWithHistory()
        {
            var created = CreateTask();

            Assert.True(Act(created.Id, "Start").IsSuccess);
            var result = Act(created.Id, "Reject", "Missing signature");

            Assert.True(result.IsSuccess);
            Assert.Equal(ApprovalStatus.Rejected, result.Value.Status);
            Assert.Collection(result.Value.History,
                e => Assert.Equal(ApprovalAction.Start, e.Action),
                e => Assert.Equal(new ApprovalHistoryEntry(ApprovalAction.Reject, Assignee, "Missing signature", StartUtc.AddMinutes(2)), e));
            Assert.Equal(ApprovalStatus.Rejected, _service.GetById(created.Id).Value.Status);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, "Approve", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, "Reject", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, "Start", ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Approved, "Start", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, "Approve", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, "Reject", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Start", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Approve", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, "Reject", ApprovalTaskMessages.Codes.AlreadyFinalized)]
        public void ExecuteAction_NotAllowedTransition_ReturnsConflictAndKeepsState(ApprovalStatus status, string action, string code)
        {
            var before = MoveTo(status);

            var result = Act(before.Id, action, "Some comment");

            AssertError(result, ErrorType.Conflict, code);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, "Start")]
        [InlineData(ApprovalStatus.InProgress, "Approve")]
        [InlineData(ApprovalStatus.InProgress, "Reject")]
        [InlineData(ApprovalStatus.Approved, "Start")]
        public void ExecuteAction_OtherActor_ReturnsForbiddenAndKeepsState(ApprovalStatus status, string action)
        {
            var before = MoveTo(status);

            var result = Act(before.Id, action, "Some comment", ApprovalTaskTestData.OtherUserId);

            AssertError(result, ErrorType.Forbidden, ApprovalTaskMessages.Codes.NotAssignee);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ExecuteAction_RejectWithoutComment_ReturnsValidationAndKeepsState(string? comment)
        {
            var before = MoveTo(ApprovalStatus.InProgress);

            var result = Act(before.Id, "Reject", comment);

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired);
            AssertUnchanged(before);
        }

        [Theory]
        [InlineData(null, ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("", ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("  ", ApprovalTaskMessages.Codes.ActionRequired)]
        [InlineData("Delete", ApprovalTaskMessages.Codes.UnknownAction)]
        [InlineData("1", ApprovalTaskMessages.Codes.UnknownAction)]
        [InlineData("Start,Approve", ApprovalTaskMessages.Codes.UnknownAction)]
        public void ExecuteAction_MissingOrUnknownAction_ReturnsValidationAndKeepsState(string? action, string code)
        {
            var before = MoveTo(ApprovalStatus.Assigned);

            var result = Act(before.Id, action);

            AssertError(result, ErrorType.Validation, code);
            AssertUnchanged(before);
        }

        [Fact]
        public void ExecuteAction_UnknownAction_MessageNamesAction()
        {
            var task = CreateTask();

            var result = Act(task.Id, "Delete");

            Assert.Equal("Unknown action 'Delete'. Allowed: Start, Approve, Reject.", result.Error!.Message);
        }

        [Fact]
        public void ExecuteAction_ActionNameIsCaseInsensitive()
        {
            var task = CreateTask();

            var result = Act(task.Id, "start");

            Assert.True(result.IsSuccess);
            Assert.Equal(ApprovalStatus.InProgress, result.Value.Status);
        }

        [Fact]
        public void ExecuteAction_BlankActor_ReturnsValidation()
        {
            var before = MoveTo(ApprovalStatus.Assigned);

            var result = Act(before.Id, "Start", actor: " ");

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidActor);
            AssertUnchanged(before);
        }

        [Fact]
        public void ExecuteAction_MissingTask_ReturnsNotFound()
        {
            var result = Act(Guid.NewGuid(), "Start");

            AssertError(result, ErrorType.NotFound, ApprovalTaskMessages.Codes.NotFound);
        }

        [Fact]
        public void ExecuteAction_InvalidCommandOnMissingTask_ReturnsValidationFirst()
        {
            var result = Act(Guid.NewGuid(), "Delete");

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.UnknownAction);
        }

        [Fact]
        public void GetById_MissingTask_ReturnsNotFound()
        {
            var id = Guid.NewGuid();

            var result = _service.GetById(id);

            AssertError(result, ErrorType.NotFound, ApprovalTaskMessages.Codes.NotFound);
            Assert.Equal($"Approval task '{id}' was not found.", result.Error!.Message);
        }

        #endregion
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceTests"`
Expected: build FAILS — `CS0234` namespaces `EdocsTestTask.Core.Commands` / `EdocsTestTask.Infrastructure.Services.ApprovalTasks` have no such types.

- [ ] **Step 4: Create commands and interface**

Create `EdocsTestTask.Core/Commands/CreateApprovalTaskCommand.cs`:

```csharp
namespace EdocsTestTask.Core.Commands
{
    /// <summary>
    /// Input for creating an approval task. Fields are nullable: the service validates them.
    /// </summary>
    public sealed record CreateApprovalTaskCommand(string? DocumentNumber, string? Title, string? AssigneeId);
}
```

Create `EdocsTestTask.Core/Commands/ExecuteActionCommand.cs`:

```csharp
namespace EdocsTestTask.Core.Commands
{
    /// <summary>
    /// Input for an action on an approval task. <see cref="ActorId"/> is the authenticated user, never the request body.
    /// </summary>
    public sealed record ExecuteActionCommand(string? ActorId, string? Action, string? Comment);
}
```

Create `EdocsTestTask.Core/Interfaces/Services/IApprovalTaskService.cs`:

```csharp
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Core.Interfaces.Services
{
    /// <summary>
    /// Creates approval tasks and moves them through their lifecycle.
    /// </summary>
    public interface IApprovalTaskService
    {
        #region Methods

        Result<ApprovalTask> Create(CreateApprovalTaskCommand command);

        Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand command);

        Result<ApprovalTask> GetById(Guid id);

        #endregion
    }
}
```

Delete placeholders: `git rm EdocsTestTask.Core/Interfaces/Services/.gitkeep EdocsTestTask.Infrastructure/Services/.gitkeep`

- [ ] **Step 5: Create the helper**

Create `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs`:

```csharp
using System.Globalization;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Core.Helpers;
using EdocsTestTask.Shared.Validation;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks
{
    /// <summary>
    /// Input validation for <see cref="ApprovalTaskService"/>. State-specific rules live in the state classes.
    /// </summary>
    public static class ApprovalTaskServiceHelper
    {
        #region Methods

        /// <summary>
        /// Title via <see cref="TextValidationHelper.IsTitleValid"/> (3..200 trimmed); DocumentNumber and AssigneeId required.
        /// Reports every failing field.
        /// </summary>
        public static ValidationResult ValidateCreate(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = new ValidationResult();

            if (!TextValidationHelper.IsTitleValid(command.Title, out var titleError))
            {
                validation.AddError(titleError, ApprovalTaskMessages.Codes.InvalidTitle);
            }

            if (!TextValidationHelper.IsRequiredTextValid(command.DocumentNumber, nameof(command.DocumentNumber), out var documentNumberError))
            {
                validation.AddError(documentNumberError, ApprovalTaskMessages.Codes.InvalidDocumentNumber);
            }

            if (!TextValidationHelper.IsRequiredTextValid(command.AssigneeId, nameof(command.AssigneeId), out var assigneeError))
            {
                validation.AddError(assigneeError, ApprovalTaskMessages.Codes.InvalidAssignee);
            }

            return validation;
        }

        /// <summary>
        /// ActorId required; Action required and one of <see cref="ApprovalAction"/> names (case-insensitive).
        /// </summary>
        public static ValidationResult ValidateAction(ExecuteActionCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = new ValidationResult();

            if (!TextValidationHelper.IsRequiredTextValid(command.ActorId, nameof(command.ActorId), out var actorError))
            {
                validation.AddError(actorError, ApprovalTaskMessages.Codes.InvalidActor);
            }

            if (string.IsNullOrWhiteSpace(command.Action))
            {
                validation.AddError(ApprovalTaskMessages.ActionRequired, ApprovalTaskMessages.Codes.ActionRequired);
            }
            else if (!TryParseAction(command.Action, out _))
            {
                validation.AddError(
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.UnknownAction, command.Action.Trim()),
                    ApprovalTaskMessages.Codes.UnknownAction);
            }

            return validation;
        }

        /// <summary>
        /// Matches defined names only. Unlike <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>,
        /// numbers ("1") and flag lists ("Start,Approve") are rejected.
        /// </summary>
        public static bool TryParseAction(string? value, out ApprovalAction action)
        {
            action = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            var name = Enum.GetNames<ApprovalAction>()
                .FirstOrDefault(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));

            return name is not null && Enum.TryParse(name, out action);
        }

        #endregion
    }
}
```

- [ ] **Step 6: Create the service (without locking — Task 7 adds it)**

Create `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`:

```csharp
using System.Globalization;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Core.Interfaces.Repositories;
using EdocsTestTask.Core.Interfaces.Services;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Shared.Validation;
using Helper = EdocsTestTask.Infrastructure.Services.ApprovalTasks.ApprovalTaskServiceHelper;

namespace EdocsTestTask.Infrastructure.Services.ApprovalTasks
{
    /// <summary>
    /// Orchestrates approval task operations: validates the command, then delegates
    /// status rules to the <see cref="IApprovalTaskState"/> of the task's current status.
    /// </summary>
    public sealed class ApprovalTaskService : IApprovalTaskService
    {
        #region Fields

        private readonly IApprovalTaskRepository _repository;

        private readonly ApprovalTaskStateFactory _stateFactory;

        private readonly TimeProvider _timeProvider;

        #endregion

        #region Constructors

        public ApprovalTaskService(IApprovalTaskRepository repository, ApprovalTaskStateFactory stateFactory, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(stateFactory);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _repository = repository;
            _stateFactory = stateFactory;
            _timeProvider = timeProvider;
        }

        #endregion

        #region Methods

        public Result<ApprovalTask> Create(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = Validate(command);
            if (!validation.IsValid)
            {
                return validation.ToFailure<ApprovalTask>();
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var task = new ApprovalTask
            {
                Id = Guid.NewGuid(),
                DocumentNumber = command.DocumentNumber!.Trim(),
                Title = command.Title!.Trim(),
                AssigneeId = command.AssigneeId!.Trim(),
                Status = ApprovalStatus.Assigned,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                History = []
            };

            _repository.Add(task);
            return task;
        }

        public Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = Validate(command);
            if (!validation.IsValid)
            {
                return validation.ToFailure<ApprovalTask>();
            }

            Helper.TryParseAction(command.Action, out var action);
            var actorId = command.ActorId!.Trim();

            return ExecuteValidatedAction(id, action, actorId, command.Comment);
        }

        public Result<ApprovalTask> GetById(Guid id)
        {
            var task = _repository.GetById(id);
            if (task is null)
            {
                return NotFound(id);
            }

            return task;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Orchestrator-level validation of the create command (field rules only).
        /// </summary>
        private static ValidationResult Validate(CreateApprovalTaskCommand command) => Helper.ValidateCreate(command);

        /// <summary>
        /// Orchestrator-level validation of the action command (actor present, action known).
        /// Status-specific rules are checked afterwards by the task's state.
        /// </summary>
        private static ValidationResult Validate(ExecuteActionCommand command) => Helper.ValidateAction(command);

        private Result<ApprovalTask> ExecuteValidatedAction(Guid id, ApprovalAction action, string actorId, string? comment)
        {
            // The repository returns a copy, so a failure below leaves stored data untouched.
            var task = _repository.GetById(id);
            if (task is null)
            {
                return NotFound(id);
            }

            if (!string.Equals(task.AssigneeId, actorId, StringComparison.Ordinal))
            {
                return Error.Forbidden(ApprovalTaskMessages.Codes.NotAssignee, ApprovalTaskMessages.NotAssignee);
            }

            var state = _stateFactory.Get(task.Status);
            var context = new ApprovalTaskStateContext(action, actorId, comment, _timeProvider.GetUtcNow().UtcDateTime);

            var stateValidation = state.Validate(task, context);
            if (!stateValidation.IsValid)
            {
                return stateValidation.ToFailure<ApprovalTask>();
            }

            state.Apply(task, context);
            _repository.Update(task);
            return task;
        }

        private static Error NotFound(Guid id) =>
            Error.NotFound(
                ApprovalTaskMessages.Codes.NotFound,
                string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.TaskNotFound, id));

        #endregion
    }
}
```

- [ ] **Step 7: Register in DI**

Replace the body of `AddInfrastructure` in `EdocsTestTask.Infrastructure/DependencyInjection.cs` so the file reads:

```csharp
using EdocsTestTask.Core.Interfaces.Repositories;
using EdocsTestTask.Core.Interfaces.Services;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EdocsTestTask.Infrastructure
{
    public static class DependencyInjection
    {
        #region Methods

        /// <summary>
        /// Registers Infrastructure implementations of Core service and repository interfaces.
        /// </summary>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.TryAddSingleton(TimeProvider.System);

            // Singletons: the in-memory data and the service's per-task locks must be shared by all requests.
            services.AddSingleton<IApprovalTaskRepository, InMemoryApprovalTaskRepository>();
            services.AddSingleton<ApprovalTaskStateFactory>();
            services.AddSingleton<IApprovalTaskService, ApprovalTaskService>();

            return services;
        }

        #endregion
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceTests"`
Expected: PASS.

Then the full suite: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj` — Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add EdocsTestTask.Core/Commands EdocsTestTask.Core/Interfaces/Services EdocsTestTask.Infrastructure/Services EdocsTestTask.Infrastructure/DependencyInjection.cs EdocsTestTask.Tests/TestData/TestTimeProvider.cs EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs
git commit -m "feat(infrastructure): add ApprovalTaskService orchestrator with state-based actions"
```

---

### Task 7: Per-task locking for concurrent actions

**Files:**
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`
- Test: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceConcurrencyTests.cs`

**Interfaces:**
- Consumes (Task 6): `ApprovalTaskService`, `CreateApprovalTaskCommand`, `ExecuteActionCommand`, `InMemoryApprovalTaskRepository`, `ApprovalTaskStateFactory`, `TestTimeProvider`, `ApprovalTaskTestData`.
- Produces: no new public API; `ExecuteAction` becomes safe for concurrent calls on the same task.

**Acceptance Criteria:**
- Two concurrent final actions (Approve+Reject, Approve+Approve, Reject+Reject) on the same `InProgress` task, repeated 200 times: exactly one succeeds, the other fails with `Conflict` `ApprovalTask.AlreadyFinalized`; stored history has exactly two entries (Start + one decision) and the stored status matches the winner.
- Locks are created only for ids that exist in the repository (unknown ids return 404 without touching the lock map).

- [ ] **Step 1: Write the failing concurrency test**

Create `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceConcurrencyTests.cs`:

```csharp
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Repositories;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;
using EdocsTestTask.Shared.Results;
using EdocsTestTask.Tests.TestData;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceConcurrencyTests
    {
        #region Constants

        private const int Iterations = 200;

        private const string Assignee = ApprovalTaskTestData.AssigneeId;

        #endregion

        #region Fields

        private readonly InMemoryApprovalTaskRepository _repository = new();

        private readonly ApprovalTaskService _service;

        #endregion

        #region Constructors

        public ApprovalTaskServiceConcurrencyTests()
        {
            var time = new TestTimeProvider(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
            _service = new ApprovalTaskService(_repository, new ApprovalTaskStateFactory(), time);
        }

        #endregion

        #region Tests

        [Theory]
        [InlineData("Approve", "Reject")]
        [InlineData("Approve", "Approve")]
        [InlineData("Reject", "Reject")]
        public async Task ExecuteAction_TwoConcurrentFinalDecisions_ExactlyOneSucceeds(string first, string second)
        {
            for (var i = 0; i < Iterations; i++)
            {
                var task = _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", Assignee)).Value;
                Assert.True(_service.ExecuteAction(task.Id, new ExecuteActionCommand(Assignee, "Start", null)).IsSuccess);

                using var barrier = new Barrier(2);
                Result<ApprovalTask> Decide(string action)
                {
                    barrier.SignalAndWait();
                    return _service.ExecuteAction(task.Id, new ExecuteActionCommand(Assignee, action, "Decision comment"));
                }

                var results = await Task.WhenAll(Task.Run(() => Decide(first)), Task.Run(() => Decide(second)));

                var winner = Assert.Single(results, r => r.IsSuccess);
                var loser = Assert.Single(results, r => r.IsFailure);
                Assert.Equal(ErrorType.Conflict, loser.Error!.Type);
                Assert.Equal(ApprovalTaskMessages.Codes.AlreadyFinalized, loser.Error.Code);

                var stored = _repository.GetById(task.Id)!;
                Assert.Equal(winner.Value.Status, stored.Status);
                Assert.Equal(2, stored.History.Count);
                Assert.Single(stored.History, h => h.Action is ApprovalAction.Approve or ApprovalAction.Reject);
            }
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceConcurrencyTests"`
Expected: FAIL — in some iteration both decisions succeed (`Assert.Single` finds 2 successes). The race is timing-dependent; if it passes by chance, run it once more. If it still passes, continue anyway: the test guards the lock from Step 3 and the review must confirm the lock is present.

- [ ] **Step 3: Add the per-task lock**

In `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`:

Add `using System.Collections.Concurrent;` at the top (keep usings sorted).

Add to `#region Fields`, after `_timeProvider`:

```csharp

        /// <summary>
        /// One lock per existing task: actions on the same task run one at a time, different tasks run in parallel.
        /// Process-local only (see README, known limitations).
        /// </summary>
        private readonly ConcurrentDictionary<Guid, Lock> _taskLocks = new();
```

Replace the line in `ExecuteAction`

```csharp
            return ExecuteValidatedAction(id, action, actorId, command.Comment);
```

with

```csharp
            // Check existence before taking a lock, so unknown ids from requests never grow the lock map.
            // Tasks are never deleted, so a task seen here still exists inside the lock.
            if (_repository.GetById(id) is null)
            {
                return NotFound(id);
            }

            lock (_taskLocks.GetOrAdd(id, _ => new Lock()))
            {
                return ExecuteValidatedAction(id, action, actorId, command.Comment);
            }
```

Update the comment at the top of `ExecuteValidatedAction` to:

```csharp
            // Runs under the task's lock: read, validate, apply and save form one atomic step.
            // The repository returns a copy, so a failure below leaves stored data untouched.
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTasks"`
Expected: PASS (concurrency, service and state tests).

- [ ] **Step 5: Commit**

```bash
git add EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceConcurrencyTests.cs
git commit -m "feat(infrastructure): serialize actions per approval task with a lock"
```

---

### Task 8: API — `ApprovalTasksController`

**Files:**
- Create: `EdocsTestTask.Api/Contracts/ApprovalTasks/CreateApprovalTaskRequest.cs`
- Create: `EdocsTestTask.Api/Contracts/ApprovalTasks/ExecuteActionRequest.cs`
- Create: `EdocsTestTask.Api/Contracts/ApprovalTasks/ApprovalTaskResponse.cs`
- Create: `EdocsTestTask.Api/Contracts/ApprovalTasks/ApprovalHistoryEntryResponse.cs`
- Create: `EdocsTestTask.Api/Mappings/ApprovalTaskMappings.cs`
- Create: `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs`
- Delete: `EdocsTestTask.Api/Controllers/.gitkeep`
- Test: `EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs`

**Interfaces:**
- Consumes (Tasks 1, 6; JWT plan): `Result<T>.Map`, `ResultExtensions.ToActionResult<T>(int successStatusCode)`, `ServiceResponse<T>`, `IApprovalTaskService`, `CreateApprovalTaskCommand`, `ExecuteActionCommand`, `ApprovalTask`, `UserRoles.Author/Approver`, `User.GetUserId()`, `AuthApiFactory`, `TestJwtFactory.Create(subject, role)`, `AuthTestHelpers.AssertErrorAsync`, `AuthErrorCodes.Forbidden`.
- Produces:
  - `POST api/approval-tasks` (`Author`) body `{ documentNumber, title, assigneeId }` → 201 `ServiceResponse<ApprovalTaskResponse>`.
  - `POST api/approval-tasks/{id:guid}/actions` (`Approver`) body `{ action, comment }` → 200; actor from `sub`.
  - `GET api/approval-tasks/{id:guid}` (any authenticated) → 200.
  - `ApprovalTaskResponse(Guid Id, string DocumentNumber, string Title, string AssigneeId, string Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<ApprovalHistoryEntryResponse> History)`; `ApprovalHistoryEntryResponse(string Action, string ActorId, string? Comment, DateTime AtUtc)`.

**Acceptance Criteria:**
- Happy path over HTTP: Author creates (201, `Assigned`, empty history) → `approver-1` Start (200, `InProgress`) → Approve (200, `Approved`) → GET with author token (200, two history entries with `actorId = approver-1`).
- An `actorId` field in the action body is ignored; the actor is the token's `sub`.
- `approver-2` acting on a task assigned to `approver-1` → 403 `ApprovalTask.NotAssignee`; Approver creating → 403 `Auth.Forbidden`; Author acting → 403 `Auth.Forbidden`.
- Unknown action → 400 `ApprovalTask.UnknownAction`; invalid title → 400 `ApprovalTask.InvalidTitle`; unknown id → 404 `ApprovalTask.NotFound`; Approve on `Assigned` → 409 `ApprovalTask.InvalidTransition`; no token on GET → 401.

- [ ] **Step 1: Write the failing API tests**

Create `EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Tests.Api.Auth;

namespace EdocsTestTask.Tests.Api.ApprovalTasks
{
    public class ApprovalTasksApiTests : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string Route = "api/approval-tasks";

        #endregion

        #region Fields

        private readonly HttpClient _client;

        private readonly string _authorToken;

        private readonly string _approverToken;

        private readonly string _otherApproverToken;

        #endregion

        #region Constructors

        public ApprovalTasksApiTests(AuthApiFactory factory)
        {
            _client = factory.CreateClient();
            var tokens = new TestJwtFactory(factory.JwtOptions);
            _authorToken = tokens.Create("author-1", UserRoles.Author);
            _approverToken = tokens.Create("approver-1", UserRoles.Approver);
            _otherApproverToken = tokens.Create("approver-2", UserRoles.Approver);
        }

        #endregion

        #region Helpers

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, string? token, object? body = null)
        {
            using var request = new HttpRequestMessage(method, route);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await _client.SendAsync(request);
        }

        private static async Task<ApprovalTaskResponse> ReadTaskAsync(HttpResponseMessage response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ServiceResponse<ApprovalTaskResponse>>();
            Assert.NotNull(envelope);
            Assert.True(envelope.Success);
            return envelope.Data!;
        }

        private async Task<ApprovalTaskResponse> CreateTaskAsync()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _authorToken,
                new { documentNumber = "DOC-001", title = "Supply contract", assigneeId = "approver-1" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await ReadTaskAsync(response);
        }

        private Task<HttpResponseMessage> ActAsync(Guid id, string token, object body) =>
            SendAsync(HttpMethod.Post, $"{Route}/{id}/actions", token, body);

        #endregion

        #region Tests

        [Fact]
        public async Task FullPath_CreateStartApproveGet_ReturnsConsistentTask()
        {
            var created = await CreateTaskAsync();
            Assert.Equal("Assigned", created.Status);
            Assert.Equal(created.CreatedAtUtc, created.UpdatedAtUtc);
            Assert.Empty(created.History);

            var started = await ActAsync(created.Id, _approverToken, new { action = "Start" });
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
            Assert.Equal("InProgress", (await ReadTaskAsync(started)).Status);

            var approved = await ActAsync(created.Id, _approverToken, new { action = "Approve", comment = "OK" });
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            Assert.Equal("Approved", (await ReadTaskAsync(approved)).Status);

            var fetched = await SendAsync(HttpMethod.Get, $"{Route}/{created.Id}", _authorToken);
            Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
            var task = await ReadTaskAsync(fetched);
            Assert.Equal("Approved", task.Status);
            Assert.Collection(task.History,
                e => Assert.Equal(("Start", "approver-1", (string?)null), (e.Action, e.ActorId, e.Comment)),
                e => Assert.Equal(("Approve", "approver-1", (string?)"OK"), (e.Action, e.ActorId, e.Comment)));
        }

        [Fact]
        public async Task Action_ActorIdInBody_IsIgnored()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _otherApproverToken, new { action = "Start", actorId = "approver-1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.NotAssignee);
        }

        [Fact]
        public async Task Create_ByApprover_Returns403()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _approverToken,
                new { documentNumber = "DOC-001", title = "Supply contract", assigneeId = "approver-1" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Forbidden);
        }

        [Fact]
        public async Task Action_ByAuthor_Returns403()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _authorToken, new { action = "Start" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, AuthErrorCodes.Forbidden);
        }

        [Fact]
        public async Task Create_InvalidTitle_Returns400()
        {
            var response = await SendAsync(HttpMethod.Post, Route, _authorToken,
                new { documentNumber = "DOC-001", title = "  ", assigneeId = "approver-1" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.InvalidTitle);
        }

        [Fact]
        public async Task Action_Unknown_Returns400()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _approverToken, new { action = "Delete" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.UnknownAction);
        }

        [Fact]
        public async Task Action_MissingTask_Returns404()
        {
            var response = await ActAsync(Guid.NewGuid(), _approverToken, new { action = "Start" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.NotFound);
        }

        [Fact]
        public async Task Action_ApproveWhenAssigned_Returns409()
        {
            var created = await CreateTaskAsync();

            var response = await ActAsync(created.Id, _approverToken, new { action = "Approve" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AuthTestHelpers.AssertErrorAsync(response, ApprovalTaskMessages.Codes.InvalidTransition);
        }

        [Fact]
        public async Task Get_WithoutToken_Returns401()
        {
            var response = await SendAsync(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", token: null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTasksApiTests"`
Expected: build FAILS — `CS0234` namespace `EdocsTestTask.Api.Contracts.ApprovalTasks` not found.

- [ ] **Step 3: Create the contracts**

Create `EdocsTestTask.Api/Contracts/ApprovalTasks/CreateApprovalTaskRequest.cs`:

```csharp
namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Body of POST api/approval-tasks. Nullable so the service, not model binding, reports missing fields.
    /// </summary>
    public sealed record CreateApprovalTaskRequest(string? DocumentNumber, string? Title, string? AssigneeId);
}
```

Create `EdocsTestTask.Api/Contracts/ApprovalTasks/ExecuteActionRequest.cs`:

```csharp
namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Body of POST api/approval-tasks/{id}/actions. The actor is the authenticated user ("sub"), not a body field.
    /// </summary>
    public sealed record ExecuteActionRequest(string? Action, string? Comment);
}
```

Create `EdocsTestTask.Api/Contracts/ApprovalTasks/ApprovalHistoryEntryResponse.cs`:

```csharp
namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// One successful action in a task's history.
    /// </summary>
    public sealed record ApprovalHistoryEntryResponse(string Action, string ActorId, string? Comment, DateTime AtUtc);
}
```

Create `EdocsTestTask.Api/Contracts/ApprovalTasks/ApprovalTaskResponse.cs`:

```csharp
namespace EdocsTestTask.Api.Contracts.ApprovalTasks
{
    /// <summary>
    /// Approval task as returned to clients. Status and actions are names, e.g. "InProgress".
    /// </summary>
    public sealed record ApprovalTaskResponse(
        Guid Id,
        string DocumentNumber,
        string Title,
        string AssigneeId,
        string Status,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        IReadOnlyList<ApprovalHistoryEntryResponse> History);
}
```

- [ ] **Step 4: Create the mapping**

Create `EdocsTestTask.Api/Mappings/ApprovalTaskMappings.cs`:

```csharp
using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Core.Entities;

namespace EdocsTestTask.Api.Mappings
{
    /// <summary>
    /// Maps approval task entities to transport models.
    /// </summary>
    public static class ApprovalTaskMappings
    {
        #region Methods

        public static ApprovalTaskResponse ToResponse(this ApprovalTask task)
        {
            ArgumentNullException.ThrowIfNull(task);

            return new ApprovalTaskResponse(
                task.Id,
                task.DocumentNumber,
                task.Title,
                task.AssigneeId,
                task.Status.ToString(),
                task.CreatedAtUtc,
                task.UpdatedAtUtc,
                task.History
                    .Select(entry => new ApprovalHistoryEntryResponse(entry.Action.ToString(), entry.ActorId, entry.Comment, entry.AtUtc))
                    .ToArray());
        }

        #endregion
    }
}
```

- [ ] **Step 5: Create the controller**

Create `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs`:

```csharp
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Contracts;
using EdocsTestTask.Api.Contracts.ApprovalTasks;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Api.Mappings;
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdocsTestTask.Api.Controllers
{
    /// <summary>
    /// Approval tasks: Authors create them, the assigned Approver moves them through Start → Approve/Reject.
    /// </summary>
    [ApiController]
    [Route(BaseRoute)]
    public sealed class ApprovalTasksController : ControllerBase
    {
        #region Constants

        public const string BaseRoute = "api/approval-tasks";

        #endregion

        #region Fields

        private readonly IApprovalTaskService _service;

        #endregion

        #region Constructors

        public ApprovalTasksController(IApprovalTaskService service)
        {
            ArgumentNullException.ThrowIfNull(service);

            _service = service;
        }

        #endregion

        #region Methods

        [HttpPost]
        [Authorize(Roles = UserRoles.Author)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> Create([FromBody] CreateApprovalTaskRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var command = new CreateApprovalTaskCommand(request.DocumentNumber, request.Title, request.AssigneeId);
            return _service.Create(command)
                .Map(task => task.ToResponse())
                .ToActionResult(StatusCodes.Status201Created);
        }

        [HttpPost("{id:guid}/actions")]
        [Authorize(Roles = UserRoles.Approver)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> ExecuteAction(Guid id, [FromBody] ExecuteActionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var command = new ExecuteActionCommand(User.GetUserId(), request.Action, request.Comment);
            return _service.ExecuteAction(id, command)
                .Map(task => task.ToResponse())
                .ToActionResult();
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> GetById(Guid id) =>
            _service.GetById(id)
                .Map(task => task.ToResponse())
                .ToActionResult();

        #endregion
    }
}
```

Delete placeholder: `git rm EdocsTestTask.Api/Controllers/.gitkeep`

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTasksApiTests"`
Expected: PASS.

Then the full suite: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj` — Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add EdocsTestTask.Api/Contracts/ApprovalTasks EdocsTestTask.Api/Mappings EdocsTestTask.Api/Controllers EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs
git commit -m "feat(api): add approval tasks endpoints"
```

---

### Task 9: `.http` examples and README

**Files:**
- Modify: `EdocsTestTask.Api/EdocsTestTask.Api.http` (append)
- Modify: `README.md` (append sections after «Автентифікація»)

**Interfaces:**
- Consumes (JWT plan Task 4): `.http` variables `@authorToken`, `@approverToken`, `@otherApproverToken`, `@EdocsTestTask.Api_HostAddress`; README «Автентифікація».
- Produces: runnable request examples; README sections «Запуск», «Приклади», «Обробка станів (State pattern)», «Захист від конкурентних змін», «Відомі обмеження».

**Acceptance Criteria:**
- `.http` contains create → start → approve → get, plus reject-without-comment (400) and foreign approver (403), chained through `# @name createTask` and `{{createTask.response.body.$.data.id}}`.
- README has the run commands for API and tests, curl examples for create and action, the state table, the locking description and known limitations.
- README contains no JWT strings beyond those already in «Автентифікація» (the JWT plan's token-match check still passes).
- Running the app and sending the `.http` requests gives the documented status codes.

- [ ] **Step 1: Append requests to the `.http` file**

Append to `EdocsTestTask.Api/EdocsTestTask.Api.http`:

```http
### Create approval task (Author)
# @name createTask
POST {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks
Authorization: Bearer {{authorToken}}
Content-Type: application/json

{
  "documentNumber": "DOC-2026-001",
  "title": "Supply contract",
  "assigneeId": "approver-1"
}

### Start (assigned Approver)
POST {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks/{{createTask.response.body.$.data.id}}/actions
Authorization: Bearer {{approverToken}}
Content-Type: application/json

{
  "action": "Start"
}

### Reject without comment -> 400
POST {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks/{{createTask.response.body.$.data.id}}/actions
Authorization: Bearer {{approverToken}}
Content-Type: application/json

{
  "action": "Reject"
}

### Another approver -> 403
POST {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks/{{createTask.response.body.$.data.id}}/actions
Authorization: Bearer {{otherApproverToken}}
Content-Type: application/json

{
  "action": "Approve"
}

### Approve (assigned Approver)
POST {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks/{{createTask.response.body.$.data.id}}/actions
Authorization: Bearer {{approverToken}}
Content-Type: application/json

{
  "action": "Approve",
  "comment": "Looks good"
}

### Get task with history
GET {{EdocsTestTask.Api_HostAddress}}/api/approval-tasks/{{createTask.response.body.$.data.id}}
Authorization: Bearer {{authorToken}}

###
```

- [ ] **Step 2: Append README sections**

If `README.md` already has a «Запуск» section from the auth plan, skip the «Запуск» block below and keep the existing one. Append after the «Автентифікація» section:

````markdown
## Запуск

```bash
dotnet run --project EdocsTestTask.Api
dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj
```

API слухає `http://localhost:5113`. Готові запити — у `EdocsTestTask.Api/EdocsTestTask.Api.http`. Токени описані в розділі «Автентифікація».

## Приклади

Створення задачі (роль `Author`):

```bash
curl -X POST http://localhost:5113/api/approval-tasks \
  -H "Authorization: Bearer $AUTHOR_TOKEN" -H "Content-Type: application/json" \
  -d '{"documentNumber":"DOC-2026-001","title":"Supply contract","assigneeId":"approver-1"}'
```

Відповідь `201`: `{ "success": true, "data": { "id": "...", "status": "Assigned", "history": [] , ... } }`.

Дія (роль `Approver`, лише призначений виконавець):

```bash
curl -X POST http://localhost:5113/api/approval-tasks/<id>/actions \
  -H "Authorization: Bearer $APPROVER_TOKEN" -H "Content-Type: application/json" \
  -d '{"action":"Start"}'
```

`action`: `Start`, `Approve` або `Reject` (без урахування регістру); `comment` обов'язковий для `Reject`. Виконавець береться з claim `sub` токена, поле `actorId` у тілі ігнорується.

## Обробка станів (State pattern)

Кожен статус — окремий клас у `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/` з методами `Validate` і `Apply`. `ApprovalTaskService` (оркестратор) спершу виконує власний `Validate` команди, потім бере стан через `ApprovalTaskStateFactory` і делегує йому перевірку та перехід.

| Статус | Дозволено | Результат |
|---|---|---|
| `Assigned` | `Start` | `InProgress` |
| `InProgress` | `Approve` / `Reject` (з коментарем) | `Approved` / `Rejected` |
| `Approved`, `Rejected` | — | `409` |

Порядок перевірок: `400` (команда) → `404` (задача) → `403` (не виконавець) → `409`/`400` (правила стану). Усі повідомлення — константи в `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`.

## Захист від конкурентних змін

`ApprovalTaskService` тримає окремий `lock` на кожну задачу: читання, перевірка, перехід і збереження виконуються як один крок. Другий паралельний `Approve`/`Reject` бачить уже фінальний статус і отримує `409`; в історії лишається одне рішення. Репозиторій зберігає й повертає копії, тому помилка не змінює збережених даних. Тест: `ApprovalTaskServiceConcurrencyTests`.

## Відомі обмеження

- Блокування працює лише в межах одного процесу; для кількох інстансів потрібна оптимістична конкурентність у сховищі.
- Дані в пам'яті зникають після перезапуску; словник lock-ів росте разом із кількістю задач і не очищується.
- `assigneeId` не перевіряється на існування користувача з роллю `Approver`.
- Помилки десеріалізації тіла (некоректний JSON) повертаються стандартним `ProblemDetails`, а не конвертом `ServiceResponse`.
- Довжина коментаря не обмежена.
````

- [ ] **Step 3: Verify manually**

Run: `dotnet run --project EdocsTestTask.Api` (background), send the `.http` requests in order from the IDE (or the curl commands with the README tokens). Expected: 201, 200, 400, 403, 200, 200 with two history entries. Stop the app.

Run the JWT plan's token-match check:

```bash
diff <(grep -oE 'eyJ[A-Za-z0-9_.-]+' EdocsTestTask.Api/EdocsTestTask.Api.http) <(grep -oE 'eyJ[A-Za-z0-9_.-]+' README.md) && echo MATCH
```

Expected: `MATCH`.

- [ ] **Step 4: Commit**

```bash
git add EdocsTestTask.Api/EdocsTestTask.Api.http README.md
git commit -m "docs: document approval task API, state handling and concurrency"
```

---

## Spec coverage

| Spec requirement | Task |
|---|---|
| §1 State pattern, stateless states + factory | 5 |
| §1 State `Validate` on every state; orchestrator always has `Validate` | 5 (`IApprovalTaskState.Validate`), 6 (`ApprovalTaskService.Validate` ×2) |
| §1 In-memory repository returning copies | 4 |
| §1/§4 Title via `TextValidationHelper.IsTitleValid(command.Title, out var error)`, 3..200 | 2, 6 (`ValidateCreate`) |
| §1/§5 Messages as constants in a separate file, codes in nested `Codes` | 2 |
| §1/§6 Per-task lock, atomic apply-on-copy | 4 (copies), 7 (lock) |
| §2 Entities, enums, commands, interfaces | 3, 4, 6 |
| §2 Controller with roles, actor from `sub` | 8 |
| §3 Transition matrix, comment rule, final states | 5, 6 |
| §4 Create/ExecuteAction/GetById flow and check order | 6, 7 |
| §6 Known limitations in README | 9 |
| §7 Tests: helper, states, service paths, failures keep state, concurrency | 2, 5, 6, 7 (+ HTTP smoke tests in 8) |

Out of scope (not in the spec): `AI_USAGE.md` required by `docs/TEST_TASK.md` §05 — tracked separately.
