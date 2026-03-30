# Claude Code Context — DutchieLibrary

This file provides project context for AI-assisted development. Keep it up to date as the project evolves.

## AI Agent Guidelines

### .gitignore

- **After any code change** that introduces new files, output paths, secrets, build artifacts, or tooling — check `.gitignore` and update it if the new file/pattern is not already covered.
- Types of changes that commonly require a `.gitignore` update:
  - New config or secrets files (e.g. `*.env`, `secrets.json`, provider-specific credential files)
  - New output directories or generated files (e.g. tool-specific cache folders, test result directories)
  - New packages or runtimes that produce local state (e.g. lock files, local DB files, log directories)
  - IDE or editor integrations that write local settings (e.g. `.idea/`, `.vscode/` overrides)
- When in doubt, add the pattern — over-ignoring is safer than accidentally committing sensitive or generated files.

### Documentation

- **After any change to process, configuration, architecture, or behaviour** — update `README.md`, this file (`CLAUDE_CONTEXT.md`), and the memory file as appropriate.
- Specifically:
  - **New or changed configuration keys** → update the `appsettings.json` reference table in `README.md` and the appsettings section in `CLAUDE_CONTEXT.md`
  - **New Platform App objects or fields** → update the Platform Application section in `README.md` and the Platform App Objects section in `CLAUDE_CONTEXT.md`
  - **New packages or dependencies** → update the Dependency Versions table in `CLAUDE_CONTEXT.md`
  - **New or removed endpoints, pipelines, or workers** → update the relevant sections in both `README.md` and `CLAUDE_CONTEXT.md`
  - **Architectural changes** → update the architecture diagram and Key Files tables in `CLAUDE_CONTEXT.md`
  - **Pending work completed or cancelled** → remove it from the Pending / Future Work section in `CLAUDE_CONTEXT.md`
- Documentation that no longer reflects reality is worse than no documentation — remove or correct stale content, don't leave it alongside updated content.

### Changelog

- **After every code change**, add an entry to `CHANGELOG.md`.
- Each entry must include the UTC date and time (`YYYY-MM-DD HH:MM UTC`), the file(s) changed, and a concise description of what was added, changed, or removed.
- Group multiple related changes made in the same session under a single dated entry — do not create one entry per file.
- Use the categories: `Added`, `Changed`, `Removed`, `Fixed`.
- Place new entries at the top of the file, below the header.

---

## Project Purpose

.NET 8 integration library and worker service that syncs **Dutchie POS** data to **Sage Intacct** (and future ERP targets). Two scheduled sync jobs run as `BackgroundService` workers:

- **ClosingReportWorker** (24h default) — pulls the Dutchie daily closing report → GL journal entry in Intacct
- **TransactionSyncWorker** (15m default) — incremental pull of Dutchie transactions → AR invoices in Intacct

---

## Solution

```
DutchieLibrary.slnx        ← note: .slnx not .sln
```

Build command:
```sh
dotnet build /Users/jaradhuffman/Repos/dutchie-library/DutchieLibrary.slnx
```

Target: **net8.0**, LangVersion: **12**, Nullable: enabled, ImplicitUsings: enabled.

---

## Project Map

| Project | Namespace root | Purpose |
|---|---|---|
| `src/DutchieLibrary` | `Dutchie` | Typed HTTP client for the Dutchie POS REST API |
| `src/DutchieIntegration` | `Dutchie.Integration` | ERP-neutral abstractions, sync pipelines, state store |
| `src/DutchieIntacct` | `Dutchie.Intacct` | Sage Intacct SDK connector + Platform App config provider |
| `src/DutchieWorker` | `Dutchie.Worker` | Generic Host worker; entry point |

Dependency order: Library ← Integration ← Intacct ← Worker

---

## Key Files

### Library
| File | Role |
|---|---|
| `Clients/DutchieClientBase.cs` | Base HTTP client; retry loop (5× on 500, 3s delay), JSON settings |
| `Clients/ReportingClient.cs` | `IReportingClient` — closing-report, register-transactions, transactions, cash-summary |
| `Clients/ProductClient.cs` | `IProductClient` — product catalog |
| `Authentication/DutchieAuthHandler.cs` | `DelegatingHandler` that adds HTTP Basic auth (LocationKey:IntegratorKey) |
| `DutchieServiceCollectionExtensions.cs` | `AddDutchieClient(IConfiguration)` DI registration |
| `DutchieClientOptions.cs` | `LocationKey`, `IntegratorKey`, `BaseUrl` |
| `DutchieApiException.cs` | Custom exception with `StatusCode` + response body |
| `Models/Reporting/ClosingReport.cs` | Primary closing-report model |
| `Models/Reporting/Transaction.cs` | Full transaction with items, payments, taxes |

### Integration
| File | Role |
|---|---|
| `Abstractions/IErpConnector.cs` | `PostJournalEntryAsync` / `PostSalesTransactionAsync` |
| `Abstractions/IErpConfigProvider.cs` | `GetConfigAsync → ErpMappingConfig` |
| `Abstractions/ISyncStateStore.cs` | Watermark read/write |
| `Models/ErpMappingConfig.cs` | GL account map, default dims, Dutchie credentials (when from Platform App) |
| `Models/JournalEntryPayload.cs` | ERP-neutral journal entry |
| `Models/SalesTransactionPayload.cs` | ERP-neutral sales transaction |
| `Pipeline/ClosingReportSyncPipeline.cs` | Closing report → journal entry orchestration |
| `Pipeline/TransactionSyncPipeline.cs` | Incremental transaction → AR invoice orchestration |
| `State/JsonFileSyncStateStore.cs` | File-based watermark persistence (`sync-state.json`) |
| `IntegrationServiceCollectionExtensions.cs` | `AddDutchieIntegration(state => ...)` |

### Intacct
| File | Role |
|---|---|
| `Configuration/IntacctOptions.cs` | `CompanyId`, `UserId`, `UserPassword`, `SenderId`, `SenderPassword`, `EntityId`, `LocationId` |
| `Configuration/DutchieMasterConfigRow.cs` | Parsed `dutchie_master_config` row (journal symbol, tolerance, live flag) |
| `Configuration/DutchieLocationConfigRow.cs` | Parsed `dutchie_location_config` row (Dutchie creds, default dims) |
| `Configuration/PlatformAppErpConfigProvider.cs` | Queries all 3 Platform App objects; builds `ErpMappingConfig` |
| `Configuration/AppSettingsErpConfigProvider.cs` | Dev-only: loads `ErpMappingConfig` from appsettings |
| `Connectors/IntacctErpConnector.cs` | `IErpConnector` impl using Intacct SDK 3.2.2 |
| `IntacctServiceCollectionExtensions.cs` | `AddIntacctConnector(config)` + `.UsePlatformAppConfig()` |

### Worker
| File | Role |
|---|---|
| `Program.cs` | Entry point; loads `.env`, registers DI, configures NLog, runs host |
| `Workers/ClosingReportWorker.cs` | 24h BackgroundService for closing report sync |
| `Workers/TransactionSyncWorker.cs` | 15m BackgroundService for transaction sync |
| `WorkerOptions.cs` | `ClosingReportInterval`, `ClosingReportLookback`, `TransactionSyncInterval` |
| `appsettings.json` | Config sections: `Dutchie`, `Intacct`, `Worker` |
| `nlog.config` | NLog: coloured console (Info+), rolling file (Debug+), errors file (Error+) |

### Platform App
| File | Role |
|---|---|
| `PlatformApp/DutchieIntegration.xml` | Intacct Platform Application — deploy once per company |

---

## Dutchie API

- **Base URL:** `https://api.pos.dutchie.com`
- **Auth:** HTTP Basic — `LocationKey:IntegratorKey`
- **Swagger:** `https://api.pos.dutchie.com/swagger/v001/swagger.json`
- **Constraints:**
  - `/reporting/closing-report` — date range must be 12h–31d
  - `/reporting/cash-summary` — `fromLastModifiedDate` max 7 days back
  - `/reporting/transactions` — `TransactionId`, date range, and last-modified range are mutually exclusive

---

## Intacct SDK

- Package: `Intacct.SDK` version **3.2.2**
- `OnlineClient.Execute(IFunction, RequestConfig)` → `OnlineResponse`
- `QueryFunction` (new-style, `Intacct.SDK.Functions.Common.NewQuery`) for custom object queries
  - `SelectBuilder.Fields(string[]).GetFields()` → `ISelect[]`
  - `new Filter("fieldName").SetEqualTo(value)` → `IFilter`
  - `Result.Data` → `List<XElement>` — parse with `element.Element("FIELDNAME")?.Value?.Trim()`
- `ClientConfig` fields: `CompanyId`, `UserId`, `UserPassword`, `SenderId`, `SenderPassword`, `EntityId`
- Journal entries: `JournalEntryCreate` / `JournalEntryLineCreate`
- AR Invoices: `InvoiceCreate` / `InvoiceLineCreate`

---

## Platform Application Objects

Three custom objects. No custom fields on standard objects except `DUTCHIE_CUSTOMER_ID` on `customer` (patient cross-reference).

```
dutchie_master_config  (id=20021)
  GL Journal (RGLJOURNAL), maximum_overshort, is_live

dutchie_location_config  (id=20023)
  → Rdutchiemasterconfig (to 20021)
  → RLOC (to standard location, 1:1)
  entity_id override, dutchie_location_key, dutchie_integrator_key
  RCUSTOMER, RDEPARTMENT, RITEM (defaults)

dutchie_field_config  (id=20020)
  → Rdutchielocationconfig (to 20023)
  input_type (header/paymentSummary/taxRateSummary/categorySummary)
  api_field, credit_debit (C/D)
  RGLACCOUNT, RCUSTOMER, RDEPARTMENT, RITEM, RCLASS overrides
  amount (net/gross/discount), entry_type (revenue/cogs)

dutchie_process_log  (id=20022)
  → Rdutchielocationconfig (to 20023)
  job_name, status, records_processed, raw_errors, summarized_readable_errors
```

**Query flow in `PlatformAppErpConfigProvider`:**
1. Query `dutchie_master_config` (no filter — company-level, first row wins)
2. Query `dutchie_location_config` filtered by `RLOC = IntacctOptions.LocationId`
3. Query `dutchie_field_config` filtered by `Rdutchielocationconfig = locationConfig.RecordNo`

---

## Dependency Versions

| Package | Version | Used In |
|---|---|---|
| `Newtonsoft.Json` | 13.0.3 | Library, Integration |
| `Microsoft.Extensions.Http` | 8.0.1 | Library |
| `Microsoft.Extensions.Options` | 8.0.2 | Library, Integration, Intacct |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.2 | Integration, Intacct |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 8.0.2 | Integration, Intacct |
| `Intacct.SDK` | 3.2.2 | Intacct |
| `Microsoft.Extensions.Hosting` | 8.0.1 | Worker |
| `DotNetEnv` | 3.1.1 | Worker |
| `NLog.Extensions.Hosting` | 5.3.15 | Worker |

---

## Conventions

- **JSON:** Newtonsoft.Json throughout (not System.Text.Json). `CamelCasePropertyNamesContractResolver`, `NullValueHandling.Ignore`, `StringEnumConverter(CamelCaseNamingStrategy)`.
- **Async:** Always `.ConfigureAwait(false)` in library/integration/intacct layers. Omit in worker layer (top-level host).
- **DI registration:** Each project exposes a single `Add*` extension on `IServiceCollection`. Chaining pattern: `AddIntacctConnector(config).UsePlatformAppConfig()`.
- **Credentials:** Environment variables only — never in committed config files. `__` double-underscore maps to `:` in .NET config (e.g. `INTACCT__COMPANYID` → `Intacct:CompanyId`). `.env` file supported via DotNetEnv with `clobberExistingVars: false` (real env vars always win).
- **Logging:** Use `ILogger<T>` (MEL) throughout; NLog is the provider wired in the Worker only. Structured logging with message templates.
- **Retry:** HTTP 500 only, 5 retries, 3s fixed delay, in `DutchieClientBase.GetAsync`.

---

## DotNetEnv API Note

`LoadOptions` is a regular class (not a record). Use the copy constructor:
```csharp
new LoadOptions(Env.TraversePath(), clobberExistingVars: false)
// NOT: Env.TraversePath() with { ClobberExistingVars = false }  ← does not compile
```

---

## Pending / Future Work

- **Multi-location worker:** Currently one `IntacctOptions.LocationId` per process. A future enhancement could loop over all `dutchie_location_config` records and run each pipeline per location, using the per-location `DutchieLocationKey`/`DutchieIntegratorKey` from `ErpMappingConfig` to construct per-location `HttpClient` credentials.
- **Dutchie credentials from Intacct:** `ErpMappingConfig.DutchieLocationKey` and `DutchieIntegratorKey` are populated by `PlatformAppErpConfigProvider`. The pipelines and workers do not yet use these to override the statically-configured `DutchieClientOptions`. Wiring this would allow fully Intacct-driven credentials with no env vars for Dutchie.
- **`dutchie_process_log` writer:** The `dutchie_process_log` Platform App object schema is defined and the `IntacctErpConnector` could write log records using `CustomObjectCreate`. Not yet implemented.
- **`categorySummary` / `customerTypeSummary` pipeline:** `dutchie_field_config` supports these `input_type` values but the sync pipelines do not yet act on them. Reserved for future revenue breakdown by product category or customer type.
- **HA state store:** `JsonFileSyncStateStore` is single-instance only (file lock via `SemaphoreSlim`). For multi-instance deployments, implement `ISyncStateStore` backed by a database or distributed cache.
- **Product sync:** `IProductClient.GetProductsAsync` is implemented but no `ProductSyncPipeline` or worker exists yet.
