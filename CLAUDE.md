# CLAUDE.md — Transformer

> This file is the single source of truth for both **Cowork** and **Claude Code** on every task in this repository. Read it in full before starting any work.

---

## 1. Project Overview

**Transformer** is a general-purpose data normalization Azure Function that sits between upstream data sources (operational systems, APIs, ingestion pipelines) and downstream consumers (data warehouses, APIs, applications).

It is called by ingestion workflows or event triggers and transforms semi-structured or heterogeneous payloads into structured, schema-aligned data ready for storage, analytics, or downstream processing. Transformation behavior is fully driven by JSON configuration files — no code changes are required to add or modify a transformation.

---

## 2. Tech Stack

| Concern | Choice |
|---|---|
| Runtime | .NET 8 |
| Azure Functions version | v4 |
| Worker model | **Isolated worker** (not in-process) |
| Hosting plan | **Flex Consumption** |
| JSON library | `System.Text.Json` |
| Logging | Built-in `ILogger` + Application Insights |
| HTTP trigger extensions | `Microsoft.Azure.Functions.Worker.Extensions.Http` |

**No external service calls in v1.** No Polly, no Key Vault, no Serilog, no FluentValidation. Keep the dependency footprint minimal.

---

## 3. Project Structure

```
transformer/
├── src/
│   └── Transformer/                        # Azure Function project (.csproj)
│       ├── Functions/                      # HTTP-triggered function class(es)
│       ├── Services/                       # Transformation logic (interfaces + implementations)
│       ├── Models/                         # Request/response/config POCOs
│       ├── Configs/                        # Embedded transformation config JSON files
│       │   └── {Domain}/
│       │       └── {Operation}/
│       │           └── {configName}.json
│       └── Program.cs                      # DI wiring, middleware registration
├── tests/
│   └── Transformer.Tests/                  # xUnit test project (.csproj)
├── transformer.sln
└── CLAUDE.md
```

### Naming conventions

| Artifact | Convention | Example |
|---|---|---|
| Classes, methods, properties | PascalCase | `TransformationService` |
| File names | PascalCase | `TransformationService.cs` |
| Private fields | `_camelCase` | `_transformationService` |
| Interfaces | `I`-prefix + PascalCase | `ITransformationService` |
| Config files | kebab-case | `salesforce-to-warehouse.json` |
| JSON serialization | camelCase | `{ "orderId": "..." }` |

---

## 4. Configuration Format

Transformation configs are **embedded JSON files** versioned alongside the code. They live at:

```
src/Transformer/Configs/{Domain}/{Operation}/{configName}.json
```

The function resolves the config from the three URL route parameters at request time.

### Schema

```json
{
  "version": "1.0",
  "description": "Human-readable description of this transformation",

  "source": {
    "type": "json",
    "rootPath": "$"
  },

  "target": {
    "type": "json",
    "rootObject": "order"
  },

  "settings": {
    "ignoreNulls": true,
    "dateFormat": "yyyy-MM-ddTHH:mm:ssZ",
    "culture": "en-US"
  },

  "mappings": [
    { "target": "orderId", "source": "$.id", "type": "string" },
    { "target": "customer.name", "source": "$.customer.full_name", "type": "string", "transform": "trim" },
    {
      "target": "customer.email",
      "source": "$.customer.email",
      "type": "string",
      "validate": { "regex": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", "onFail": "null" }
    },
    {
      "target": "customer.isVip",
      "source": "$.customer.tags",
      "type": "boolean",
      "transform": "contains",
      "parameters": { "value": "VIP" }
    },
    { "target": "orderDate", "source": "$.created_at", "type": "datetime", "format": "yyyy-MM-ddTHH:mm:ssZ" },
    { "target": "totalAmount", "source": "$.total", "type": "decimal", "transform": "round", "parameters": { "precision": 2 } },
    { "target": "currency", "source": "$.currency", "type": "string", "default": "USD" },
    {
      "target": "status",
      "source": "$.status",
      "type": "string",
      "lookup": { "pending": "Pending", "paid": "Completed", "failed": "Cancelled" }
    },
    { "target": "shipping.address.line1", "source": "$.shipping.address1" },
    { "target": "shipping.address.city", "source": "$.shipping.city" },
    { "target": "shipping.address.postalCode", "source": "$.shipping.zip" },
    {
      "target": "items",
      "source": "$.line_items",
      "type": "array",
      "itemMapping": {
        "sku": "$.sku",
        "name": "$.name",
        "quantity": "$.qty",
        "unitPrice": "$.price",
        "lineTotal": { "expression": "$.qty * $.price" }
      }
    },
    {
      "target": "discountAmount",
      "type": "decimal",
      "condition": { "if": "$.discount != null", "then": "$.discount.amount", "else": 0 }
    },
    { "target": "isHighValue", "type": "boolean", "expression": "$.total > 1000" },
    { "target": "metadata.sourceSystem", "value": "Shopify" },
    { "target": "metadata.processedAt", "type": "datetime", "transform": "now" }
  ],

  "postProcessing": [
    { "type": "removeEmptyObjects" },
    { "type": "sortArray", "target": "items", "by": "name" }
  ],

  "errorHandling": {
    "onMissingField": "ignore",
    "onTypeMismatch": "coerce",
    "onError": "log"
  }
}
```

### Supported feature surface

- Direct field mapping (`source → target`)
- Nested object creation via dot-notation targets
- Type conversion: `string`, `decimal`, `datetime`, `boolean`
- Default values
- Validation rules with regex and `onFail` fallback
- Transform functions: `trim`, `round`, `contains`, `now`, `passthrough`
- Conditional logic (`if / then / else`)
- Inline expressions (math, comparisons)
- Lookup / enum mapping
- Array transformations with item-level `itemMapping`
- Static value injection (`value` field, no `source`)
- Post-processing steps (`removeEmptyObjects`, `sortArray`)
- Error handling strategies per mapping and globally

---

## 5. API Contract

### Route

```
POST /api/transform/{domain}/{operation}/{configName}
```

No API versioning in v1. Add when there is a real breaking change.

### Request

```json
{
  "correlationId": "abc-123",
  "payload": {
    // raw source data — any valid JSON object
  }
}
```

`Content-Type: application/json` required.

### Response (200 OK)

```json
{
  "correlationId": "abc-123",
  "domain": "CRM",
  "operation": "Order",
  "configName": "salesforce-to-warehouse",
  "processedAt": "2026-05-05T12:00:00Z",
  "payload": {
    // transformed output data
  }
}
```

### Error Response (RFC 7807 ProblemDetails)

```json
{
  "type": "https://transformer/errors/config-not-found",
  "title": "Configuration Not Found",
  "status": 404,
  "detail": "No config found for CRM/Order/salesforce-to-warehouse",
  "correlationId": "abc-123"
}
```

### Status Codes

| Scenario | Code |
|---|---|
| Successful transformation | `200 OK` |
| Malformed JSON request body | `400 Bad Request` |
| Config not found for route | `404 Not Found` |
| Unsupported content type | `415 Unsupported Media Type` |
| Transformation engine error | `500 Internal Server Error` |

---

## 6. Coding Conventions

### Async

- All I/O-bound operations use `async/await` end-to-end
- Method signatures return `Task` or `Task<T>`, never `void`
- `CancellationToken` threaded from function entry point through all async calls
- No `async void`, no `.Result`, no `.Wait()`

### Dependency Injection

- All services registered in `Program.cs` via `IServiceCollection` extension methods
- Always register interface → implementation (e.g., `services.AddSingleton<ITransformationService, TransformationService>()`)
- Stateless services (transformation engine, config loader) registered as **Singleton**
- No service locator pattern — constructor injection only

### Exception Handling

- A **global middleware exception handler** in `Program.cs` catches unhandled exceptions, logs via `ILogger`, and returns a `ProblemDetails` 500 response
- Services and functions throw **typed exceptions** (e.g., `ConfigNotFoundException`, `TransformationException`) — never generic `Exception`
- No swallowed exceptions — everything is either handled at the boundary or logged and rethrown
- `try/catch` blocks only at meaningful boundaries, not scattered through service logic

### Logging

- Use injected `ILogger<T>` — structured logging via Application Insights
- Log at `Information` for successful transforms (include `correlationId`, `domain`, `operation`, `configName`, duration)
- Log at `Warning` for recoverable issues (missing optional fields, type coercions)
- Log at `Error` for all exceptions before rethrowing

---

## 7. Testing

| Concern | Choice |
|---|---|
| Framework | xUnit |
| Mocking | Moq |
| Coverage target | **80% minimum** |
| Integration tests | Deferred to a future iteration |

- Tests live in `tests/Transformer.Tests/`
- Mirror the `src/Transformer/` folder structure inside the test project
- All tests must be deterministic and side-effect free
- CI enforces `dotnet test` passing; coverage reporting to be added when integration tests are introduced

---

## 8. Deployment

### Azure Resource Naming

| Resource | Name pattern | Example |
|---|---|---|
| Resource Group | `rg-transformer-{env}` | `rg-transformer-dev` |
| Function App | `func-transformer-{env}` | `func-transformer-dev` |
| Storage Account | `sttransformer{env}` | `sttransformerdev` |
| Application Insights | `appi-transformer-{env}` | `appi-transformer-dev` |

### Environments

`dev` only for v1. `prod` and additional environments to be added when needed.

### Configuration & Secrets

All configuration via **environment variables / Azure App Settings**. No Key Vault for v1. Transformation configs are embedded in the build artifact — no runtime config store required.

---

## 9. GitHub Workflow

### Labels

**Status labels** (one per issue at all times):

| Label | Meaning |
|---|---|
| `status:ready` | Fully specified, ready for Claude Code to pick up |
| `status:in-progress` | Claude Code is actively working it |
| `status:needs-clarification` | Blocked on a question — Claude Code added to Open Questions |
| `status:in-review` | PR is open, CI running or awaiting review |
| `status:qa` | Merged, awaiting smoke test / validation |
| `status:done` | Accepted and closed |
| `status:blocked` | Blocked by an external dependency |

**Type labels** (one per issue):

| Label | Meaning |
|---|---|
| `type:feature` | New capability |
| `type:bug` | Defect fix |
| `type:chore` | Non-functional work (refactor, tooling, docs) |

### Story Flow

```
status:ready
  └─▶ Claude Code picks up (moves to status:in-progress)
        └─▶ Implementation complete → PR opened (moves to status:in-review)
              └─▶ CI passes → Claude Code approves and merges
                    └─▶ Issue closed, moved to status:done
```

If Claude Code gets stuck it adds a question to the issue's **Open Questions** section and moves the label to `status:needs-clarification`.

### Claude Code Story Pickup Rules

1. Query open issues with label `status:ready`, ordered by issue number ascending (lowest = oldest = highest priority)
2. Move the issue to `status:in-progress` before starting any work
3. Create a branch named `{type}/{issue-number}-{short-slug}` (e.g., `feature/12-order-transform`)
4. Implement per the Acceptance Criteria; follow all conventions in this file
5. Open a PR using the PR template; link the issue with `Closes #N`
6. After CI passes, approve and merge the PR
7. Close the issue and apply `status:done`

### Issue Template

Issues use `.github/ISSUE_TEMPLATE/story.md`. All stories must have:
- **Context** — why we are doing this
- **Acceptance Criteria** — checkable conditions for done
- **Technical Notes** — files to touch, patterns to follow
- **Out of Scope** — explicit exclusions
- **Open Questions** — left blank by the author; Claude Code adds here if blocked

### PR Template

PRs use `.github/pull_request_template.md` and must include:
- Summary of what changed and why
- `Closes #N` link to the issue
- Checked-off What Was Tested items (build, tests, smoke test, no console errors)
- Acceptance Criteria copied from the issue and checked off

### CI

CI runs on all PRs and pushes to `main` (`.github/workflows/ci.yml`):
- `dotnet restore`
- `dotnet build --configuration Release`
- `dotnet test --configuration Release`

All three steps must pass before a PR can be merged. **Never merge a PR with failing CI.**
