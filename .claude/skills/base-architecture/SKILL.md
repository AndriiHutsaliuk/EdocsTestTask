---
name: base-architecture
description: Design, implement, or document a task-specific base architecture for .NET 10 applications, including service boundaries, dependency direction, and a Result pattern for service outcomes.
---

# Base architecture

Build the architecture around the current task. Identify the required behavior, entry points, domain concepts, integrations, deployment constraints, and existing code before choosing projects or folders. Target .NET 10 for new work; when changing an existing solution, identify framework and package upgrades needed to reach .NET 10 instead of assuming the migration is already complete.

## Layers and dependencies

Use the task's application name in place of Task. These are role names, not a requirement to create every project for a small task.

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| Task.Shared | Result and Result<T> contracts and other types shared across layers. | None |
| Task.Core | Domain entities, value objects, rules, service and repository interfaces, and domain errors. | Shared |
| Task.Infrastructure | Service and repository implementations, persistence, external integrations, and dependency injection registration. | Core |
| Task.Api | Controllers or other entry points, ServiceResponse and other transport models, authentication, middleware, and composition. | Core and Infrastructure |
| Task.Tests | Unit and integration tests for the layers involved in the task. | Projects under test |

Keep domain contracts independent of transport, database, and external services. A typical request flows from an entry point through a Core service interface to an Infrastructure implementation, then through a Core repository interface to its Infrastructure implementation. Keep entry points thin: parse input, authorize, call a service, and translate its Result to a ServiceResponse. Follow existing project names and boundaries when extending a codebase; do not add a layer solely to match this table.

## Services and structure

- Each service lives in **its own folder**; pair with a **Helper** in the same folder pattern (static helpers; in the service use `using Helper = ...` for the helper namespace).
- Service interface: place in **`Task.Core/Interfaces/Services/`** (or a subfolder such as `Communication/Email/`) matching existing layout—one interface per service implementation.
- Services return `Result<T>` or `Result` (see Service Result pattern); endpoints return `ServiceResponse<T>` or `PagedResponse<T>` (see Endpoint ServiceResponse).
- Constants: **`Task.Core.Constants`**.
- One class per file; use **regions** inside classes; meaningful names.
- **Constructors:** classes that receive dependencies use C# primary constructors and use the parameters directly — no `ArgumentNullException.ThrowIfNull` and no copying into private fields. Derived values (e.g. `factory.CreateClient()`) and state the class later mutates (e.g. `_utcNow = start`) become field initializers; if the parameter is also used inside members, compute them in members instead (a parameter that is both captured and used in an initializer triggers warning CS9124). Keep an explicit constructor only when it validates invariants or must be non-public (e.g. `Result`), or when it has no parameters and builds fields that depend on each other.

## Service Result pattern

Service methods must return a Result pattern for expected outcomes: Result<T> when returning a value and Result when no value is needed. Use the solution's existing Result types if present. Otherwise define a consistent Result contract that carries success or failure, a value when applicable, and structured error information. Do not create incompatible Result types per feature.

Represent validation failures, missing resources, conflicts, and other expected business failures as failed Results. Keep exceptions for unexpected failures or cases that cannot be handled at the service boundary. The entry point maps Results to HTTP responses, messages, or other task-specific outputs; services do not depend on transport status codes.

## Endpoint ServiceResponse

API endpoints must return a ServiceResponse: ServiceResponse<T> when returning data, PagedResponse<T> when returning a paged list, and ServiceResponse when no data is returned. Use the solution's existing ServiceResponse and PagedResponse types if present. Otherwise define them once in the Api layer as the transport envelope that carries success, data when applicable, and error information for the client. Endpoints translate the service Result into a ServiceResponse and set the matching HTTP status code; do not return raw entities, domain types, or Results directly from endpoints, and do not use ServiceResponse inside Core or Infrastructure.

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

## Design and delivery

For a feature, trace the path from entry point to service, domain operation, and persistence or external integration. Define the contracts at the boundary that owns them. Put validation where it can be reused and tested, and use structured logging for failures that need operational context without exposing sensitive data.

When asked to write an architecture document, describe the task's components, responsibilities, dependency graph, main request and data flows, Result contract, error handling, and test boundaries. Separate what already exists from proposed changes. Cite relevant source paths for claims about an existing codebase, and identify unresolved choices rather than inventing details.

Verify implementation with the relevant .NET 10 build and focused tests. Include tests for successful Results and meaningful failure cases at service boundaries.
