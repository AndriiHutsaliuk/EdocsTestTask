# Bugfixing Batch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use beads-superpowers:subagent-driven-development (recommended) or beads-superpowers:executing-plans to implement this plan task-by-task. Each Task becomes a bead (`bd create -t task --parent <epic-id>`). Steps within tasks use checkbox (`- [ ]`) syntax for human readability.

**Goal:** Apply the approved bugfixing batch:
- short-text validation for DocumentNumber and AssigneeId (≤128) and a Title limit of 256;
- `ApprovalTaskFactory`, and validation only through `Helper.Validate`;
- one transition table (`Helper.ResolveNextStatus`) in place of the State classes;
- primary constructors everywhere, plus the base-architecture rule;
- Swagger in Development with real response codes, and README updates.

**Architecture:** Validation rules live in Core (`TextValidationHelper`, `ValidationConstants`, `ApprovalTaskMessages`). The new static `ApprovalTaskFactory` (Core) builds new tasks. `ApprovalTaskService` calls `ApprovalTaskServiceHelper.Validate(...)` and `ApprovalTaskServiceHelper.ResolveNextStatus(...)` directly, and its dependencies come in through a primary constructor. Inside the per-task lock it updates status, time and history in one place. Swagger is Swashbuckle with a Bearer scheme, mounted only in Development and before authentication.

**Tech Stack:** .NET 10, ASP.NET Core Web API, xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing, Swashbuckle.AspNetCore 10.2.3 (Microsoft.OpenApi v2).

**Spec:** `.internal/specs/2026-10-02-bugfixing-batch-design.md`

## Global Constraints

- Work directly on `master` (user-approved). One commit per task; end every commit message with your session's `Co-Authored-By` trailer.
- Build: `dotnet build EdocsTestTask.slnx` must report **0 warnings, 0 errors**. Tests: `dotnet test EdocsTestTask.slnx` must be green (exactly 1 skipped test, the manual `GenerateStaticToken`).
- Code style: block-scoped `namespace X { ... }`, `#region` blocks, one class per file, XML `<summary>` on public types, braces on every `if`. Keep each file's existing line endings (C# files and `SKILL.md` are CRLF).
- Client-facing messages are `const string` in `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`; limits are in `EdocsTestTask.Core/Constants/ValidationConstants.cs`.
- Limits: `TitleMinLength = 3`, `TitleMaxLength = 256`, `ShortTextMaxLength = 128` (all applied to trimmed text).
- Primary constructors: use the parameters directly. No `ArgumentNullException.ThrowIfNull` for them and no copying into private fields. Exceptions: `Result`/`Result<T>` (they validate invariants and are non-public), and the parameterless test constructors `ApprovalTaskServiceTests()` / `ApprovalTaskServiceConcurrencyTests()`.
- A primary-constructor parameter that is used inside members must not also feed a field initializer (compiler warning CS9124).
- Swagger: package `Swashbuckle.AspNetCore` version `10.2.3`; models live in namespace `Microsoft.OpenApi` (not `Microsoft.OpenApi.Models`). Mounted only when `app.Environment.IsDevelopment()`, and before `UseAuthentication`/`UseAuthorization`.
- Security: do not remove or weaken any `[Authorize]` attribute, the fallback policy, or the actor-from-`sub` rule.

## File Map

| File | Task | Change |
|---|---|---|
| `EdocsTestTask.Core/Constants/ValidationConstants.cs` | 1 | `TitleMaxLength` 256; new `ShortTextMaxLength` 128 |
| `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs` | 1 | new `FieldTooLong` |
| `EdocsTestTask.Core/Helpers/TextValidationHelper.cs` | 1 | new `IsShortTextValid`; `[NotNullWhen(true)]` on `IsRequiredTextValid` value |
| `EdocsTestTask.Tests/Core/TextValidationHelperTests.cs` | 1 | limits 256 / short text tests |
| `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs` | 1, 3 | too-long title uses the constant; DocumentNumber limit tests |
| `EdocsTestTask.Core/Factories/ApprovalTaskFactory.cs` | 2 | new |
| `EdocsTestTask.Tests/Core/ApprovalTaskFactoryTests.cs` | 2 | new |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs` | 3, 4 | `ValidateCreate`/`ValidateAction` → `Validate` overloads; DocumentNumber and AssigneeId via `IsShortTextValid`; new `ResolveNextStatus` |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs` | 3, 4, 5 | `Helper.Validate`, `ApprovalTaskFactory`; transition via `Helper.ResolveNextStatus`; primary ctor |
| `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/` (9 files) | 4 | deleted |
| `EdocsTestTask.Infrastructure/DependencyInjection.cs` | 4 | drop the `ApprovalTaskStateFactory` registration |
| `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs` | 4 | deleted |
| `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceHelperTests.cs` | 4 | new: transition table tests |
| `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceConcurrencyTests.cs` | 4 | service ctor without the state factory |
| `README.md` | 3, 4, 6 | field limits, known limitations; «Обробка станів» rewritten; Swagger section |
| `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs` | 5, 6 | primary ctor; `[ProducesResponseType]` |
| `EdocsTestTask.Api/Middleware/GlobalExceptionHandler.cs` | 5 | primary ctor |
| `EdocsTestTask.Tests/Api/Auth/TestJwtFactory.cs`, `EdocsTestTask.Tests/TestData/TestTimeProvider.cs` | 5 | primary ctor |
| `EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs`, `EdocsTestTask.Tests/Api/Auth/AuthenticationTests.cs`, `AuthorizationTests.cs`, `StaticTokenTests.cs` | 5 | primary ctor |
| `.claude/skills/base-architecture/SKILL.md` | 5 | constructor rule |
| `EdocsTestTask.Api/EdocsTestTask.Api.csproj` | 6 | Swashbuckle package |
| `EdocsTestTask.Api/Extensions/SwaggerExtensions.cs` | 6 | new |
| `EdocsTestTask.Api/Program.cs` | 6 | wire Swagger |
| `EdocsTestTask.Api/Properties/launchSettings.json` | 6 | `launchBrowser` + `launchUrl: swagger` |
| `EdocsTestTask.Tests/Api/Swagger/SwaggerTests.cs` | 6 | new |
| `EdocsTestTask.Shared/Validation/ValidationResultExtensions.cs`, `EdocsTestTask.Tests/Shared/ValidationResultExtensionsTests.cs`, `EdocsTestTask.Api/Contracts/PagedResponse.cs` | 7 | deleted |
| `EdocsTestTask.Shared/Validation/ValidationResult.cs`, `ValidationErrorCodes.cs` | 7 | drop `Exception`/`SetException` and `ValidationErrorCodes.Exception` |
| `EdocsTestTask.Core/Constants/ValidationConstants.cs` | 1, 7 | Title/ShortText limits; drop the Description region |
| `EdocsTestTask.Infrastructure/DependencyInjection.cs`, `EdocsTestTask.Infrastructure/EdocsTestTask.Infrastructure.csproj`, `EdocsTestTask.Api/Program.cs` | 4, 6, 7 | `AddInfrastructure()` without `IConfiguration`; drop `Microsoft.Extensions.Configuration.Abstractions` |
| `.claude/skills/base-architecture/SKILL.md` | 5, 7 | constructor rule; Validation section matches the code |

Task order: 1 → 2 → 3 → 4 → 5 → 6 → 7.
- Task 3 uses the outputs of Tasks 1 and 2.
- Task 4 replaces the State classes after Task 3.
- Task 5 changes the service's constructor form after Task 4 has shrunk it to `(repository, timeProvider)`.
- Task 6's test uses the primary-constructor style from Task 5.
- Task 7 removes dead code last, because it touches files that earlier tasks edit (`Program.cs`, `ValidationConstants.cs`, `DependencyInjection.cs`, `SKILL.md`).

---

### Task 1: Text validation limits (Title 256, IsShortTextValid 128)

**Files:**
- Modify: `EdocsTestTask.Core/Constants/ValidationConstants.cs`
- Modify: `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`
- Modify: `EdocsTestTask.Core/Helpers/TextValidationHelper.cs`
- Test: `EdocsTestTask.Tests/Core/TextValidationHelperTests.cs`
- Test: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs` (one line)

**Interfaces:**
- Consumes: existing `TextValidationHelper.IsRequiredTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)`, `ApprovalTaskMessages.FieldRequired`.
- Produces:
  - `ValidationConstants.TitleMaxLength == 256`, `ValidationConstants.ShortTextMaxLength == 128`
  - `ApprovalTaskMessages.FieldTooLong` = `"{0} must be {1} characters or fewer."`
  - `static bool TextValidationHelper.IsShortTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)`

**Acceptance Criteria:**
- `IsTitleValid`: trimmed length 256 → true; 257 → false with `"Title must be between 3 and 256 characters."`.
- `IsShortTextValid`: null / `""` / whitespace → false with `"<fieldName> is required and cannot be empty or whitespace."`; trimmed length 1 and 128 (with and without padding) → true and `error == null`; 129 → false with `"<fieldName> must be 128 characters or fewer."`; blank `fieldName` → `ArgumentException`.
- `ValidationConstants.TitleMaxLength == 256`, `ValidationConstants.ShortTextMaxLength == 128`.
- Full suite green, 0 build warnings.

- [ ] **Step 1: Update the tests (RED)**

In `EdocsTestTask.Tests/Core/TextValidationHelperTests.cs`:

1. In `IsTitleValid_LengthOutOfRange_ReturnsLengthError` replace `[InlineData(201, "")]` with `[InlineData(257, "")]` and the expected message with `"Title must be between 3 and 256 characters."`.
2. In `IsTitleValid_LengthInRange_ReturnsTrue` replace `[InlineData(200, "")]` with `[InlineData(256, "")]`.
3. Replace the `TitleMaxLength_Is200` test with:

```csharp
        [Fact]
        public void TitleMaxLength_Is256()
        {
            Assert.Equal(256, ValidationConstants.TitleMaxLength);
        }

        [Fact]
        public void ShortTextMaxLength_Is128()
        {
            Assert.Equal(128, ValidationConstants.ShortTextMaxLength);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void IsShortTextValid_Missing_ReturnsFieldError(string? value)
        {
            var isValid = TextValidationHelper.IsShortTextValid(value, "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber is required and cannot be empty or whitespace.", error);
        }

        [Theory]
        [InlineData(1, "")]
        [InlineData(128, "")]
        [InlineData(128, "  ")]
        public void IsShortTextValid_WithinLimit_ReturnsTrue(int length, string padding)
        {
            var value = padding + new string('7', length) + padding;

            var isValid = TextValidationHelper.IsShortTextValid(value, "DocumentNumber", out var error);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void IsShortTextValid_TooLong_ReturnsLengthError()
        {
            var isValid = TextValidationHelper.IsShortTextValid(new string('7', 129), "DocumentNumber", out var error);

            Assert.False(isValid);
            Assert.Equal("DocumentNumber must be 128 characters or fewer.", error);
        }

        [Fact]
        public void IsShortTextValid_BlankFieldName_Throws()
        {
            Assert.ThrowsAny<ArgumentException>(() => TextValidationHelper.IsShortTextValid("x", " ", out _));
        }
```

In `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs`, `Create_TooLongTitle_ReturnsValidationError`: replace `new string('a', 201)` with `new string('a', ValidationConstants.TitleMaxLength + 1)`. Without this, the raised limit makes the test pass a valid title. `EdocsTestTask.Core.Constants` is already imported.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~TextValidationHelperTests"`
Expected: build FAILS with `CS0117` (`ValidationConstants` has no `ShortTextMaxLength`) and `CS0117` (`TextValidationHelper` has no `IsShortTextValid`).

- [ ] **Step 3: Implement**

`EdocsTestTask.Core/Constants/ValidationConstants.cs`: set `TitleMaxLength` to 256, then add a `ShortText` region between the `Title` and `Description` regions:

```csharp
        #region Title

        public const int TitleMinLength = 3;

        /// <summary>
        /// Raised from the 30-character default: approval task titles are document names.
        /// </summary>
        public const int TitleMaxLength = 256;

        #endregion

        #region ShortText

        /// <summary>
        /// Upper bound for short identifiers such as a document number.
        /// </summary>
        public const int ShortTextMaxLength = 128;

        #endregion
```

`EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`: add right after `FieldRequired`:

```csharp
        /// <summary>{0} = field name, {1} = maximum length.</summary>
        public const string FieldTooLong = "{0} must be {1} characters or fewer.";
```

`EdocsTestTask.Core/Helpers/TextValidationHelper.cs`:
- Change the `IsRequiredTextValid` signature to `public static bool IsRequiredTextValid([NotNullWhen(true)] string? value, string fieldName, [NotNullWhen(false)] out string? error)`. The body stays the same. This lets callers use `value` without `!` after a successful check.
- Add after `IsRequiredTextValid`:

```csharp
        /// <summary>
        /// Value must not be null, empty or whitespace, and its trimmed length must not exceed
        /// <see cref="ValidationConstants.ShortTextMaxLength"/>.
        /// </summary>
        public static bool IsShortTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)
        {
            if (!IsRequiredTextValid(value, fieldName, out error))
            {
                return false;
            }

            if (value.Trim().Length > ValidationConstants.ShortTextMaxLength)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    ApprovalTaskMessages.FieldTooLong,
                    fieldName,
                    ValidationConstants.ShortTextMaxLength);
                return false;
            }

            return true;
        }
```

Also update the class `<summary>` to say "Reusable checks for required text fields (title, short text). Each returns false with a client-facing message."

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~TextValidationHelperTests|FullyQualifiedName~ApprovalTaskServiceTests"`
Expected: PASS.

Then run `dotnet build EdocsTestTask.slnx` (0 warnings) and `dotnet test EdocsTestTask.slnx` (all green, 1 skipped).

- [ ] **Step 5: Commit**

```bash
git add EdocsTestTask.Core/Constants/ValidationConstants.cs EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs EdocsTestTask.Core/Helpers/TextValidationHelper.cs EdocsTestTask.Tests/Core/TextValidationHelperTests.cs EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs
git commit -m "feat(core): add IsShortTextValid (max 128) and raise title limit to 256"
```

---

### Task 2: ApprovalTaskFactory

**Files:**
- Create: `EdocsTestTask.Core/Factories/ApprovalTaskFactory.cs`
- Test: `EdocsTestTask.Tests/Core/ApprovalTaskFactoryTests.cs`

**Interfaces:**
- Consumes: `EdocsTestTask.Core.Commands.CreateApprovalTaskCommand(string? DocumentNumber, string? Title, string? AssigneeId)`, `EdocsTestTask.Core.Entities.ApprovalTask` (init-only `Id`, `required` `DocumentNumber`/`Title`/`AssigneeId`, settable `Status`/`UpdatedAtUtc`, init `CreatedAtUtc`, `List<ApprovalHistoryEntry> History`), `EdocsTestTask.Core.Enums.ApprovalStatus.Assigned`.
- Produces: `static ApprovalTask EdocsTestTask.Core.Factories.ApprovalTaskFactory.Create(CreateApprovalTaskCommand command, DateTime nowUtc)`.

**Acceptance Criteria:**
- The result has a non-empty `Id` (different on every call), trimmed `DocumentNumber`/`Title`/`AssigneeId`, `Status == Assigned`, `CreatedAtUtc == UpdatedAtUtc == nowUtc`, and an empty `History`.
- A null command → `ArgumentNullException`. A null/empty/whitespace `DocumentNumber`, `Title` or `AssigneeId` → `ArgumentException`, because that is a programming error: the caller validates first.
- Full suite green, 0 build warnings.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Core/ApprovalTaskFactoryTests.cs` (CRLF line endings):

```csharp
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Core.Factories;

namespace EdocsTestTask.Tests.Core
{
    public class ApprovalTaskFactoryTests
    {
        #region Fields

        private static readonly DateTime NowUtc = new(2026, 10, 2, 9, 30, 0, DateTimeKind.Utc);

        #endregion

        #region Tests

        [Fact]
        public void Create_ValidCommand_ReturnsTrimmedAssignedTaskWithEqualDatesAndEmptyHistory()
        {
            var task = ApprovalTaskFactory.Create(new CreateApprovalTaskCommand("  DOC-001 ", " Supply contract ", " approver-1 "), NowUtc);

            Assert.NotEqual(Guid.Empty, task.Id);
            Assert.Equal("DOC-001", task.DocumentNumber);
            Assert.Equal("Supply contract", task.Title);
            Assert.Equal("approver-1", task.AssigneeId);
            Assert.Equal(ApprovalStatus.Assigned, task.Status);
            Assert.Equal(NowUtc, task.CreatedAtUtc);
            Assert.Equal(NowUtc, task.UpdatedAtUtc);
            Assert.Empty(task.History);
        }

        [Fact]
        public void Create_CalledTwice_GivesDistinctIds()
        {
            var command = new CreateApprovalTaskCommand("DOC-001", "Supply contract", "approver-1");

            Assert.NotEqual(ApprovalTaskFactory.Create(command, NowUtc).Id, ApprovalTaskFactory.Create(command, NowUtc).Id);
        }

        [Fact]
        public void Create_NullCommand_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ApprovalTaskFactory.Create(null!, NowUtc));
        }

        [Theory]
        [InlineData(null, "Supply contract", "approver-1")]
        [InlineData("DOC-001", " ", "approver-1")]
        [InlineData("DOC-001", "Supply contract", "")]
        public void Create_UnvalidatedBlankField_Throws(string? documentNumber, string? title, string? assigneeId)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
                ApprovalTaskFactory.Create(new CreateApprovalTaskCommand(documentNumber, title, assigneeId), NowUtc));
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskFactoryTests"`
Expected: build FAILS with `CS0234` (namespace `EdocsTestTask.Core.Factories` does not exist).

- [ ] **Step 3: Implement**

Create `EdocsTestTask.Core/Factories/ApprovalTaskFactory.cs` (CRLF line endings):

```csharp
using EdocsTestTask.Core.Commands;
using EdocsTestTask.Core.Entities;
using EdocsTestTask.Core.Enums;

namespace EdocsTestTask.Core.Factories
{
    /// <summary>
    /// Builds new approval tasks. Callers validate the command first (see the service Helper).
    /// </summary>
    public static class ApprovalTaskFactory
    {
        #region Methods

        /// <summary>
        /// Creates a task in <see cref="ApprovalStatus.Assigned"/> with trimmed text fields,
        /// <c>CreatedAtUtc == UpdatedAtUtc == nowUtc</c> and an empty history.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
        /// <exception cref="ArgumentException">A text field is null, empty or whitespace: the command was not validated.</exception>
        public static ApprovalTask Create(CreateApprovalTaskCommand command, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.DocumentNumber);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.Title);
            ArgumentException.ThrowIfNullOrWhiteSpace(command.AssigneeId);

            return new ApprovalTask
            {
                Id = Guid.NewGuid(),
                DocumentNumber = command.DocumentNumber.Trim(),
                Title = command.Title.Trim(),
                AssigneeId = command.AssigneeId.Trim(),
                Status = ApprovalStatus.Assigned,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                History = []
            };
        }

        #endregion
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskFactoryTests"`
Expected: PASS (6 tests).

Then run `dotnet build EdocsTestTask.slnx` (0 warnings) and `dotnet test EdocsTestTask.slnx` (all green, 1 skipped).

- [ ] **Step 5: Commit**

```bash
git add EdocsTestTask.Core/Factories/ApprovalTaskFactory.cs EdocsTestTask.Tests/Core/ApprovalTaskFactoryTests.cs
git commit -m "feat(core): add ApprovalTaskFactory for new approval tasks"
```

---

### Task 3: Service validates through Helper.Validate and creates through ApprovalTaskFactory

**Files:**
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs`
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`
- Modify: `README.md`
- Test: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs`

**Interfaces:**
- Consumes: `TextValidationHelper.IsShortTextValid(...)`, `ValidationConstants.ShortTextMaxLength` (Task 1); `ApprovalTaskFactory.Create(CreateApprovalTaskCommand, DateTime)` (Task 2).
- Produces: `static ValidationResult ApprovalTaskServiceHelper.Validate(CreateApprovalTaskCommand command)` and `static ValidationResult ApprovalTaskServiceHelper.Validate(ExecuteActionCommand command)`. `ValidateCreate`/`ValidateAction` no longer exist. `TryParseAction` is unchanged.

**Acceptance Criteria:**
- Create with a 129-character `DocumentNumber` → Validation `ApprovalTask.InvalidDocumentNumber` with message `"DocumentNumber must be 128 characters or fewer."`. Exactly 128 characters → success.
- Create with a 129-character `AssigneeId` → Validation `ApprovalTask.InvalidAssignee` with message `"AssigneeId must be 128 characters or fewer."`.
- `ApprovalTaskService` has no private `Validate` methods. It calls `Helper.Validate(command)` in `Create` and `ExecuteAction`, and builds the task with `ApprovalTaskFactory.Create(command, _timeProvider.GetUtcNow().UtcDateTime)`.
- README («Обробка станів» is rewritten in Task 4, so this task leaves it alone):
  - «Приклади» lists the field limits.
  - «Відомі обмеження (approval tasks)» has the duplicate-Title item and the extended assigneeId item.
  - The assigneeId item is removed from the authentication «Відомі обмеження» list, and that list is renumbered.
- All existing service, state and API tests still pass. Full suite green, 0 build warnings.

- [ ] **Step 1: Write the failing tests**

Add to the `Tests` region of `ApprovalTaskServiceTests.cs`, right after `Create_TooLongTitle_ReturnsValidationError`:

```csharp
        [Fact]
        public void Create_TooLongDocumentNumber_ReturnsValidationError()
        {
            var documentNumber = new string('7', ValidationConstants.ShortTextMaxLength + 1);

            var result = _service.Create(new CreateApprovalTaskCommand(documentNumber, "Supply contract", Assignee));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidDocumentNumber);
            Assert.Equal("DocumentNumber must be 128 characters or fewer.", result.Error!.Message);
        }

        [Fact]
        public void Create_DocumentNumberAtLimit_Succeeds()
        {
            var documentNumber = new string('7', ValidationConstants.ShortTextMaxLength);

            var result = _service.Create(new CreateApprovalTaskCommand(documentNumber, "Supply contract", Assignee));

            Assert.True(result.IsSuccess);
            Assert.Equal(documentNumber, result.Value.DocumentNumber);
        }

        [Fact]
        public void Create_TooLongAssigneeId_ReturnsValidationError()
        {
            var assigneeId = new string('a', ValidationConstants.ShortTextMaxLength + 1);

            var result = _service.Create(new CreateApprovalTaskCommand("DOC-001", "Supply contract", assigneeId));

            AssertError(result, ErrorType.Validation, ApprovalTaskMessages.Codes.InvalidAssignee);
            Assert.Equal("AssigneeId must be 128 characters or fewer.", result.Error!.Message);
        }
```

- [ ] **Step 2: Run the tests to verify the new behavior fails**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceTests.Create_"`
Expected: `Create_TooLongDocumentNumber_ReturnsValidationError` and `Create_TooLongAssigneeId_ReturnsValidationError` FAIL, because the 129-character values are currently accepted (`Assert.True(result.IsFailure)` fails in `AssertError`). `Create_DocumentNumberAtLimit_Succeeds` passes.

- [ ] **Step 3: Implement the Helper**

In `ApprovalTaskServiceHelper.cs` replace the two validation methods with the following. `TryParseAction` and the class header stay as they are.

```csharp
        /// <summary>
        /// Title via <see cref="TextValidationHelper.IsTitleValid"/> (3..256 trimmed);
        /// DocumentNumber and AssigneeId via <see cref="TextValidationHelper.IsShortTextValid"/> (required, up to 128 trimmed).
        /// Reports every failing field.
        /// </summary>
        public static ValidationResult Validate(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = new ValidationResult();

            if (!TextValidationHelper.IsTitleValid(command.Title, out var titleError))
            {
                validation.AddError(titleError, ApprovalTaskMessages.Codes.InvalidTitle);
            }

            if (!TextValidationHelper.IsShortTextValid(command.DocumentNumber, nameof(command.DocumentNumber), out var documentNumberError))
            {
                validation.AddError(documentNumberError, ApprovalTaskMessages.Codes.InvalidDocumentNumber);
            }

            if (!TextValidationHelper.IsShortTextValid(command.AssigneeId, nameof(command.AssigneeId), out var assigneeError))
            {
                validation.AddError(assigneeError, ApprovalTaskMessages.Codes.InvalidAssignee);
            }

            return validation;
        }

        /// <summary>
        /// ActorId required; Action required and one of <see cref="ApprovalAction"/> names (case-insensitive).
        /// Whether the action is allowed for the task's status is decided later by the transition table.
        /// </summary>
        public static ValidationResult Validate(ExecuteActionCommand command)
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
```

- [ ] **Step 4: Implement the service changes**

In `ApprovalTaskService.cs`:

1. Usings: add `using EdocsTestTask.Core.Factories;` and remove `using EdocsTestTask.Shared.Validation;`. `ValidationResult` is no longer named in this file, because `ToFailure<T>()` is an instance method on it.
2. Replace the body of `Create` with:

```csharp
        public Result<ApprovalTask> Create(CreateApprovalTaskCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);

            var validation = Helper.Validate(command);
            if (!validation.IsValid)
            {
                return validation.ToFailure<ApprovalTask>();
            }

            var task = ApprovalTaskFactory.Create(command, _timeProvider.GetUtcNow().UtcDateTime);

            _repository.Add(task);
            return task;
        }
```

3. In `ExecuteAction`, replace `var validation = Validate(command);` with `var validation = Helper.Validate(command);`.
4. Delete both `private static ValidationResult Validate(...)` methods and their XML comments from the `Private Methods` region.
5. Change the class `<summary>` to: `Orchestrates approval task operations: validates the command through <see cref="ApprovalTaskServiceHelper"/>, then delegates status rules to the <see cref="IApprovalTaskState"/> of the task's current status.`

- [ ] **Step 5: Run the tests**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceTests"`
Expected: PASS.

- [ ] **Step 6: Update README.md**

README.md has LF line endings; keep them.

Leave «Обробка станів (State pattern)» unchanged: Task 4 rewrites it.

(b) In «Приклади», right after the line ``Відповідь `201`: ...``, add a blank line and:

`` Обмеження полів (після обрізання пробілів по краях): `title` — 3–256 символів, `documentNumber` і `assigneeId` — обов'язкові, до 128 символів. ``

(c) In «Відомі обмеження (approval tasks)» replace the line

`` - `assigneeId` не перевіряється на існування користувача з роллю `Approver`. ``

with these two lines:

```markdown
- Під час створення не перевіряється, чи існує користувач `assigneeId` і чи має він роль `Approver`: окремої бази користувачів у тестовому проєкті немає, користувачі існують лише як claims у статичних токенах.
- Можна створити кілька задач з однаковим `title` (і з однаковим `documentNumber`), бо унікальність не перевіряється. ТЗ не визначає, чи це дозволено, тож питання відкрите.
```

(d) In «Автентифікація → Відомі обмеження» delete item `3. Сховища користувачів немає, тому `assigneeId` не перевіряється на існування.` and renumber the remaining items to 1–4. The old item 4 (`Профіль http ...`) becomes 3, and the old item 5 (`Після 2027-10-01 ...`) becomes 4.

- [ ] **Step 7: Full verification**

Run `dotnet build EdocsTestTask.slnx` (0 warnings) and `dotnet test EdocsTestTask.slnx` (all green, 1 skipped). Then run `git grep -n "ValidateCreate\|ValidateAction" -- "*.cs"`. Expected: no output.

- [ ] **Step 8: Commit**

```bash
git add EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs README.md
git commit -m "refactor(infrastructure): validate via Helper.Validate, build tasks via ApprovalTaskFactory, limit DocumentNumber and AssigneeId to 128"
```

---

### Task 4: Replace the State classes with one transition table

**Files:**
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskServiceHelper.cs` (add `ResolveNextStatus`)
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`
- Modify: `EdocsTestTask.Infrastructure/DependencyInjection.cs`
- Delete: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/` (all 9 files: `IApprovalTaskState.cs`, `ApprovalTaskStateContext.cs`, `ApprovalTaskStateFactory.cs`, `ApprovalTaskStateBase.cs`, `FinalApprovalTaskState.cs`, `AssignedState.cs`, `InProgressState.cs`, `ApprovedState.cs`, `RejectedState.cs`)
- Delete: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs`
- Create: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceHelperTests.cs`
- Modify: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceTests.cs`
- Modify: `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceConcurrencyTests.cs`
- Modify: `README.md` («Обробка станів»)

**Interfaces:**
- Consumes:
  - `Result<T>`: implicit conversions from `T` and from `Error`, `IsFailure`, `Errors`, and `static Result<T>.Failure(IEnumerable<Error>)`.
  - `Error.Conflict(code, message)` and `Error.Validation(code, message)`.
  - `ApprovalTaskMessages.InvalidTransition` (`{0}` = action, `{1}` = status), `TaskAlreadyFinalized` (`{0}` = status), `RejectCommentRequired`, and the codes `InvalidTransition`, `AlreadyFinalized`, `RejectCommentRequired`.
- Produces:
  - `public static Result<ApprovalStatus> ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus status, ApprovalAction action, string? comment)`.
  - The `ApprovalTaskService` constructor becomes `(IApprovalTaskRepository repository, TimeProvider timeProvider)`.
  - The `EdocsTestTask.Infrastructure.Services.ApprovalTasks.States` namespace no longer exists.

**Acceptance Criteria:**
- The table:
  - `Assigned`+`Start` → `InProgress`
  - `InProgress`+`Approve` → `Approved`
  - `InProgress`+`Reject` with a non-blank comment → `Rejected`
  - `Approved`/`Rejected` + any action → Conflict `ApprovalTask.AlreadyFinalized`
  - any other pair → Conflict `ApprovalTask.InvalidTransition`
  - `InProgress`+`Reject` with a null/empty/whitespace comment → Validation `ApprovalTask.RejectCommentRequired`
- Check order: final status, then the pair, then the comment. So `Approved`+`Reject` without a comment → `AlreadyFinalized`, and `Assigned`+`Reject` without a comment → `InvalidTransition`.
- Exactly 3 of all status × action pairs succeed.
- Messages are unchanged: `"Action 'Approve' is not allowed when task is in status 'Assigned'."` and `"Task is already Rejected; no further actions are allowed."`.
- The service changes status, `UpdatedAtUtc` and history in one place inside the existing lock. A whitespace comment is stored as `null`, and a padded comment is stored trimmed.
- The `States/` folder, the `ApprovalTaskStateFactory` registration and `ApprovalTaskStatesTests.cs` are gone. `git grep -n "ApprovalTaskState\|\.States" -- "*.cs"` prints nothing.
- All existing service, concurrency and API tests pass unchanged except for the constructor call. Full suite green, 0 build warnings.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskServiceHelperTests.cs` (CRLF line endings):

```csharp
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Core.Enums;
using EdocsTestTask.Infrastructure.Services.ApprovalTasks;
using EdocsTestTask.Shared.Results;

namespace EdocsTestTask.Tests.Infrastructure.ApprovalTasks
{
    public class ApprovalTaskServiceHelperTests
    {
        #region Tests

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Start, null, ApprovalStatus.InProgress)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Approve, null, ApprovalStatus.Approved)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "Missing signature", ApprovalStatus.Rejected)]
        public void ResolveNextStatus_AllowedTransition_ReturnsNextStatus(
            ApprovalStatus status, ApprovalAction action, string? comment, ApprovalStatus expected)
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
        }

        [Theory]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.Assigned, ApprovalAction.Reject, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.InvalidTransition)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, null, ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.InProgress, ApprovalAction.Reject, "   ", ErrorType.Validation, ApprovalTaskMessages.Codes.RejectCommentRequired)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Approved, ApprovalAction.Reject, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Start, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Approve, null, ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        [InlineData(ApprovalStatus.Rejected, ApprovalAction.Reject, "Bad", ErrorType.Conflict, ApprovalTaskMessages.Codes.AlreadyFinalized)]
        public void ResolveNextStatus_NotAllowed_ReturnsSingleError(
            ApprovalStatus status, ApprovalAction action, string? comment, ErrorType type, string code)
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment);

            Assert.True(result.IsFailure);
            var error = Assert.Single(result.Errors);
            Assert.Equal(type, error.Type);
            Assert.Equal(code, error.Code);
        }

        [Fact]
        public void ResolveNextStatus_InvalidTransition_MessageNamesActionAndStatus()
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus.Assigned, ApprovalAction.Approve, null);

            Assert.Equal("Action 'Approve' is not allowed when task is in status 'Assigned'.", result.Error!.Message);
        }

        [Fact]
        public void ResolveNextStatus_Finalized_MessageNamesStatus()
        {
            var result = ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus.Rejected, ApprovalAction.Start, null);

            Assert.Equal("Task is already Rejected; no further actions are allowed.", result.Error!.Message);
        }

        [Fact]
        public void ResolveNextStatus_AcrossAllStatusesAndActions_AllowsExactlyThreeTransitions()
        {
            var allowed =
                from status in Enum.GetValues<ApprovalStatus>()
                from action in Enum.GetValues<ApprovalAction>()
                where ApprovalTaskServiceHelper.ResolveNextStatus(status, action, "comment").IsSuccess
                select (status, action);

            Assert.Equal(
                new[]
                {
                    (ApprovalStatus.Assigned, ApprovalAction.Start),
                    (ApprovalStatus.InProgress, ApprovalAction.Approve),
                    (ApprovalStatus.InProgress, ApprovalAction.Reject)
                },
                allowed.ToArray());
        }

        #endregion
    }
}
```

In `ApprovalTaskServiceTests.cs` add, after `ExecuteAction_StartThenReject_CompletesWithHistory`:

```csharp
        [Fact]
        public void ExecuteAction_WhitespaceComment_IsStoredAsNull()
        {
            var created = CreateTask();
            Assert.True(Act(created.Id, "Start").IsSuccess);

            var result = Act(created.Id, "Approve", "   ");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.History[^1].Comment);
        }
```

The trimmed case (`"  Looks good  "` → `"Looks good"`) is already covered by `ExecuteAction_StartThenApprove_CompletesWithHistory`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceHelperTests"`
Expected: build FAILS with `CS0117` (`ApprovalTaskServiceHelper` does not contain a definition for `ResolveNextStatus`).

- [ ] **Step 3: Add ResolveNextStatus to the Helper**

In `ApprovalTaskServiceHelper.cs`:
- Add `using EdocsTestTask.Shared.Results;`.
- Change the class `<summary>` to `Input validation and the status transition table for <see cref="ApprovalTaskService"/>.`
- Add after `Validate(ExecuteActionCommand)`:

```csharp
        /// <summary>
        /// The whole workflow in one table:
        /// Assigned + Start → InProgress; InProgress + Approve → Approved; InProgress + Reject (non-empty comment) → Rejected.
        /// Checked in this order: a final status → Conflict AlreadyFinalized; a pair not in the table → Conflict InvalidTransition;
        /// Reject without a comment → Validation RejectCommentRequired.
        /// </summary>
        public static Result<ApprovalStatus> ResolveNextStatus(ApprovalStatus status, ApprovalAction action, string? comment)
        {
            if (status is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            {
                return Error.Conflict(
                    ApprovalTaskMessages.Codes.AlreadyFinalized,
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.TaskAlreadyFinalized, status));
            }

            ApprovalStatus? nextStatus = (status, action) switch
            {
                (ApprovalStatus.Assigned, ApprovalAction.Start) => ApprovalStatus.InProgress,
                (ApprovalStatus.InProgress, ApprovalAction.Approve) => ApprovalStatus.Approved,
                (ApprovalStatus.InProgress, ApprovalAction.Reject) => ApprovalStatus.Rejected,
                _ => null
            };

            if (nextStatus is null)
            {
                return Error.Conflict(
                    ApprovalTaskMessages.Codes.InvalidTransition,
                    string.Format(CultureInfo.InvariantCulture, ApprovalTaskMessages.InvalidTransition, action, status));
            }

            if (action == ApprovalAction.Reject && string.IsNullOrWhiteSpace(comment))
            {
                return Error.Validation(ApprovalTaskMessages.Codes.RejectCommentRequired, ApprovalTaskMessages.RejectCommentRequired);
            }

            return nextStatus.Value;
        }
```

- [ ] **Step 4: Use it in the service; delete the State classes**

In `ApprovalTaskService.cs`:
1. Remove `using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;`. Remove the `_stateFactory` field, the `stateFactory` constructor parameter, its `ThrowIfNull` and its assignment. The constructor becomes `public ApprovalTaskService(IApprovalTaskRepository repository, TimeProvider timeProvider)`. Task 5 turns it into a primary constructor.
2. Class `<summary>`: `Orchestrates approval task operations: validates the command through <see cref="ApprovalTaskServiceHelper"/>, resolves the next status from its transition table, and applies the change under a per-task lock.`
3. Replace `ExecuteValidatedAction` and add `NormalizeComment` (both in `Private Methods`):

```csharp
        private Result<ApprovalTask> ExecuteValidatedAction(Guid id, ApprovalAction action, string actorId, string? comment)
        {
            // Runs under the task's lock: read, check, change and save form one atomic step.
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

            var nextStatus = Helper.ResolveNextStatus(task.Status, action, comment);
            if (nextStatus.IsFailure)
            {
                return Result<ApprovalTask>.Failure(nextStatus.Errors);
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            task.Status = nextStatus.Value;
            task.UpdatedAtUtc = nowUtc;
            task.History.Add(new ApprovalHistoryEntry(action, actorId, NormalizeComment(comment), nowUtc));

            _repository.Update(task);
            return task;
        }

        private static string? NormalizeComment(string? comment) =>
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
```

4. Delete the folder `EdocsTestTask.Infrastructure/Services/ApprovalTasks/States/` (all 9 files) with `git rm -r`.
5. `EdocsTestTask.Infrastructure/DependencyInjection.cs`: delete `services.AddSingleton<ApprovalTaskStateFactory>();` and `using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;`.
6. Tests:
   - `git rm EdocsTestTask.Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs`.
   - In `ApprovalTaskServiceTests.cs` and `ApprovalTaskServiceConcurrencyTests.cs`, remove `using EdocsTestTask.Infrastructure.Services.ApprovalTasks.States;`. Change the constructor calls to `new ApprovalTaskService(_repository, _time)` and `new ApprovalTaskService(_repository, time)`.
   - Keep `EdocsTestTask.Tests/TestData/ApprovalTaskTestData.cs`: `ApprovalTaskTests`, `InMemoryApprovalTaskRepositoryTests` and the service tests still use it.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~ApprovalTaskServiceHelperTests|FullyQualifiedName~ApprovalTaskServiceTests|FullyQualifiedName~ApprovalTaskServiceConcurrencyTests|FullyQualifiedName~ApprovalTasksApiTests"`
Expected: PASS.

- [ ] **Step 6: Rewrite README «Обробка станів»**

README.md uses LF line endings. Replace the whole section, from `## Обробка станів (State pattern)` up to (not including) `## Захист від конкурентних змін`, with:

```markdown
## Обробка станів

Увесь workflow описує одна таблиця в `ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment)`. Вона повертає наступний статус або помилку:

| Поточний статус | Дія | Результат |
|---|---|---|
| `Assigned` | `Start` | `InProgress` |
| `InProgress` | `Approve` | `Approved` |
| `InProgress` | `Reject` з непорожнім `comment` | `Rejected` (без коментаря — `400`) |
| `Approved`, `Rejected` | будь-яка | `409` `ApprovalTask.AlreadyFinalized` |
| інші комбінації | | `409` `ApprovalTask.InvalidTransition` |

`ApprovalTaskService` спершу перевіряє команду через `Helper.Validate`. Нову задачу будує `ApprovalTaskFactory`. Для дії сервіс усередині lock задачі бере наступний статус із таблиці й в одному місці оновлює статус, `updatedAtUtc` та історію.

Окремі класи на кожен стан (State pattern) спершу були реалізовані, але потім їх прибрали. На три переходи вони давали дев'ять файлів і дублювали правила (перевірка дії та вибір наступного статусу жили в різних методах). Таблиця тримає весь workflow в одному місці. State pattern мав би сенс, якби стани мали власну поведінку: ескалації, таймери, дії при вході чи виході.

Порядок перевірок: `400` (команда) → `404` (задача) → `403` (не виконавець) → `409`/`400` (таблиця переходів). Усі повідомлення — константи в `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`.
```

- [ ] **Step 7: Full verification**

Run `dotnet build EdocsTestTask.slnx` (0 warnings) and `dotnet test EdocsTestTask.slnx` (all green, 1 skipped).
Run `git grep -n "ApprovalTaskState\|ApprovalTasks.States\|IApprovalTaskState" -- "*.cs" "*.md" ":!.internal"`. Expected: no output.

- [ ] **Step 8: Commit**

```bash
git add -A EdocsTestTask.Infrastructure EdocsTestTask.Tests/Infrastructure/ApprovalTasks README.md
git commit -m "refactor(infrastructure): replace State classes with a single transition table in the service helper"
```

---

### Task 5: Primary constructors and the base-architecture rule

Behavior does not change, so the existing suite is the safety net. Correctness is the full suite staying green, 0 warnings, and the constructor check in Step 8.

**Files:**
- Modify: `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs`
- Modify: `EdocsTestTask.Api/Middleware/GlobalExceptionHandler.cs`
- Modify: `EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs`
- Modify: `EdocsTestTask.Tests/Api/Auth/TestJwtFactory.cs`
- Modify: `EdocsTestTask.Tests/TestData/TestTimeProvider.cs`
- Modify: `EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs`
- Modify: `EdocsTestTask.Tests/Api/Auth/AuthenticationTests.cs`
- Modify: `EdocsTestTask.Tests/Api/Auth/AuthorizationTests.cs`
- Modify: `EdocsTestTask.Tests/Api/Auth/StaticTokenTests.cs`
- Modify: `.claude/skills/base-architecture/SKILL.md`

**Interfaces:**
- Consumes: the service after Task 4, whose constructor is `(IApprovalTaskRepository repository, TimeProvider timeProvider)`.
- Produces: unchanged public APIs and constructor signatures. Only the declaration form changes.

**Acceptance Criteria:**
- Every listed class uses a primary constructor. Removed: the `Constructors` regions, the `ThrowIfNull` calls on constructor parameters, and fields that only copied a parameter.
- The Step 8 search finds explicit constructors only in `Result.cs`, `ResultOfT.cs`, `ApprovalTaskServiceTests.cs` and `ApprovalTaskServiceConcurrencyTests.cs`.
- `base-architecture/SKILL.md` contains the constructor rule.
- Full suite green, 0 build warnings (in particular, no CS9124).

- [ ] **Step 1: ApprovalTasksController**

Change the declaration to `public sealed class ApprovalTasksController(IApprovalTaskService service) : ControllerBase`. Delete the `Fields` region (`_service`) and the `Constructors` region. In the three actions replace `_service.` with `service.`. Attributes, the `Constants` region and the method bodies stay otherwise the same.

- [ ] **Step 2: GlobalExceptionHandler**

Change the declaration to `public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler`. Delete the `Fields` and `Constructors` regions. Replace `_logger.LogError(` with `logger.LogError(`.

- [ ] **Step 3: ApprovalTaskService**

Replace the class declaration and its `Fields` and `Constructors` regions with:

```csharp
    public sealed class ApprovalTaskService(IApprovalTaskRepository repository, TimeProvider timeProvider) : IApprovalTaskService
    {
        #region Fields

        /// <summary>
        /// One lock per existing task: actions on the same task run one at a time, different tasks run in parallel.
        /// Process-local only (see README, known limitations).
        /// </summary>
        private readonly ConcurrentDictionary<Guid, Lock> _taskLocks = new();

        #endregion
```

In the rest of the file replace `_repository.` with `repository.` (in `Create`, `ExecuteAction`, `GetById` and `ExecuteValidatedAction`) and `_timeProvider.` with `timeProvider.` (in `Create` and `ExecuteValidatedAction`). Keep the class `<summary>` from Task 4.

- [ ] **Step 4: TestJwtFactory and TestTimeProvider**

`TestJwtFactory.cs`:
- Declaration: `public sealed class TestJwtFactory(JwtOptions options)`.
- Delete the `_options` field and the `Constructors` region. Keep `private readonly JsonWebTokenHandler _handler = new();` in `Fields`.
- Replace every `_options.` with `options.` (4 occurrences: `Issuer`, `Audience` and `SigningKey` in `Create`; `Issuer`/`Audience` in `CreateUnsigned`).

`TestTimeProvider.cs`:

```csharp
    public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
    {
        #region Fields

        private DateTimeOffset _utcNow = start;

        #endregion

        #region Methods

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);

        #endregion
    }
```

The field stays because `Advance` mutates it. `start` is only used by the initializer, so it is not captured.

- [ ] **Step 5: ApprovalTasksApiTests and AuthorizationTests (fixture only feeds fields)**

`ApprovalTasksApiTests.cs`: declaration `public class ApprovalTasksApiTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>`. Replace the `Fields` and `Constructors` regions with:

```csharp
        #region Fields

        private readonly HttpClient _client = factory.CreateClient();

        private readonly string _authorToken = CreateToken(factory, "author-1", UserRoles.Author);

        private readonly string _approverToken = CreateToken(factory, "approver-1", UserRoles.Approver);

        private readonly string _otherApproverToken = CreateToken(factory, "approver-2", UserRoles.Approver);

        #endregion
```

Then add as the first member of the existing `Helpers` region:

```csharp
        private static string CreateToken(AuthApiFactory fixture, string userId, string role) =>
            new TestJwtFactory(fixture.JwtOptions).Create(userId, role);
```

The parameter is named `fixture`, not `factory`, so it does not shadow the primary constructor parameter.

`AuthorizationTests.cs`: declaration `public class AuthorizationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>`. Delete the `Constructors` region and turn the fields into initializers:

```csharp
        #region Fields

        private readonly HttpClient _client = factory.CreateClient();

        private readonly TestJwtFactory _tokens = new(factory.JwtOptions);

        #endregion
```

- [ ] **Step 6: AuthenticationTests and StaticTokenTests (the fixture is also used inside tests)**

Here `factory` is captured because tests call `factory.WithWebHostBuilder` / `factory.JwtOptions`. So nothing derived from it may be a field initializer (CS9124). Derived values become private properties that are computed on each access. xUnit builds a new class instance per test anyway, so the lifetime stays the same.

`AuthenticationTests.cs`: declaration `public class AuthenticationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>`. Replace the `Fields` and `Constructors` regions with:

```csharp
        #region Properties

        /// <summary>
        /// Computed from the fixture: <c>factory</c> is also used inside tests, so field initializers would capture it twice (CS9124).
        /// </summary>
        private HttpClient Client => factory.CreateClient();

        private TestJwtFactory Tokens => new(factory.JwtOptions);

        #endregion
```

Then replace every `_client.` with `Client.` and every `_tokens.` with `Tokens.`. In `MissingOrShortSigningKey_FailsStartup` rename the local to avoid shadowing:

```csharp
            using var misconfiguredFactory = factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] = signingKey
                    })));

            var exception = Record.Exception(() => misconfiguredFactory.CreateClient());
```

`StaticTokenTests.cs`: declaration `public class StaticTokenTests(AuthApiFactory factory, ITestOutputHelper output) : IClassFixture<AuthApiFactory>`. Delete the `Fields` and `Constructors` regions. Then change these lines:
- `var response = await _client.GetWithTokenAsync(...)` → `var response = await factory.CreateClient().GetWithTokenAsync(...)`
- `new TestJwtFactory(_factory.JwtOptions)` → `new TestJwtFactory(factory.JwtOptions)`
- `_output.WriteLine(` → `output.WriteLine(`

- [ ] **Step 7: base-architecture rule**

In `.claude/skills/base-architecture/SKILL.md` (CRLF), section «Services and structure», add this bullet after the line `- One class per file; use **regions** inside classes; meaningful names.`:

```markdown
- **Constructors:** classes that receive dependencies use C# primary constructors and use the parameters directly — no `ArgumentNullException.ThrowIfNull` and no copying into private fields. Derived values (e.g. `factory.CreateClient()`) become field initializers; if the parameter is also used inside members, compute them in members instead (a parameter that is both captured and used in an initializer triggers warning CS9124). Keep an explicit constructor only when it validates invariants or must be non-public (e.g. `Result`), or when it has no parameters and builds fields that depend on each other.
```

- [ ] **Step 8: Full verification**

Run `dotnet build EdocsTestTask.slnx`. Expected: 0 warnings, 0 errors (check that there is no CS9124).
Run `dotnet test EdocsTestTask.slnx`. Expected: all green, 1 skipped.
Run:

```bash
grep -rnE "^\s+(public|internal|private|protected) [A-Z][A-Za-z]+\(" --include=*.cs EdocsTestTask.Shared EdocsTestTask.Core EdocsTestTask.Infrastructure EdocsTestTask.Api EdocsTestTask.Tests | grep -v -E "/(obj|bin)/"
```

Expected: exactly 4 lines, in `Result.cs`, `ResultOfT.cs`, `ApprovalTaskServiceTests.cs` and `ApprovalTaskServiceConcurrencyTests.cs`.

- [ ] **Step 9: Commit**

```bash
git add EdocsTestTask.Api/Controllers/ApprovalTasksController.cs EdocsTestTask.Api/Middleware/GlobalExceptionHandler.cs EdocsTestTask.Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs EdocsTestTask.Tests/Api/Auth/TestJwtFactory.cs EdocsTestTask.Tests/TestData/TestTimeProvider.cs EdocsTestTask.Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs EdocsTestTask.Tests/Api/Auth/AuthenticationTests.cs EdocsTestTask.Tests/Api/Auth/AuthorizationTests.cs EdocsTestTask.Tests/Api/Auth/StaticTokenTests.cs .claude/skills/base-architecture/SKILL.md
git commit -m "refactor: use primary constructors and document the rule in base-architecture"
```

---

### Task 6: Swagger (Development only) with JWT Authorize

**Files:**
- Modify: `EdocsTestTask.Api/EdocsTestTask.Api.csproj`
- Create: `EdocsTestTask.Api/Extensions/SwaggerExtensions.cs`
- Modify: `EdocsTestTask.Api/Program.cs`
- Modify: `EdocsTestTask.Api/Properties/launchSettings.json`
- Modify: `EdocsTestTask.Api/Controllers/ApprovalTasksController.cs` (`[ProducesResponseType]`)
- Modify: `README.md`
- Test: `EdocsTestTask.Tests/Api/Swagger/SwaggerTests.cs`

**Interfaces:**
- Consumes:
  - `AuthApiFactory` (Development host) and `AuthTestHelpers.GetWithTokenAsync(this HttpClient client, string route, string? token)`.
  - `TestJwtFactory(JwtOptions options).Create(string? subject, string? role, ...)` and `JwtOptions.SectionName`.
  - `ApprovalTasksController.BaseRoute` (`"api/approval-tasks"`) and `UserRoles.Author`.
  - The envelopes `EdocsTestTask.Api.Contracts.ServiceResponse` (errors, including 401/403 from auth) and `ServiceResponse<T>` (success).
- Produces:
  - `public const string SwaggerExtensions.DocumentName = "v1"` and `public const string SwaggerExtensions.BearerSchemeName = "bearer"`
  - `IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)`
  - `IApplicationBuilder UseSwaggerWithUi(this IApplicationBuilder app)`

**Acceptance Criteria:**
- Development, no token:
  - `GET /swagger/v1/swagger.json` → 200. `paths` contains `/api/approval-tasks`, `components.securitySchemes.bearer.scheme == "bearer"`, and the top-level `security` contains a `bearer` requirement.
  - `POST /api/approval-tasks` documents a `201` response and no `200` response.
  - `GET /swagger/index.html` → 200.
- The controller declares its real response codes with `[ProducesResponseType]`:
  - Create: 201 / 400 / 401 / 403.
  - Actions: 200 / 400 / 401 / 403 / 404 / 409.
  - GetById: 200 / 401 / 404.
- Production (with a valid signing key and a valid token): `GET /swagger/v1/swagger.json` → 404.
- `launchSettings.json` profiles `http` and `https` have `"launchBrowser": true` and `"launchUrl": "swagger"`.
- README has a «Swagger» section explaining how to test the API through it.
- Existing auth tests still pass: API endpoints still return 401 without a token. Full suite green, 0 build warnings.

- [ ] **Step 1: Write the failing tests**

Create `EdocsTestTask.Tests/Api/Swagger/SwaggerTests.cs` (CRLF line endings):

```csharp
using System.Net;
using System.Text.Json;
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Controllers;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Core.Constants;
using EdocsTestTask.Tests.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EdocsTestTask.Tests.Api.Swagger
{
    public class SwaggerTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
    {
        #region Constants

        private const string DocumentRoute = $"/swagger/{SwaggerExtensions.DocumentName}/swagger.json";

        private const string ProductionSigningKey = "production-test-signing-key-0123456789-abcdefghij";

        #endregion

        #region Tests

        [Fact]
        public async Task Development_SwaggerDocument_IsServedWithoutTokenAndDescribesBearerAuth()
        {
            var response = await factory.CreateClient().GetWithTokenAsync(DocumentRoute, null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            Assert.True(root.GetProperty("paths").TryGetProperty($"/{ApprovalTasksController.BaseRoute}", out var createPath));
            var createResponses = createPath.GetProperty("post").GetProperty("responses");
            Assert.True(createResponses.TryGetProperty("201", out _));
            Assert.False(createResponses.TryGetProperty("200", out _));
            Assert.True(root.GetProperty("components").GetProperty("securitySchemes")
                .TryGetProperty(SwaggerExtensions.BearerSchemeName, out var scheme));
            Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
            Assert.Contains(
                root.GetProperty("security").EnumerateArray(),
                requirement => requirement.TryGetProperty(SwaggerExtensions.BearerSchemeName, out _));
        }

        [Fact]
        public async Task Development_SwaggerUi_IsServedWithoutToken()
        {
            var response = await factory.CreateClient().GetWithTokenAsync("/swagger/index.html", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Production_SwaggerDocument_IsNotServed()
        {
            using var production = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] = ProductionSigningKey
                    }));
            });
            var jwtOptions = production.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
            var token = new TestJwtFactory(jwtOptions).Create("author-1", UserRoles.Author);

            // A valid token passes the fallback policy, so 404 means Swagger is not mounted (not merely unauthorized).
            var response = await production.CreateClient().GetWithTokenAsync(DocumentRoute, token);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~SwaggerTests"`
Expected: build FAILS with `CS0103`/`CS0234` (`SwaggerExtensions` does not exist).

- [ ] **Step 3: Add the package**

Run: `dotnet add EdocsTestTask.Api package Swashbuckle.AspNetCore --version 10.2.3`
Expected: `EdocsTestTask.Api.csproj` gets `<PackageReference Include="Swashbuckle.AspNetCore" Version="10.2.3" />`. A spike confirmed the solution builds with 0 warnings with this package.

- [ ] **Step 4: Implement SwaggerExtensions**

Create `EdocsTestTask.Api/Extensions/SwaggerExtensions.cs` (CRLF line endings):

```csharp
using Microsoft.OpenApi;

namespace EdocsTestTask.Api.Extensions
{
    /// <summary>
    /// Swagger (Swashbuckle) with a JWT Bearer scheme, so requests can be sent from the UI after "Authorize".
    /// </summary>
    public static class SwaggerExtensions
    {
        #region Constants

        public const string DocumentName = "v1";

        public const string BearerSchemeName = "bearer";

        private const string ApiTitle = "EdocsTestTask API";

        private const string BearerDescription =
            "Paste a JWT without the \"Bearer \" prefix (static tokens: EdocsTestTask.Api.http, README «Автентифікація»).";

        #endregion

        #region Methods

        /// <summary>
        /// Registers the OpenAPI document generator with a Bearer scheme required by every operation.
        /// </summary>
        public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(DocumentName, new OpenApiInfo { Title = ApiTitle, Version = DocumentName });
                options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = BearerSchemeName,
                    BearerFormat = "JWT",
                    Description = BearerDescription
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
                });
            });

            return services;
        }

        /// <summary>
        /// Serves /swagger/v1/swagger.json and the UI at /swagger. Call before UseAuthentication/UseAuthorization:
        /// the fallback policy also applies to requests without an endpoint and would answer 401.
        /// </summary>
        public static IApplicationBuilder UseSwaggerWithUi(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseSwagger();
            app.UseSwaggerUI();
            return app;
        }

        #endregion
    }
}
```

- [ ] **Step 5: Wire it into Program.cs**

`EdocsTestTask.Api/Program.cs` becomes:

```csharp
using EdocsTestTask.Api.Authentication;
using EdocsTestTask.Api.Extensions;
using EdocsTestTask.Api.Middleware;
using EdocsTestTask.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSwaggerWithJwt();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Development only, and before authentication (see SwaggerExtensions.UseSwaggerWithUi).
    app.UseSwaggerWithUi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program;
```

Keep the file's existing line endings.

- [ ] **Step 6: launchSettings.json**

In `EdocsTestTask.Api/Properties/launchSettings.json` add `"launchBrowser": true,` and `"launchUrl": "swagger",` to the `http` and `https` profiles, right after `"commandName": "Project",`. Leave the `Container (Dockerfile)` profile unchanged.

- [ ] **Step 7: Declare the real response codes on the controller**

Without these attributes Swagger documents only `200` for every action: the Create `201` check in the test fails, and testers would see wrong codes. In `ApprovalTasksController.cs` (which already imports `EdocsTestTask.Api.Contracts` and `Microsoft.AspNetCore.Mvc`), add the attributes under the existing `[Http...]`/`[Authorize...]` attributes of each action:

```csharp
        [HttpPost]
        [Authorize(Roles = UserRoles.Author)]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status201Created)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status403Forbidden)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> Create([FromBody] CreateApprovalTaskRequest request)
```

```csharp
        [HttpPost("{id:guid}/actions")]
        [Authorize(Roles = UserRoles.Approver)]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status409Conflict)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> ExecuteAction(Guid id, [FromBody] ExecuteActionRequest request)
```

```csharp
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType<ServiceResponse<ApprovalTaskResponse>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ServiceResponse>(StatusCodes.Status404NotFound)]
        public ActionResult<ServiceResponse<ApprovalTaskResponse>> GetById(Guid id) =>
```

Method bodies and the auth attributes are unchanged.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj --filter "FullyQualifiedName~SwaggerTests|FullyQualifiedName~AuthorizationTests|FullyQualifiedName~AuthenticationTests|FullyQualifiedName~ApprovalTasksApiTests"`
Expected: PASS (3 Swagger tests; the auth and API tests still pass).

- [ ] **Step 9: README «Swagger» section**

README.md uses LF line endings. Insert the following section right after the «Запуск» section, before `## Приклади`:

```markdown
## Swagger

Через Swagger UI можна протестувати всі ендпоінти без curl і `.http`-файлу. Він доступний лише в Development: профілі `http`/`https` з `launchSettings.json` задають це середовище, а Visual Studio і Rider відкривають `/swagger` автоматично.

1. Запустіть API (`dotnet run --project EdocsTestTask.Api`) і відкрийте `http://localhost:5113/swagger`.
2. Натисніть **Authorize** і вставте токен **без** префікса `Bearer`, наприклад `@authorToken` з `EdocsTestTask.Api/EdocsTestTask.Api.http` (див. «Автентифікація»).
3. `POST /api/approval-tasks` → **Try it out** → **Execute**. Скопіюйте `id` з відповіді.
4. **Authorize → Logout**, потім авторизуйтеся з `@approverToken` (`approver-1`). Виконайте `POST /api/approval-tasks/{id}/actions` з `{"action":"Start"}`, а потім з `Approve` або `Reject` (для `Reject` потрібен `comment`).
5. `GET /api/approval-tasks/{id}` працює з будь-яким токеном.

Для кожної дії Swagger показує реальні коди відповідей: `201`/`200` і помилки `400`/`401`/`403`/`404`/`409` у конверті `ServiceResponse`.
```

- [ ] **Step 10: Full verification and manual smoke check**

Run `dotnet build EdocsTestTask.slnx` (0 warnings) and `dotnet test EdocsTestTask.slnx` (all green, 1 skipped).

Manual smoke check:
1. Start the app in the background: `dotnet run --project EdocsTestTask.Api --launch-profile http`.
2. Check that `curl -s -o /dev/null -w "%{http_code}" http://localhost:5113/swagger/index.html` returns `200` and `curl -s -o /dev/null -w "%{http_code}" http://localhost:5113/api/approval-tasks/00000000-0000-0000-0000-000000000001` returns `401`.
3. Stop the app and confirm that nothing is listening on port 5113.

- [ ] **Step 11: Commit**

```bash
git add EdocsTestTask.Api/EdocsTestTask.Api.csproj EdocsTestTask.Api/Extensions/SwaggerExtensions.cs EdocsTestTask.Api/Program.cs EdocsTestTask.Api/Properties/launchSettings.json EdocsTestTask.Api/Controllers/ApprovalTasksController.cs EdocsTestTask.Tests/Api/Swagger/SwaggerTests.cs README.md
git commit -m "feat(api): add Swagger UI with JWT Authorize in Development"
```

---

### Task 7: Remove dead code

Requested by the user after the stress-test. The items are unused in production code: the validation exception interceptor (`ValidationResultExtensions.Check` catches rule exceptions), `ValidationResultExtensions.Validate`/`ValidateMany`, `PagedResponse<T>`, `DescriptionMinLength`/`DescriptionMaxLength`, and the `IConfiguration` parameter of `AddInfrastructure`. Removing them also orphans the following, which go too: `ValidationResult.Exception`/`SetException`, `ValidationErrorCodes.Exception`, `ValidationResultExtensionsTests`, and the `Microsoft.Extensions.Configuration.Abstractions` package in Infrastructure. The base-architecture skill's Validation section is updated to match the code (user decision).

No behavior changes, so the existing suite is the safety net.

**Files:**
- Delete: `EdocsTestTask.Shared/Validation/ValidationResultExtensions.cs`
- Delete: `EdocsTestTask.Tests/Shared/ValidationResultExtensionsTests.cs`
- Delete: `EdocsTestTask.Api/Contracts/PagedResponse.cs`
- Modify: `EdocsTestTask.Shared/Validation/ValidationResult.cs`
- Modify: `EdocsTestTask.Shared/Validation/ValidationErrorCodes.cs`
- Modify: `EdocsTestTask.Core/Constants/ValidationConstants.cs`
- Modify: `EdocsTestTask.Infrastructure/DependencyInjection.cs`
- Modify: `EdocsTestTask.Infrastructure/EdocsTestTask.Infrastructure.csproj`
- Modify: `EdocsTestTask.Api/Program.cs`
- Modify: `.claude/skills/base-architecture/SKILL.md`

**Interfaces:**
- Consumes: the state after Task 6.
- Produces:
  - `static IServiceCollection AddInfrastructure(this IServiceCollection services)` (no `IConfiguration`).
  - `ValidationResult` keeps `IsValid`, `Errors`, `AddError(string message, string code = ValidationErrorCodes.Invalid)`, `AddError(Error error)`, `ToResult()` and `ToFailure<T>()`.

**Acceptance Criteria:**
- `git grep -n -E "PagedResponse|ValidationResultExtensions|ValidateMany|SetException|DescriptionM(in|ax)Length|ValidationErrorCodes\.Exception|Configuration\.Abstractions" -- "*.cs" "*.csproj" "*.md" ":!.internal"` prints nothing. `.internal` holds historical specs and plans, so it is excluded.
- `git grep -n "IConfiguration" -- EdocsTestTask.Infrastructure` prints nothing. `Program.cs` calls `builder.Services.AddInfrastructure();`.
- The `SKILL.md` Validation section describes `AddError` plus the reusable checks in `Task.Core/Helpers`. It has no `ValidateMany`, no "stored exception" logging and no Description example. The `PagedResponse` guidance in «Endpoint ServiceResponse» stays as is, because it is conditional on paged lists.
- Full suite green (fewer tests: `ValidationResultExtensionsTests` is gone, the rest unchanged), 0 build warnings.

- [ ] **Step 1: Delete the unused files**

```bash
git rm EdocsTestTask.Shared/Validation/ValidationResultExtensions.cs EdocsTestTask.Tests/Shared/ValidationResultExtensionsTests.cs EdocsTestTask.Api/Contracts/PagedResponse.cs
```

- [ ] **Step 2: Trim ValidationResult and ValidationErrorCodes**

`ValidationResult.cs`:
- Change the class summary's second line from `Build it with <see cref="ValidationResultExtensions"/>.` to `Build it with <see cref="AddError(string, string)"/> / <see cref="AddError(Error)"/>.`
- Delete the `Exception` property with its XML comment, and delete the `SetException` method.

`ValidationErrorCodes.cs`: delete `public const string Exception = "Validation.Exception";` and the blank line before it. Keep `Invalid`, which is the default code of `AddError(string, string)`.

- [ ] **Step 3: ValidationConstants**

Delete the whole `#region Description ... #endregion` block, including the blank line before it, so the class holds only the `Title` and `ShortText` regions.

- [ ] **Step 4: AddInfrastructure without IConfiguration**

`EdocsTestTask.Infrastructure/DependencyInjection.cs`:
- Remove `using Microsoft.Extensions.Configuration;`.
- Change the signature to `public static IServiceCollection AddInfrastructure(this IServiceCollection services)`.
- Delete `ArgumentNullException.ThrowIfNull(configuration);`.

`EdocsTestTask.Infrastructure/EdocsTestTask.Infrastructure.csproj`: delete `<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.12" />`.

`EdocsTestTask.Api/Program.cs`: `builder.Services.AddInfrastructure(builder.Configuration);` → `builder.Services.AddInfrastructure();`.

- [ ] **Step 5: Align the base-architecture Validation section**

In `.claude/skills/base-architecture/SKILL.md` (CRLF), replace everything from `## Validation` up to (not including) `## Design and delivery` with:

````markdown
## Validation

- Put validation in the **service's Helper** class (static methods); cover every case and report every failing field.
- Return `ValidationResult`; add failures with `AddError(message, code)` (or `AddError(Error)` for a non-validation failure such as Conflict). Convert with `ToFailure<T>()` / `ToResult()`.
- Reusable field checks live in **`Task.Core/Helpers`** (e.g. `TextValidationHelper.IsTitleValid` / `IsShortTextValid` / `IsRequiredTextValid`): each returns `bool` with an `out` client-facing message. Limits live in `Task.Core.Constants`; messages and error codes are constants too.

### Default field rules (unless feature overrides—document override in XML comment on the constant)

**Title:** required; trimmed length 3–30.

```csharp
if (!TextValidationHelper.IsTitleValid(command.Title, out var titleError))
{
    validation.AddError(titleError, Messages.Codes.InvalidTitle);
}
```

**Short text** (identifiers such as a document number or user id): required; trimmed length up to 128 (`TextValidationHelper.IsShortTextValid`).

````

- [ ] **Step 6: Full verification**

1. Run `dotnet build EdocsTestTask.slnx`. Expected: 0 warnings, 0 errors.
2. Run `dotnet test EdocsTestTask.slnx`. Expected: all green, 1 skipped.
3. Run the two `git grep` checks from the Acceptance Criteria. Expected: both print nothing.

- [ ] **Step 7: Commit**

```bash
git add -A EdocsTestTask.Shared EdocsTestTask.Tests/Shared EdocsTestTask.Api EdocsTestTask.Core/Constants/ValidationConstants.cs EdocsTestTask.Infrastructure .claude/skills/base-architecture/SKILL.md
git commit -m "chore: remove unused validation extensions, PagedResponse, description limits and AddInfrastructure configuration"
```

---

## Spec coverage

| Spec requirement | Task |
|---|---|
| §2 primary constructors (all listed files, exclusions) | 5 |
| §2 base-architecture rule | 5 (Step 7) |
| §3 `Helper.Validate` overloads, wrappers removed | 3 |
| §4 transition table `ResolveNextStatus`, States/ + factory + DI + state tests removed | 4 |
| §5 `ApprovalTaskFactory` + service uses it | 2, 3 |
| §6 `ShortTextMaxLength`, `FieldTooLong`, `IsShortTextValid`, DocumentNumber and AssigneeId via it, `TitleMaxLength = 256` | 1, 3 |
| §7 Swashbuckle 10.2.3, extensions, Program order, Development only, launchSettings, `[ProducesResponseType]` | 6 |
| §8 README: Swagger section | 6 |
| §8 README: «Обробка станів» rewritten | 4 |
| §8 README: field limits, known limitations (+ auth list dedupe) | 3 |
| §9 tests | 1, 2, 3, 4, 6 (refactors covered by the existing suite in 5, 7) |
| §10 dead code removal + skill Validation section | 7 |

## Stress Test Results: bugfixing batch plan

### Resolved Decisions
- **Security (mandatory):** Swagger is mounted before authentication, only in Development. The spike showed that API endpoints still return 401 without a token and that only `/swagger/*` is served anonymously. Production is checked by a 404-with-valid-token test. No regression.
- **State pattern vs. transition table** (raised by the user mid-review): every claim was confirmed in code. There were 9 files for 3 transitions, the rules were duplicated in `Validate` and `GetNextStatus`, `Apply` without `Validate` allowed `Reject` with no comment, and the integrity guarantees come from the lock and the copies. Decision: one `ApprovalTaskServiceHelper.ResolveNextStatus` table (public static, in the Helper for direct table tests). The States hierarchy is deleted. Behavior and error codes are unchanged.
- **AssigneeId length:** it was unbounded, so an Author could store megabytes per task. It is now limited to 128 through `IsShortTextValid`. `comment` stays unbounded; it is a documented limitation.
- **Swagger response codes:** Swagger documented only `200`. `[ProducesResponseType]` with the real codes (201/200 + 400/401/403/404/409) replaces the README disclaimer, and the test asserts `201`.
- Self-resolved from code or the spike: no build-time OpenAPI generation (0 warnings); CS9124 handled in tests; the constructor rule does not conflict with `ThrowIfNull` on methods; `Helper.Validate` overloads are never called with a null literal; factory guards are sufficient because the service validates first; README edits across tasks are sequential; the Production test supplies the signing key like `AuthenticationTests`.

### Changes Made
- New Task 4 (transition table). The old Tasks 4 and 5 became 5 and 6.
- Task 3: AssigneeId goes through `IsShortTextValid`, with a new test. The README states wording moved to Task 4.
- Task 5: no `ApprovalTaskStateFactory` step. The service's primary constructor is `(repository, timeProvider)`.
- Task 6: `[ProducesResponseType]` step, a 201 assertion, and the README note removed.
- Spec updated: §1, §2, §4 (new), §6, §7, §8, §9.
- After the stress-test the user added the dead-code removal → Task 7 and spec §10. Its skill-doc question was resolved as "align the Validation section with the code".

### Deferred / Parking Lot
- `comment` length limit (documented in README).
- `AI_USAGE.md` (TEST_TASK §05), not part of this batch.

### Confidence Assessment
- Overall: High. Every change is either a pure refactor covered by the existing suite or behavior with a RED test first. The Swashbuckle API and pipeline order were verified by a spike.
- Areas of concern: Task 4 deletes 10 files. The Step 7 `git grep` guard and the unchanged service/API/concurrency tests are the safety net.
