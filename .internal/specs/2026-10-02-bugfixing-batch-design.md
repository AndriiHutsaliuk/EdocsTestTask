# Bugfixing batch: primary constructors, Helper.Validate, ApprovalTaskFactory, таблиця переходів, валідація тексту, Swagger, README

**Дата:** 2026-10-02
**Статус:** дизайн затверджено в brainstorming; spec затверджено; оновлено за результатами stress-test плану і запиту на видалення мертвого коду (§10)
**Базується на:** `.internal/specs/2026-10-01-approval-task-state-design.md` (реалізовано, HEAD `576155f`). Пункт «State pattern» звідти **скасовано** цим spec (див. §4).

## 1. Рішення

| Рішення | Вибір |
|---|---|
| Конструктори | C# primary constructors; параметри використовуються напряму (без `ThrowIfNull` і без присвоєння в `_field`) |
| Валідація в оркестраторі | Лише `Helper.Validate(command)`; приватні обгортки `Validate(...)` у сервісі видаляються. Замінює правило «оркестратор завжди має власний Validate» з попереднього spec |
| Створення задачі | Статичний `ApprovalTaskFactory.Create(command, nowUtc)` у `EdocsTestTask.Core/Factories/` |
| Переходи статусів | Одна таблиця `ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment)` → `Result<ApprovalStatus>`; ієрархія `States/` (9 файлів) і `ApprovalTaskStateFactory` видаляються |
| DocumentNumber, AssigneeId | `TextValidationHelper.IsShortTextValid` — обов'язкове, обрізана довжина ≤ 128 |
| Title | `ValidationConstants.TitleMaxLength` 200 → 256 (мінімум 3 без змін) |
| Swagger | Swashbuckle.AspNetCore 10.2.3, лише в Development, Bearer-схема (кнопка Authorize); `[ProducesResponseType]` на діях контролера |
| README | Нові пункти — у «Відомі обмеження (approval tasks)», окремого розділу «Нюанси» немає |

## 2. Primary constructors

Переводяться на primary constructor (тіла методів використовують параметр замість `_field`):

| Файл | Було | Стане |
|---|---|---|
| `Api/Controllers/ApprovalTasksController.cs` | ctor + `_service` | `ApprovalTasksController(IApprovalTaskService service)` |
| `Api/Middleware/GlobalExceptionHandler.cs` | ctor + `_logger` | `GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)` |
| `Infrastructure/Services/ApprovalTasks/ApprovalTaskService.cs` | ctor + поля | `ApprovalTaskService(IApprovalTaskRepository repository, TimeProvider timeProvider)` (після §4 фабрики станів немає); поле `_taskLocks` лишається |
| `Tests/Api/Auth/TestJwtFactory.cs` | ctor + `_options` | `TestJwtFactory(JwtOptions options)` |
| `Tests/TestData/TestTimeProvider.cs` | ctor + `_utcNow = start` | `TestTimeProvider(DateTimeOffset start)`; `private DateTimeOffset _utcNow = start;` (поле змінюється в `Advance`) |
| `Tests/Api/ApprovalTasks/ApprovalTasksApiTests.cs`, `Tests/Api/Auth/AuthenticationTests.cs`, `AuthorizationTests.cs`, `StaticTokenTests.cs` | ctor з fixture | primary ctor; похідні значення — ініціалізатори полів, або властивості, якщо параметр використовується й у тестах (CS9124); прості копії параметрів (`_factory`, `_output`) прибираються |

Не змінюються (з причиною):
- `Shared/Results/Result.cs`, `ResultOfT.cs` — конструктор перевіряє інваріанти і навмисно `protected`/`private` (створення лише через фабричні методи); primary constructor не можна зробити `private`.
- `ApprovalTaskServiceTests()`, `ApprovalTaskServiceConcurrencyTests()` — без параметрів, будують поля, що залежать одне від одного (ініціалізатор поля не може посилатися на інше поле).

`.claude/skills/base-architecture/SKILL.md`, розділ «Services and structure» — нове правило:

> - **Constructors:** classes that receive dependencies use C# primary constructors and use the parameters directly — no `ArgumentNullException.ThrowIfNull` and no copying into private fields. Derived values (e.g. `factory.CreateClient()`) become field initializers; if the parameter is also used inside members, compute them in members instead (a parameter that is both captured and used in an initializer triggers warning CS9124). Keep an explicit constructor only when it validates invariants or must be non-public (e.g. `Result`), or when it has no parameters and builds fields that depend on each other.

## 3. Валідація через Helper

`ApprovalTaskServiceHelper`:
- `ValidateCreate(CreateApprovalTaskCommand)` → `Validate(CreateApprovalTaskCommand)`.
- `ValidateAction(ExecuteActionCommand)` → `Validate(ExecuteActionCommand)`.
- `TryParseAction` без змін.

`ApprovalTaskService`: видалити `private static ValidationResult Validate(...)` (обидва); виклики → `Helper.Validate(command)`.

## 4. Таблиця переходів замість State pattern

Причини (перевірено в коді): на три переходи — 9 файлів (`IApprovalTaskState`, `ApprovalTaskStateContext`, `ApprovalTaskStateFactory`, `ApprovalTaskStateBase`, `FinalApprovalTaskState`, 4 стани), `ApprovedState`/`RejectedState` лише задають `Status`; правила записані двічі (`Validate` і `GetNextStatus`); `Apply` без `Validate` дозволяє `Reject` без коментаря; гарантії цілісності дають lock і копії в репозиторії, а не стани; ТЗ (§05) оцінює зрозумілість, «велика кількість шарів не дає балів».

`ApprovalTaskServiceHelper.ResolveNextStatus(ApprovalStatus status, ApprovalAction action, string? comment)` → `Result<ApprovalStatus>`:

| Поточний статус | Дія | Результат |
|---|---|---|
| `Assigned` | `Start` | `InProgress` |
| `InProgress` | `Approve` | `Approved` |
| `InProgress` | `Reject` з непорожнім коментарем | `Rejected` |
| `Approved` / `Rejected` | будь-яка | Conflict `ApprovalTask.AlreadyFinalized` («Task is already {status}; …») |
| решта комбінацій | | Conflict `ApprovalTask.InvalidTransition` («Action '{action}' is not allowed when task is in status '{status}'.») |

Порядок перевірок усередині: фінальний статус → пара не в таблиці → `Reject` без коментаря (Validation `ApprovalTask.RejectCommentRequired`). Поведінка й коди помилок збігаються з поточною реалізацією.

`ApprovalTaskService.ExecuteValidatedAction` (всередині наявного lock): `GetById` → 404; не виконавець → 403; `Helper.ResolveNextStatus` → помилка повертається без змін даних; успіх → в одному місці `Status`, `UpdatedAtUtc = now`, `History.Add(new ApprovalHistoryEntry(action, actorId, trimmed-comment-or-null, now))` → `repository.Update`.

Видаляються: тека `Infrastructure/Services/ApprovalTasks/States/` (9 файлів), реєстрація `ApprovalTaskStateFactory` у `DependencyInjection`, `Tests/Infrastructure/ApprovalTasks/ApprovalTaskStatesTests.cs`. Конструктор сервісу: `(IApprovalTaskRepository, TimeProvider)`.

## 5. ApprovalTaskFactory

`EdocsTestTask.Core/Factories/ApprovalTaskFactory.cs`:

```csharp
public static class ApprovalTaskFactory
{
    /// Builds a new task in Assigned status from an already validated command.
    public static ApprovalTask Create(CreateApprovalTaskCommand command, DateTime nowUtc)
}
```

- `ArgumentNullException.ThrowIfNull(command)`; `ArgumentException.ThrowIfNullOrWhiteSpace` для `DocumentNumber`, `Title`, `AssigneeId` (сервіс валідує раніше, тож це guard від помилки програміста, не бізнес-помилка).
- Результат: `Id = Guid.NewGuid()`, три текстові поля `Trim()`, `Status = Assigned`, `CreatedAtUtc = UpdatedAtUtc = nowUtc`, `History = []`.
- Сервіс: `var task = ApprovalTaskFactory.Create(command, timeProvider.GetUtcNow().UtcDateTime);`.

## 6. Валідація тексту

- `ValidationConstants.ShortTextMaxLength = 128` (новий регіон `ShortText`); `TitleMaxLength = 256` (XML-коментар оновити).
- `ApprovalTaskMessages.FieldTooLong = "{0} must be {1} characters or fewer."` (`{0}` = назва поля, `{1}` = максимум).
- `TextValidationHelper.IsShortTextValid(string? value, string fieldName, [NotNullWhen(false)] out string? error)`:
  - порожній `fieldName` → `ArgumentException`;
  - null/порожнє/пробіли → `FieldRequired`;
  - обрізана довжина > 128 → `FieldTooLong` (з `fieldName`, 128);
  - інакше `true`, `error = null`.
- `Helper.Validate(CreateApprovalTaskCommand)`: `DocumentNumber` (код `ApprovalTask.InvalidDocumentNumber`) і `AssigneeId` (код `ApprovalTask.InvalidAssignee`) перевіряються через `IsShortTextValid`. `comment` лишається без обмеження довжини (задокументовано в README).

## 7. Swagger

- Пакет `Swashbuckle.AspNetCore` 10.2.3 в `EdocsTestTask.Api.csproj`.
- `Api/Extensions/SwaggerExtensions.cs`:
  - `AddSwaggerWithJwt(this IServiceCollection)` — `AddSwaggerGen` з документом `v1`, security definition `"bearer"` (`SecuritySchemeType.Http`, `Scheme = "bearer"`, `BearerFormat = "JWT"`) і глобальною вимогою через `OpenApiSecuritySchemeReference` (Microsoft.OpenApi v2, namespace `Microsoft.OpenApi`).
  - `UseSwaggerWithUi(this IApplicationBuilder)` — `UseSwagger()` + `UseSwaggerUI()`.
- `Program.cs`: `AddSwaggerWithJwt()` завжди (лише реєстрація сервісів); `UseSwaggerWithUi()` всередині `if (app.Environment.IsDevelopment())` і **до** `UseAuthentication`/`UseAuthorization` — fallback-політика застосовується й до запитів без endpoint, тож інакше `/swagger` віддавав би 401.
- `ApprovalTasksController`: `[ProducesResponseType]` з реальними кодами — Create: 201 (`ServiceResponse<ApprovalTaskResponse>`), 400/401/403 (`ServiceResponse`); Actions: 200, 400/401/403/404/409; GetById: 200, 401/404.
- `Properties/launchSettings.json`: профілі `http` і `https` — `"launchBrowser": true`, `"launchUrl": "swagger"`.
- Безпека: в Production/Docker Swagger не підключається; ендпоінти API як і раніше вимагають токен (Swagger лише описує їх і передає токен, який вставив користувач).

## 8. README

- Новий розділ «Swagger» (після «Запуск»): `dotnet run --project EdocsTestTask.Api` → `http://localhost:5113/swagger` → **Authorize** → вставити токен без префікса `Bearer` (`@authorToken` / `@approverToken` з `EdocsTestTask.Api.http`) → виконувати запити через «Try it out». Доступний лише в Development.
- «Обробка станів»: замість опису State pattern — таблиця з §4, опис `Helper.Validate` / `ApprovalTaskFactory` / `ResolveNextStatus` і коротке обґрунтування, чому стани прибрано.
- Межі полів (Title 3–256, DocumentNumber і AssigneeId до 128) — у «Приклади».
- «Відомі обмеження (approval tasks)»:
  - новий пункт: можна створити кілька задач з однаковим `Title` (і однаковим `DocumentNumber`) — унікальність не перевіряється; ТЗ не визначає, чи це дозволено, питання відкрите;
  - пункт про `assigneeId` доповнити: під час Create не перевіряється існування користувача; окремої бази користувачів у тестовому проєкті немає — користувачі існують лише як claims у статичних токенах.
- «Автентифікація → Відомі обмеження»: пункт 3 (дублікат про `assigneeId`) видалити, решту перенумерувати.

## 9. Тести

- `TextValidationHelperTests`: межі Title 256 / 257 і повідомлення `"Title must be between 3 and 256 characters."`; `TitleMaxLength_Is256`; `IsShortTextValid`: null/`""`/пробіли → `FieldRequired`; 128 → true; 129 → `"DocumentNumber must be 128 characters or fewer."`; 128 символів з пробілами по краях → true; порожній `fieldName` → `ArgumentException`.
- `ApprovalTaskFactoryTests`: поля обрізані; `Assigned`; `CreatedAtUtc == UpdatedAtUtc == nowUtc`; порожня історія; `Id != Guid.Empty` і різний для двох викликів; null-команда → `ArgumentNullException`; порожнє поле → `ArgumentException`.
- `ApprovalTaskServiceTests`: Create з `DocumentNumber` довжиною 129 → Validation `ApprovalTask.InvalidDocumentNumber` з повідомленням `"DocumentNumber must be 128 characters or fewer."`; рівно 128 → успіх; `AssigneeId` довжиною 129 → Validation `ApprovalTask.InvalidAssignee`; `Title` 257 → `ApprovalTask.InvalidTitle`; Approve з коментарем із пробілів → в історії `Comment == null`.
- `ApprovalTaskServiceHelperTests` (замість `ApprovalTaskStatesTests`): три дозволені переходи; кожна недозволена комбінація з кодом/типом помилки (включно з порядком: `Approved`+`Reject` без коментаря → `AlreadyFinalized`, `Assigned`+`Reject` без коментаря → `InvalidTransition`); тексти повідомлень InvalidTransition / AlreadyFinalized; з усіх пар статус×дія успішні рівно три.
- `SwaggerTests` (через `AuthApiFactory`, Development): `GET /swagger/v1/swagger.json` без токена → 200, тіло містить `/api/approval-tasks`, схему `bearer` і відповідь `201` (не `200`) для `POST /api/approval-tasks`; `GET /swagger/index.html` без токена → 200. Production (`WithWebHostBuilder` + `UseEnvironment(Production)` + валідний `Jwt:SigningKey`): `GET /swagger/v1/swagger.json` з валідним токеном → 404.
- Рефакторинги (primary ctors, перейменування) покриваються наявними тестами; весь набір має лишатися зеленим, збірка — без попереджень.

## 10. Видалення мертвого коду (додано користувачем після stress-test)

Видаляється те, що не використовується в робочому коді:
- `Shared/Validation/ValidationResultExtensions.cs` (`Validate`/`ValidateMany` і перехоплювач винятків у `Check`) разом із `Tests/Shared/ValidationResultExtensionsTests.cs`; як наслідок — `ValidationResult.Exception`/`SetException` і `ValidationErrorCodes.Exception`.
- `Api/Contracts/PagedResponse.cs` — ендпоінтів із пагінацією немає.
- `ValidationConstants.DescriptionMinLength`/`DescriptionMaxLength` — поля Description у задачі немає.
- Параметр `IConfiguration` у `AddInfrastructure` (виклик стає `AddInfrastructure()`) і пакет `Microsoft.Extensions.Configuration.Abstractions` в Infrastructure.

`base-architecture/SKILL.md`, розділ Validation, узгоджується з кодом: `ValidationResult` + `AddError`, перевикористовувані перевірки в `Core/Helpers` (`TextValidationHelper`); без `ValidateMany`, логування «stored exception» і прикладу Description. Рекомендація про `PagedResponse` у «Endpoint ServiceResponse» лишається (умовна — лише для пагінованих списків).
