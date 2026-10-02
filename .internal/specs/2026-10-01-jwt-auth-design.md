# JWT Auth — дизайн

**Дата:** 2026-10-01
**Статус:** затверджено в brainstorming, очікує рев'ю spec
**Обсяг:** Auth-інфраструктура для `EdocsTestTask.Api` + тести. Сам `ApprovalTasksController` не входить — він отримає атрибути з матриці доступу (розділ 4) у плані фічі approval-tasks.

## 1. Мета і рішення

Додати автентифікацію статичними JWT і авторизацію за двома ролями через `[Authorize]`.

| Рішення | Вибір |
|---|---|
| Механізм | `Microsoft.AspNetCore.Authentication.JwtBearer`, HS256, симетричний ключ із конфігурації |
| Авторизація | `[Authorize(Roles = UserRoles.X)]` на ендпоінтах + `FallbackPolicy = RequireAuthenticatedUser` (secure-by-default) |
| Ролі | `Author` — створює задачі; `Approver` — виконує дії над призначеними йому задачами |
| Поточний користувач | Лише з claim `sub` токена. Поле `actorId` прибирається з тіла `POST /api/approval-tasks/{id}/actions` |
| Токени | 3 заздалегідь згенеровані токени, `exp = 2027-10-01T00:00:00Z`, закомічені в `EdocsTestTask.Api.http` і README |
| Валідація | Повна: підпис, issuer, audience, lifetime, алгоритм. «Статичні» ≠ «без перевірки» |

**Відхилення від ТЗ (свідоме):** ТЗ (`docs/TEST_TASK.md`, §01–02) каже, що `actorId` у запиті імітує поточного користувача. Ми беремо актора з токена: два джерела ідентичності без зв'язку дозволили б будь-якому автентифікованому користувачу діяти від імені іншого. README пояснює це в розділі рішень.

## 2. Компоненти

| Файл | Відповідальність |
|---|---|
| `EdocsTestTask.Api/EdocsTestTask.Api.csproj` | + `PackageReference Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x (версія, узгоджена з іншими 10.0.x пакетами рішення) |
| `EdocsTestTask.Core/Constants/UserRoles.cs` | `public const string Author = "Author"; public const string Approver = "Approver";` + `IReadOnlySet<string> All` для перевірки «відома роль» |
| `EdocsTestTask.Api/Authentication/JwtOptions.cs` | `SectionName = "Jwt"`; `Issuer`, `Audience`, `SigningKey` — `string` зі значенням за замовчуванням `""` (обов'язковість перевіряє валідатор, а не ключове слово `required`, щоб не ламати options-binder) |
| `EdocsTestTask.Api/Authentication/JwtOptionsValidator.cs` | `IValidateOptions<JwtOptions>`, запускається через `ValidateOnStart`: усі три значення непорожні, `Encoding.UTF8.GetByteCount(SigningKey) >= 32` |
| `EdocsTestTask.Api/Authentication/AuthClaimTypes.cs` | `Subject = "sub"`, `Role = "role"` — імена claim-ів без мапінгу |
| `EdocsTestTask.Api/Authentication/AuthenticationExtensions.cs` | `AddJwtAuthentication(this IServiceCollection, IConfiguration)`: options + `ValidateOnStart`, JwtBearer, події, `AddAuthorizationBuilder().SetFallbackPolicy(...)` |
| `EdocsTestTask.Api/Authentication/AuthErrorCodes.cs` | `Unauthorized = "Auth.Unauthorized"`, `Forbidden = "Auth.Forbidden"` |
| `EdocsTestTask.Api/Authentication/ClaimsPrincipalExtensions.cs` | `string GetUserId(this ClaimsPrincipal)` — повертає `sub`; якщо його немає, кидає `InvalidOperationException` (помилка програміста: ендпоінт без `[Authorize]`), яку `GlobalExceptionHandler` перетворює на 500 |
| `EdocsTestTask.Api/Program.cs` | `builder.Services.AddJwtAuthentication(builder.Configuration);` `app.UseAuthentication();` перед `app.UseAuthorization();` `app.MapHealthChecks("/health").AllowAnonymous();` |
| `EdocsTestTask.Api/appsettings.json` | `"Jwt": { "Issuer": "EdocsTestTask", "Audience": "EdocsTestTask.Api" }` — без ключа |
| `EdocsTestTask.Api/appsettings.Development.json` | `"Jwt": { "SigningKey": "EdocsTestTask-DEV-ONLY-jwt-signing-key-2026-do-not-use-in-prod" }` (62 байти UTF-8; назва сама каже, що ключ тестовий) |
| `EdocsTestTask.Api/EdocsTestTask.Api.http` | змінні `@authorToken`, `@approverToken`, `@otherApproverToken` (по одній на рядок, формат `@name = value`) |
| `README.md` | розділ «Автентифікація»: таблиця користувачів/ролей/токенів, приклад заголовка, `Jwt__SigningKey` для не-Development, регенерація токенів, відомі обмеження (розділ 8). Якщо README ще немає — створюється з цим розділом |

## 3. Параметри валідації токена

```text
MapInboundClaims          = false            // sub і role лишаються з цими іменами
TokenValidationParameters:
  ValidateIssuer           = true,  ValidIssuer   = Jwt:Issuer
  ValidateAudience         = true,  ValidAudience = Jwt:Audience
  ValidateLifetime         = true            // ClockSkew — за замовчуванням (5 хв)
  ValidateIssuerSigningKey = true,  IssuerSigningKey = SymmetricSecurityKey(UTF8(Jwt:SigningKey))
  ValidAlgorithms          = [ HS256 ]       // відсікає alg:none і підміну алгоритму
  RequireSignedTokens      = true            // за замовчуванням, не вимикати
  RequireExpirationTime    = true            // за замовчуванням, не вимикати
  NameClaimType            = "sub"
  RoleClaimType            = "role"
```

Події `JwtBearerEvents`:

- **`OnTokenValidated`** — якщо `sub` відсутній або порожній, або значення `role` не входить до `UserRoles.All` → `context.Fail(...)` (далі 401).
- **`OnChallenge`** — `context.HandleResponse()`; статус 401; заголовок `WWW-Authenticate: Bearer` (додається вручну, бо `HandleResponse` пригнічує стандартний); тіло `ServiceResponse.Fail([new ErrorResponse(AuthErrorCodes.Unauthorized, "Authentication is required. Provide a valid Bearer token.")])`. Причину відмови (`context.AuthenticateFailure`) логуємо з `TraceIdentifier`, клієнту не повертаємо.
- **`OnForbidden`** — статус 403; тіло `ServiceResponse.Fail([new ErrorResponse(AuthErrorCodes.Forbidden, "You do not have permission to perform this action.")])`.

## 4. Матриця доступу (контракт для фічі approval-tasks)

| Ендпоінт | Атрибут | Актор |
|---|---|---|
| `POST /api/approval-tasks` | `[Authorize(Roles = UserRoles.Author)]` | — |
| `POST /api/approval-tasks/{id}/actions` | `[Authorize(Roles = UserRoles.Approver)]` | `User.GetUserId()` → параметр `actorId` сервісу |
| `GET /api/approval-tasks/{id}` | `[Authorize]` | — |
| `GET /health` | `.AllowAnonymous()` | — |

Сервіс не залежить від JWT: він отримує `actorId` як `string` і сам перевіряє `assigneeId == actorId` (бізнес-правило ТЗ → `Error.Forbidden` → 403). `assigneeId` у задачі — `string`, щоб збігатися з `sub`.

## 5. Статичні користувачі й токени

| `sub` | `role` | Змінна в `.http` |
|---|---|---|
| `author-1` | `Author` | `@authorToken` |
| `approver-1` | `Approver` | `@approverToken` |
| `approver-2` | `Approver` | `@otherApproverToken` — «чужий» виконавець для сценарію 403 |

Claims кожного токена: `sub`, `role`, `iss`, `aud`, `iat`, `nbf`, `exp = 2027-10-01T00:00:00Z` (Unix `1822348800`). Один токен — одна роль.

Токени генеруються `TestJwtFactory` (розділ 7) через тест `[Fact(Skip = "Manual: remove Skip to print fresh static tokens")]`, що друкує їх у `ITestOutputHelper`. Згенеровані рядки вставляються в `.http` і README вручну. Процедура описана в README.

## 6. Обробка помилок

| Ситуація | Хто відповідає | HTTP | Код |
|---|---|---|---|
| Немає токена / сміття / чужий ключ / протермінований / невірний iss або aud / `alg: none` / без `sub` / невідома роль | JwtBearer `OnChallenge` | 401 | `Auth.Unauthorized` |
| Валідний токен, роль не підходить | JwtBearer `OnForbidden` | 403 | `Auth.Forbidden` |
| Ендпоінт без явного атрибута, немає токена | FallbackPolicy → `OnChallenge` | 401 | `Auth.Unauthorized` |
| Роль правильна, але актор не є assignee | сервіс фічі → `Error.Forbidden` | 403 | доменний код |

Усі відповіді — у конверті `ServiceResponse` (`EdocsTestTask.Api/Contracts/ServiceResponse.cs`), як решта API.

## 7. Тестування

Проєкт `EdocsTestTask.Tests`, тека `Api/Auth/`, простір імен `EdocsTestTask.Tests.Api.Auth`. Нова залежність: `Microsoft.AspNetCore.Mvc.Testing` 10.0.x. Файл `EdocsTestTask.Api/EdocsTestTask.Api.http` лінкується в output тестів (`<None Include=... Link=... CopyToOutputDirectory="PreserveNewest" />`).

| Файл | Призначення |
|---|---|
| `AuthProbeController.cs` | Лише в тестовій збірці. `[ApiController] [Route("test/auth-probe")]`; дії: `GET author` (`[Authorize(Roles = UserRoles.Author)]`), `GET approver` (`[Authorize(Roles = UserRoles.Approver)]`), `GET any` (`[Authorize]`, повертає `ServiceResponse<string>` з `User.GetUserId()`), `GET fallback` (без атрибутів) |
| `AuthApiFactory.cs` | `WebApplicationFactory<Program>`, `UseEnvironment("Development")` (реальний тестовий ключ), `ConfigureTestServices(s => s.AddControllers().AddApplicationPart(typeof(AuthProbeController).Assembly))` |
| `TestJwtFactory.cs` | Створює токен із заданими `sub`, `role`, `exp`, `iss`, `aud`, ключем; окремий метод для непідписаного `alg: none` токена |
| `AuthenticationTests.cs` | HTTP-тести нижче |
| `StaticTokenTests.cs` | Перевірка закомічених токенів + skip-тест генерації |

Кейси:

1. **401 + `Auth.Unauthorized` + `WWW-Authenticate: Bearer`** (параметризовано): без заголовка; токен-сміття; чужий ключ; протермінований (`exp` на годину раніше, більше за ClockSkew); невірний issuer; невірна audience; `alg: none`; без `sub`; роль `Admin`.
2. **403 + `Auth.Forbidden`**: Approver → `author`.
3. **200**: Author → `author`; Approver → `approver`; кожна роль → `any` повертає свій `sub`.
4. **FallbackPolicy**: `fallback` без токена → 401; з валідним токеном → 200.
5. **`/health`** без токена → 200.
6. **Статичні токени**: тест читає `.http` (рядки `@<name>Token = <value>`), для кожного з трьох: `any` → 200 з очікуваним `sub`; payload містить очікувану `role` і `exp = 1822348800`. Ловить розсинхрон ключа і токенів, якими користуватиметься рев'юер.

Перевірка: `dotnet build` і `dotnet test` для всього рішення — зелені.

## 8. Відомі обмеження (свідомі компроміси → README)

1. **Тестовий симетричний ключ закомічено** в `appsettings.Development.json`: будь-хто з доступом до репозиторію може випустити токен. У проді ключ надходить із secret store / env (`Jwt__SigningKey`) і ніколи не комітиться. Без ключа застосунок не стартує (`ValidateOnStart`), тому Production/Docker без `Jwt__SigningKey` не запуститься.
2. **Термін дії токенів — рік, без відкликання й refresh**: злитий токен дійсний до 2027-10-01. Вимога замовника.
3. **Немає сховища користувачів**: `assigneeId` не перевіряється на існування.
4. Профіль `http` (`localhost:5113`) передає bearer-токен відкритим текстом — допустимо лише локально.
5. Після 2027-10-01 токени треба регенерувати (розділ 5).

## 9. Поза обсягом

- `ApprovalTasksController`, його ендпоінти й сервіс (окремий план фічі; він застосовує матрицю з розділу 4).
- Видача токенів через API, refresh, відкликання, сховище користувачів, Swagger/OpenAPI-схема безпеки.
