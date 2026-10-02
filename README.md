# EdocsTestTask

API погодження документів. Умови тестового завдання: [docs/TEST_TASK.md](docs/TEST_TASK.md).

> **Про мову.** Це тестове завдання, тому документацію (цей README) написано українською. Код, коментарі, повідомлення API та коди помилок — англійською.

## Автентифікація

Усі ендпоінти, крім `GET /health` і (лише в Development) `/swagger`, вимагають заголовок `Authorization: Bearer <token>`. Токени статичні: їх підписано алгоритмом HS256 тестовим ключем з `appsettings.Development.json`, і вони дійсні до **2027-10-01T00:00:00Z**.

| Користувач (`sub`) | Роль | Змінна в `EdocsTestTask.Api/EdocsTestTask.Api.http` |
|---|---|---|
| `author-1` | `Author` | `@authorToken` |
| `approver-1` | `Approver` | `@approverToken` |
| `approver-2` | `Approver` | `@otherApproverToken` |

### Токени для Swagger

Скопіюйте **один блок нижче цілком** кнопкою копіювання та вставте у Swagger → **Authorize → Value → Authorize**. Кожен блок містить лише готовий JWT: додавати слово `Bearer` чи ім'я користувача не потрібно.

#### Автор — створити задачу

Користувач `author-1`, роль `Author`. Використовуйте для `POST /api/approval-tasks`.

```text
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJFZG9jc1Rlc3RUYXNrLkFwaSIsImlzcyI6IkVkb2NzVGVzdFRhc2siLCJleHAiOjE4MjIzNDg4MDAsImlhdCI6MTc5MDg4NzIyNiwibmJmIjoxNzkwODg3MjI2LCJzdWIiOiJhdXRob3ItMSIsInJvbGUiOiJBdXRob3IifQ.qfo6QOPpu0ZK5tE3wzawUeNSj8DCTmTR04UvAQVjDj0
```

#### Виконавець — почати та погодити або відхилити задачу

Користувач `approver-1`, роль `Approver`. Під час створення задачі вкажіть `"assigneeId": "approver-1"`. Перед виконанням дій змініть токен через **Authorize → Logout**, вставте цей JWT і натисніть **Authorize**.

```text
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJFZG9jc1Rlc3RUYXNrLkFwaSIsImlzcyI6IkVkb2NzVGVzdFRhc2siLCJleHAiOjE4MjIzNDg4MDAsImlhdCI6MTc5MDg4NzIyNiwibmJmIjoxNzkwODg3MjI2LCJzdWIiOiJhcHByb3Zlci0xIiwicm9sZSI6IkFwcHJvdmVyIn0.M6pJ_O46DpsjMulC0Rn4rAPckHj6PokOJzfUGT7ah9I
```

#### Інший виконавець — перевірити відмову в доступі

Користувач `approver-2`, роль `Approver`. Дія над задачею, призначеною `approver-1`, поверне `403`.

```text
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJFZG9jc1Rlc3RUYXNrLkFwaSIsImlzcyI6IkVkb2NzVGVzdFRhc2siLCJleHAiOjE4MjIzNDg4MDAsImlhdCI6MTc5MDg4NzIyNiwibmJmIjoxNzkwODg3MjI2LCJzdWIiOiJhcHByb3Zlci0yIiwicm9sZSI6IkFwcHJvdmVyIn0.rRwiiq-BlxFI2Y2AgCz8N6F7CpqKr8iYYs9c2gVTz1I
```

### Хто що може

| Ендпоінт | Хто має доступ |
|---|---|
| `POST /api/approval-tasks` | `Author` |
| `POST /api/approval-tasks/{id}/actions` | `Approver`, і лише призначений виконавець задачі |
| `GET /api/approval-tasks/{id}` | будь-яка роль |
| `GET /health` | будь-хто, токен не потрібен |
| `/swagger` (лише в Development) | будь-хто, токен не потрібен |

Помилки приходять у тому ж конверті, що й решта відповідей API:

- `401` з кодом `Auth.Unauthorized`: токена немає, він протермінований чи підроблений, або в ньому немає `sub` чи відомої ролі;
- `403` з кодом `Auth.Forbidden`: роль не дозволяє цю дію.

### Поточний користувач береться з токена

За ТЗ `actorId` передається в тілі `POST /api/approval-tasks/{id}/actions`. Тут виконавцем вважається власник токена (claim `sub`), а поля `actorId` у тілі запиту немає. Якби `actorId` лишався в тілі, будь-хто з валідним токеном міг би діяти від імені іншого виконавця.

### Ключ підпису поза Development

У `appsettings.json` ключа немає. У будь-якому іншому середовищі задайте його через змінну середовища `Jwt__SigningKey` (щонайменше 32 байти в UTF-8). Без ключа API не запуститься.

### Як перегенерувати токени

1. У файлі `EdocsTestTask.Tests/Api/Auth/StaticTokenTests.cs` приберіть аргумент `Skip` з тесту `GenerateStaticToken`.
2. Запустіть:
   ```bash
   dotnet test EdocsTestTask.slnx --filter "FullyQualifiedName~GenerateStaticToken" --logger "console;verbosity=detailed"
   ```
3. Вставте надруковані токени в `EdocsTestTask.Api.http` і в цей README, потім поверніть `Skip`.
4. Щоб змінити дату закінчення, оновіть `StaticTokenExpiry` у тому ж тестовому файлі.

### Відомі обмеження

1. Тестовий ключ підпису закомічено в `appsettings.Development.json`, тож будь-хто з доступом до репозиторію може випустити собі токен. У продакшені ключ має надходити із secret store або змінної середовища й ніколи не потрапляти в репозиторій.
2. Токени дійсні рік, відкликання і refresh немає. Якщо токен витече, ним можна буде користуватися до 2027-10-01.
3. Профіль `http` (`localhost:5113`) передає токен відкритим текстом. Це прийнятно лише для локального запуску.
4. Після 2027-10-01 токени треба перегенерувати (див. вище).

## Запуск

```bash
dotnet run --project EdocsTestTask.Api
dotnet test EdocsTestTask.Tests/EdocsTestTask.Tests.csproj
```

API слухає `http://localhost:5113`. Готові запити — у `EdocsTestTask.Api/EdocsTestTask.Api.http`. Токени описані в розділі «Автентифікація».

## Swagger

Через Swagger UI можна протестувати всі ендпоінти без curl і `.http`-файлу. Він доступний лише в Development: профілі `http`/`https` з `launchSettings.json` задають це середовище, а Visual Studio і Rider відкривають `/swagger` автоматично.

1. Запустіть API (`dotnet run --project EdocsTestTask.Api`) і відкрийте `http://localhost:5113/swagger`.
2. Скопіюйте JWT із блоку [«Автор — створити задачу»](#автор--створити-задачу). Натисніть **Authorize**, вставте скопійоване у **Value** і натисніть **Authorize**. Префікс `Bearer` Swagger додасть автоматично.
3. `POST /api/approval-tasks` → **Try it out**. Замініть тіло запиту на `{"documentNumber":"DOC-2026-001","title":"Supply contract","assigneeId":"approver-1"}` (шаблон Swagger з `"string"` створить задачу з виконавцем `string`, і `approver-1` отримає `403`) і натисніть **Execute**. Скопіюйте `id` з відповіді.
4. **Authorize → Logout**, потім вставте JWT із блоку [«Виконавець — почати та погодити або відхилити задачу»](#виконавець--почати-та-погодити-або-відхилити-задачу) та натисніть **Authorize**. Виконайте `POST /api/approval-tasks/{id}/actions` з `{"action":"Start"}`, а потім з `Approve` або `Reject` (для `Reject` потрібен `comment`).
5. `GET /api/approval-tasks/{id}` працює з будь-яким токеном.

Для кожної дії Swagger показує реальні коди відповідей: `201`/`200` і помилки `400`/`401`/`403`/`404`/`409`. Помилки приходять у конверті `ServiceResponse`, окрім помилок десеріалізації тіла: вони приходять як `ProblemDetails` (див. «Відомі обмеження (approval tasks)»).

## Приклади

Створення задачі (роль `Author`):

```bash
curl -X POST http://localhost:5113/api/approval-tasks \
  -H "Authorization: Bearer $AUTHOR_TOKEN" -H "Content-Type: application/json" \
  -d '{"documentNumber":"DOC-2026-001","title":"Supply contract","assigneeId":"approver-1"}'
```

Відповідь `201`: `{ "success": true, "data": { "id": "...", "status": "Assigned", "history": [], ... } }`.

Обмеження полів (після обрізання пробілів по краях): `title` — 3–256 символів, `documentNumber` і `assigneeId` — обов'язкові, до 128 символів; `comment` у дії — до 4096 символів. ТЗ не визначає обмежень на довжину рядків, тому ці значення обрано самостійно (константи в `EdocsTestTask.Core/Constants/ValidationConstants.cs`).

Дія (роль `Approver`, лише призначений виконавець):

```bash
curl -X POST http://localhost:5113/api/approval-tasks/<id>/actions \
  -H "Authorization: Bearer $APPROVER_TOKEN" -H "Content-Type: application/json" \
  -d '{"action":"Start"}'
```

`action`: `Start`, `Approve` або `Reject` (без урахування регістру); `comment` обов'язковий для `Reject`, до 4096 символів після обрізання пробілів по краях (довше — `400` `ApprovalTask.InvalidComment`). Виконавець береться з claim `sub` токена, поле `actorId` у тілі ігнорується.

## Обробка станів

Увесь workflow описує одна таблиця в `ApprovalTaskServiceHelper.ResolveNextStatus(status, action, comment)`. Вона повертає наступний статус або помилку:

| Поточний статус | Дія | Результат |
|---|---|---|
| `Assigned` | `Start` | `InProgress` |
| `InProgress` | `Approve` | `Approved` |
| `InProgress` | `Reject` з непорожнім `comment` | `Rejected` (без коментаря — `400`) |
| `Approved`, `Rejected` | будь-яка | `409` `ApprovalTask.AlreadyFinalized` |
| інші комбінації | | `409` `ApprovalTask.InvalidTransition` |

`ApprovalTaskService` спершу перевіряє команду через `Helper.Validate`. Нову задачу будує `Helper.Create`. Для дії сервіс усередині lock задачі бере наступний статус із таблиці й в одному місці оновлює статус, `updatedAtUtc` та історію.

Окремі класи на кожен стан (State pattern) спершу були реалізовані, але потім їх прибрали. На три переходи вони давали дев'ять файлів і дублювали правила (перевірка дії та вибір наступного статусу жили в різних методах). Таблиця тримає весь workflow в одному місці. State pattern мав би сенс, якби стани мали власну поведінку: ескалації, таймери, дії при вході чи виході.

Порядок перевірок: `400` (команда) → `404` (задача) → `403` (не виконавець) → `409`/`400` (таблиця переходів). Усі повідомлення — константи в `EdocsTestTask.Core/Constants/ApprovalTaskMessages.cs`.

## Захист від конкурентних змін

`ApprovalTaskService` тримає окремий `lock` на кожну задачу: читання, перевірка, перехід і збереження виконуються як один крок. Другий паралельний `Approve`/`Reject` бачить уже фінальний статус і отримує `409`; в історії лишається одне рішення. Репозиторій зберігає й повертає копії, тому помилка не змінює збережених даних. Тест: `ApprovalTaskServiceConcurrencyTests`.

## Відомі обмеження (approval tasks)

- Блокування працює лише в межах одного процесу; для кількох інстансів потрібна оптимістична конкурентність у сховищі.
- Дані в пам'яті зникають після перезапуску; словник lock-ів росте разом із кількістю задач і не очищується.
- Під час створення не перевіряється, чи існує користувач `assigneeId` і чи має він роль `Approver`: окремої бази користувачів у тестовому проєкті немає, користувачі існують лише як claims у статичних токенах.
- Можна створити кілька задач з однаковим `title` (і з однаковим `documentNumber`), бо унікальність не перевіряється. ТЗ не визначає, чи це дозволено, тож питання відкрите.
- Помилки десеріалізації тіла (некоректний JSON) повертаються стандартним `ProblemDetails`, а не конвертом `ServiceResponse`.
