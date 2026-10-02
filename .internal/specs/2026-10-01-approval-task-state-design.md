# Approval Task — State pattern design

> **Superseded (2026-10-02):** State pattern replaced by a single transition table, see .internal/specs/2026-10-02-bugfixing-batch-design.md §4.

**Дата:** 2026-10-01
**Статус:** затверджено в brainstorming, очікує рев'ю spec
**Залежність:** виконується ПІСЛЯ плану `.internal/plans/2026-10-01-jwt-auth.md` (актор береться з claim `sub`, ролі `Author`/`Approver`).
**Джерело вимог:** `docs/TEST_TASK.md`.

## 1. Рішення

| Рішення | Вибір |
|---|---|
| Обробка станів | State pattern: stateless класи `IApprovalTaskState` на кожен статус + `ApprovalTaskStateFactory` |
| Валідація | Кожен state має `Validate(...)`; оркестратор (`ApprovalTaskService`) завжди має власний `Validate(...)` для вхідної команди |
| Сховище | `InMemoryApprovalTaskRepository` (`ConcurrentDictionary`), повертає клони |
| Title | `TextValidationHelper.IsTitleValid(command.Title, out var error)` при Create; не null/пробіли, довжина після `Trim` у межах `ValidationConstants.TitleMinLength..TitleMaxLength` = 3..200 |
| Повідомлення | Усі тексти помилок — `const string` в окремому файлі `ApprovalTaskMessages.cs` |
| Конкурентність | Per-task `lock` усередині сервісу (в межах одного процесу, як дозволяє ТЗ) |
| Атомарність | Зміни застосовуються до клону; збереження лише після успішного `Apply` |

Відкинуті: GoF-state всередині сутності (ускладнює атомарність/клонування), таблиця переходів (не State pattern).

## 2. Компоненти

| Шар | Файл | Відповідальність |
|---|---|---|
| Core/Entities | `ApprovalTask.cs` | POCO: `Id`, `DocumentNumber`, `Title`, `AssigneeId`, `Status`, `CreatedAtUtc`, `UpdatedAtUtc`, `List<ApprovalHistoryEntry> History`; `Clone()` (глибока копія History) |
| Core/Entities | `ApprovalHistoryEntry.cs` | `Action`, `ActorId`, `Comment?`, `AtUtc` (immutable record) |
| Core/Enums | `ApprovalStatus.cs`, `ApprovalAction.cs` | `Assigned, InProgress, Approved, Rejected`; `Start, Approve, Reject` |
| Core/Constants | `ApprovalTaskMessages.cs` | Константи повідомлень (див. §5) |
| Core/Constants | `ValidationConstants.cs` | `TitleMaxLength` 30 → 200 |
| Core/Helpers | `TextValidationHelper.cs` (у Core, бо використовує `ValidationConstants` і `ApprovalTaskMessages`; Shared не може посилатися на Core) | `IsTitleValid(string? title, out string? error)`, `IsRequiredTextValid(string? value, string fieldName, out string? error)` |
| Core/Interfaces/Repositories | `IApprovalTaskRepository.cs` | `ApprovalTask? GetById(Guid id)`, `void Add(ApprovalTask task)`, `void Update(ApprovalTask task)` |
| Core/Interfaces/Services | `IApprovalTaskService.cs` | `Result<ApprovalTask> Create(CreateApprovalTaskCommand)`, `Result<ApprovalTask> ExecuteAction(Guid id, ExecuteActionCommand)`, `Result<ApprovalTask> GetById(Guid id)` |
| Core/Commands | `CreateApprovalTaskCommand`, `ExecuteActionCommand` | `DocumentNumber, Title, AssigneeId`; `ActorId, Action (string?), Comment?` |
| Infrastructure/States | `IApprovalTaskState.cs`, `ApprovalTaskStateContext.cs` | `Status`; `ValidationResult Validate(ApprovalTask task, ApprovalTaskStateContext ctx)`; `void Apply(ApprovalTask task, ApprovalTaskStateContext ctx)`. Context: `Action`, `ActorId`, `Comment`, `NowUtc` |
| Infrastructure/States | `AssignedState`, `InProgressState`, `ApprovedState`, `RejectedState` | Див. §3 |
| Infrastructure/States | `ApprovalTaskStateFactory.cs` | `IApprovalTaskState Get(ApprovalStatus)`; невідомий статус → `InvalidOperationException` (баг, не бізнес-помилка) |
| Infrastructure/Repositories | `InMemoryApprovalTaskRepository.cs` | Singleton; зберігає й віддає клони |
| Infrastructure/Services | `ApprovalTaskService.cs` | Оркестратор; `TimeProvider` для часу |
| Api/Controllers | `ApprovalTasksController.cs` | 3 ендпоінти; `[Authorize(Roles = UserRoles.Author)]` на Create, `Approver` на Actions; GET — будь-який автентифікований; `actorId` з `sub` |
| Api/Contracts | `CreateApprovalTaskRequest`, `ExecuteActionRequest` (`Action`, `Comment?`), `ApprovalTaskResponse` | DTO |

## 3. State-класи

`Validate` повертає `ValidationResult`, помилки якого мають правильний `ErrorType` (додається overload `AddError(Error)` у `ValidationResult`, якщо потрібно для не-Validation типів).

| State | Дозволена дія | Validate | Apply |
|---|---|---|---|
| `AssignedState` | `Start` | інша дія → Conflict `InvalidTransition` | `Status = InProgress` |
| `InProgressState` | `Approve`, `Reject` | `Start` → Conflict `InvalidTransition`; `Reject` з порожнім/пробільним коментарем → Validation `RejectCommentRequired` | `Status = Approved`/`Rejected` |
| `ApprovedState` / `RejectedState` | — | будь-яка дія → Conflict `TaskAlreadyFinalized` | `InvalidOperationException` (не має викликатися) |

Спільне в `Apply` (базовий абстрактний клас `ApprovalTaskStateBase`): `UpdatedAtUtc = ctx.NowUtc`, додати `ApprovalHistoryEntry(ctx.Action, ctx.ActorId, ctx.Comment?.Trim(), ctx.NowUtc)`.

## 4. Data flow

**Create** (`POST /api/approval-tasks` → `201`):
1. `Validate(command)`: `TextValidationHelper.IsTitleValid(command.Title, out var error)`; `IsRequiredTextValid` для `DocumentNumber`, `AssigneeId`. Помилки → `400`.
2. `now = timeProvider.GetUtcNow()`; нова задача `Status = Assigned`, `CreatedAtUtc = UpdatedAtUtc = now`, `History = []`, текстові поля `Trim`.
3. `repository.Add`; повернути задачу.

**ExecuteAction** (`POST /api/approval-tasks/{id}/actions` → `200`):
1. `Validate(command)`: `Action` присутня й парситься в `ApprovalAction` (case-insensitive, лише визначені імена, не числа) → інакше `400` (`ActionRequired` / `UnknownAction`); `ActorId` не порожній.
2. Якщо задачі немає → `404` ще до lock-у (невідомі id не роздувають словник lock-ів). Далі `lock (_taskLocks.GetOrAdd(id, ...))` — `ConcurrentDictionary<Guid, Lock>`:
   1. `task = repository.GetById(id)` → `null` → `404 TaskNotFound`.
   2. `task.AssigneeId != command.ActorId` (ordinal) → `403 NotAssignee`.
   3. `state = factory.Get(task.Status)`; `state.Validate(task, ctx)` → помилка → повернути без змін.
   4. `state.Apply(task, ctx)` (задача вже копія з репозиторію); `repository.Update(task)`.
3. Повернути `task`.

Порядок перевірок фіксований: 400 (команда) → 404 → 403 → 409/400 (state).

**GetById** → `200` або `404`.

Контролер використовує новий `Result<T>.Map(...)` (Shared) для перетворення в DTO. Відповіді-помилки — через наявні `ResultExtensions`/`ErrorResponse` (ErrorType → HTTP: Validation 400, NotFound 404, Forbidden 403, Conflict 409).

## 5. `ApprovalTaskMessages`

```csharp
public static class ApprovalTaskMessages
{
    public const string TitleRequired = "Title is required and cannot be empty or whitespace.";
    public const string TitleLength = "Title must be between {0} and {1} characters.";
    public const string FieldRequired = "{0} is required and cannot be empty or whitespace.";
    public const string ActionRequired = "Action is required.";
    public const string UnknownAction = "Unknown action '{0}'. Allowed: Start, Approve, Reject.";
    public const string TaskNotFound = "Approval task '{0}' was not found.";
    public const string NotAssignee = "Only the assigned user can perform actions on this task.";
    public const string InvalidTransition = "Action '{0}' is not allowed when task is in status '{1}'.";
    public const string RejectCommentRequired = "A non-empty comment is required to reject a task.";
    public const string TaskAlreadyFinalized = "Task is already {0}; no further actions are allowed.";
}
```

Коди помилок (`Error.Code`) — `ApprovalTask.<Name>`, також константами в тому ж файлі (вкладений клас `Codes`).

## 6. Конкурентність та цілісність

- Per-task lock серіалізує всі дії над однією задачею. Другий паралельний Approve/Reject бачить фінальний статус → `TaskAlreadyFinalized` (`409`); в історії одне рішення.
- Репозиторій віддає клони → ні контролер, ні тест не можуть змінити збережений стан поза сервісом.
- Помилка на будь-якому кроці не викликає `Update` → дані незмінні.
- Обмеження (README): lock лише в межах одного процесу; словник lock-ів не очищується (прийнятно для in-memory тестового завдання).

## 7. Тести (xUnit, без HTTP-проєкту)

- `TextValidationHelperTests`: null, `""`, `"   "`, < 3, > 200, межі 3/200, валідне з пробілами по краях.
- State unit-тести: кожен state × кожна дія (параметризовано) — очікуваний результат `Validate`; `Apply` змінює статус/історію/UpdatedAt.
- `ApprovalTaskServiceTests` (fake `TimeProvider`):
  - Create: успіх (Assigned, однакові дати, порожня історія); невалідні Title/DocumentNumber/AssigneeId → Validation.
  - Повний шлях Start → Approve і Start → Reject (з коментарем); історія з 2 записами з правильними даними.
  - Недозволені переходи, включно з будь-якою дією після Approved/Rejected → Conflict.
  - Чужий виконавець → Forbidden; Reject без/з пробільним коментарем → Validation; невідома/відсутня дія → Validation; відсутня задача → NotFound.
  - Для кожної помилки: статус, `UpdatedAtUtc`, кількість і вміст історії незмінні.
  - Конкурентність: задача в InProgress, `Task.WhenAll` з Approve і Reject (через `Barrier`), повторено N разів → рівно один успіх, один Conflict, одне фінальне рішення в історії.
