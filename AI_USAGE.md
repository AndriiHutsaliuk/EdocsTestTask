# Використання AI

## Інструмент

Claude Code (модель Claude Opus) з плагіном beads-superpowers: brainstorming → spec → план → реалізація з рев'ю кожної задачі.

### Використані skills

- `brainstorming` — уточнення вимог і spec (`.internal/specs/`).
- `writing-plans` — покрокові плани (`.internal/plans/`).
- `stress-test` — перевірка плану перед реалізацією.
- `subagent-driven-development` — реалізація задач субагентами з окремим рев'ю кожної.
- `test-driven-development` — спершу тест, що падає, потім код.
- `verification-before-completion` — `dotnet build`/`dotnet test` перед закриттям задачі.
- `write-documentation` — текст README.
- `base-architecture` (власний skill) — правила архітектури .NET: межі шарів, Result pattern, primary constructors.

## Суттєві промпти (дослівно)

1. Початкова постановка:

   > давай спілкуємся Щодо Test task
   > * Хочу зробити Pattern State для обробки документів для кожного стану
   > * для in-memory зроби репозиторій
   > * Коли Create Document check title
   > * TextValidationHelper.IsTitleValid(command.Title, out var error)
   > * у кожного state має бути валідація Validation
   > * в оркестраторі завжди є Vaildation Method
   > * Auth вже є дивись Readme
   > * Create Messages as constant d окремому файлі

2. Перегляд рішення щодо State pattern (фрагмент):

   > Я б прибрав окремі класи State у цьому проєкті, залишивши ApprovalStatus і явні правила переходів. Поточна реалізація працює, але для обсягу цього ТЗ додає більше складності, ніж користі.
   > …
   > Один метод ResolveNextStatus(status, action, comment), який повертає Result<ApprovalStatus>.

## Що застосували і що змінили

- **Застосовано:** JWT з ролями `Author`/`Approver` (виконавець — з claim `sub`), in-memory репозиторій, lock на кожну задачу, повідомлення-константи, Swagger у Development.
- **Змінено:** State pattern замінили однією таблицею переходів — на три переходи він давав дев'ять файлів і дублював правила (див. README, «Обробка станів»).
- **Не робили:** перевірку існування `assigneeId`, бо бази користувачів немає (див. README, «Відомі обмеження»).

## Як перевіряли результат

- TDD для нової поведінки; після кожної задачі — `dotnet build` без попереджень і повний `dotnet test`.
- Рев'ю кожної задачі та фінальне рев'ю всієї гілки.
- Тест конкурентності: 200 повторів двох одночасних фінальних дій (без lock падав).
- Ручна перевірка на запущеному API через `curl`, `.http`-файл і Swagger UI.
