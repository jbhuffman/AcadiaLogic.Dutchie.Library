# Changelog

All notable changes to this project are documented here.
Format: `YYYY-MM-DD HH:MM UTC` — `Category` — description. New entries go at the top.

---

## 2026-03-30 — PascalCase rename (C# conventions)

**Changed**

- `dutchie-library.slnx` → `DutchieLibrary.slnx` — solution file renamed to PascalCase
- `src/dutchie-library/` → `src/DutchieLibrary/` — project folder and `.csproj` renamed; `AssemblyName` updated to `DutchieLibrary`
- `src/dutchie-integration/` → `src/DutchieIntegration/` — project folder and `.csproj` renamed; `AssemblyName` updated to `DutchieIntegration`; `ProjectReference` path updated
- `src/dutchie-intacct/` → `src/DutchieIntacct/` — project folder and `.csproj` renamed; `AssemblyName` updated to `DutchieIntacct`; `ProjectReference` path updated
- `src/dutchie-worker/` → `src/DutchieWorker/` — project folder and `.csproj` renamed; `ProjectReference` paths updated
- `platform-app/` → `PlatformApp/` — platform app folder renamed to PascalCase
- `Dockerfile` — updated all `COPY`/`RUN` paths and `ENTRYPOINT` to `DutchieWorker.dll`
- `docker-compose.yml` — updated health check to `DutchieWorker.dll`
- `CLAUDE.md`, `README.md`, `AGENT.md`, `DEPLOY.md`, `FEATURES.md` — all path and project name references updated to PascalCase

---

## 2026-03-09 — Multi-location sync loop

**Added**

- `src/dutchie-library/Clients/IDutchieClientFactory.cs` — interface for creating per-location `IReportingClient` instances at runtime
- `src/dutchie-library/Clients/DutchieClientFactory.cs` — implementation using `IHttpClientFactory` (`Dutchie.PerLocation` named client, no auth handler); falls back to `DutchieClientOptions` when per-location credentials are absent

**Changed**

- `src/dutchie-integration/Abstractions/IErpConfigProvider.cs` — added `GetAllConfigsAsync()` returning one `ErpMappingConfig` per location
- `src/dutchie-intacct/Configuration/AppSettingsErpConfigProvider.cs` — implemented `GetAllConfigsAsync()` as a single-item wrapper around `GetConfigAsync()`
- `src/dutchie-intacct/Configuration/PlatformAppErpConfigProvider.cs` — implemented `GetAllConfigsAsync()`: 3 Intacct API calls total (master + all locations + all field configs grouped in memory); populates `LocationConfigRecordNo` per location
- `src/dutchie-library/DutchieServiceCollectionExtensions.cs` — registered `Dutchie.PerLocation` named `HttpClient` (no auth handler) and `IDutchieClientFactory` singleton
- `src/dutchie-integration/Pipeline/ClosingReportSyncPipeline.cs` — removed `IReportingClient` and `IErpConfigProvider` from constructor; `RunAsync` now accepts them as parameters; per-location watermark key (`ClosingReport-{locationId}`)
- `src/dutchie-integration/Pipeline/TransactionSyncPipeline.cs` — same constructor and `RunAsync` signature change; per-location watermark key (`Transactions-{locationId}`)
- `src/dutchie-worker/Workers/ClosingReportWorker.cs` — injects `IErpConfigProvider` + `IDutchieClientFactory`; loops all location configs; per-location failure is caught and logged, remaining locations continue
- `src/dutchie-worker/Workers/TransactionSyncWorker.cs` — same multi-location loop pattern

---

## 2026-03-09 — Process log writer (dutchie_process_log)

**Added**

- `src/dutchie-integration/Models/ProcessLogEntry.cs` — ERP-neutral model for sync run audit entries; includes `JobName`, `Status`, `RecordsProcessed`, `RawErrors`, `SummarizedErrors`, `LocationConfigRecordNo`, and a `Statuses` constants class

**Changed**

- `src/dutchie-integration/Abstractions/IErpConnector.cs` — added `WriteProcessLogAsync(ProcessLogEntry, CancellationToken)` method; implementations must swallow all exceptions internally
- `src/dutchie-integration/Models/ErpMappingConfig.cs` — added `LocationConfigRecordNo` property to carry the `dutchie_location_config` RECORDNO through to process log writes
- `src/dutchie-intacct/Configuration/PlatformAppErpConfigProvider.cs` — populates `LocationConfigRecordNo = location?.RecordNo` in `BuildErpMappingConfig`
- `src/dutchie-intacct/Connectors/IntacctErpConnector.cs` — implemented `WriteProcessLogAsync` using a private `ProcessLogCreateFunction : AbstractFunction` nested class that writes the `dutchie_process_log` create XML directly; exceptions are swallowed and logged as warnings
- `src/dutchie-integration/Pipeline/ClosingReportSyncPipeline.cs` — wrapped `RunAsync` body in try/catch; writes `complete` or `failed` process log after each run; cancellation is not logged as failure
- `src/dutchie-integration/Pipeline/TransactionSyncPipeline.cs` — same process log wrapping; failed transaction IDs are collected and included in `SummarizedErrors`; status is `failed` when any individual transaction posting failed

---

## 2026-03-09 — Docker and Docker Compose support

**Added**

- `Dockerfile` — multi-stage build: SDK image restores and publishes; runtime image runs as non-root `dutchie` user
- `docker-compose.yml` — single-service Compose file with `env_file`, named volumes (`dutchie-logs`, `dutchie-data`), health check, and `restart: unless-stopped`
- `.dockerignore` — excludes `bin/`, `obj/`, `.env`, secrets, logs, runtime state, IDE files, and documentation from the Docker build context

**Changed**

- `.gitignore` — added `docker-compose.override.yml` pattern for per-machine Compose overrides
- `DEPLOY.md` — replaced placeholder Docker snippet with full Docker Compose instructions (first run, override file pattern, useful commands, update procedure)

---

## 2026-03-09 — Deployment, requirements, and features documentation

**Added**

- `DEPLOY.md` — step-by-step deployment guide covering Platform App setup, build/publish, credential options, worker configuration, systemd/Docker deployment, verification, update, rollback, and troubleshooting
- `REQUIREMENTS.md` — functional and non-functional requirements including FR and NFR IDs, constraints, and out-of-scope items for the current release
- `FEATURES.md` — implemented feature descriptions and planned/future feature list

---

## 2026-03-09 — Documentation and context

**Added**

- `CHANGELOG.md` — this file; tracks all project changes with date/time
- `CLAUDE_CONTEXT.md` — full AI agent context document covering project structure, conventions, API constraints, Platform App object schema, dependency versions, and pending work
- `CLAUDE_CONTEXT.md` — AI Agent Guidelines: rules for keeping `.gitignore`, documentation, and `CHANGELOG.md` up to date after every code change
- `README.md` — full project documentation covering architecture, getting started, configuration reference, Platform Application object hierarchy, API client usage, ERP abstractions, Intacct connector, and logging
- `LICENSE` — MIT license, copyright 2026 AcadiaLogic

**Changed**

- `.gitignore` — added `logs/` to cover NLog rolling log files written at runtime

---

## 2026-03-09 — NLog logging and HTTP retry

**Added**

- `src/dutchie-worker/nlog.config` — NLog configuration with three targets: coloured console (Info+), rolling file `logs/dutchie-worker.log` (Debug+, 30-day retention), errors-only file `logs/dutchie-errors.log` (Error+, 90-day retention); Microsoft internals suppressed below Warn
- `src/dutchie-worker/dutchie-worker.csproj` — added `NLog.Extensions.Hosting 5.3.15`; added `nlog.config` as `PreserveNewest` content item

**Changed**

- `src/dutchie-library/Clients/DutchieClientBase.cs` — added `ILogger` constructor parameter and retry loop in `GetAsync`: up to 5 retries on HTTP 500, 3-second fixed delay between attempts; logs `Warning` per retry and `Error` when retries exhausted
- `src/dutchie-library/Clients/ReportingClient.cs` — added `ILogger<ReportingClient>` constructor parameter, passed to base
- `src/dutchie-library/Clients/ProductClient.cs` — added `ILogger<ProductClient>` constructor parameter, passed to base
- `src/dutchie-worker/Program.cs` — added `builder.Logging.ClearProviders()` and `builder.Logging.AddNLog()` to wire NLog as the MEL provider

---

## 2026-03-09 — Platform Application redesign (three-object hierarchy)

**Changed**

- `platform-app/DutchieIntegration.xml` — full redesign of custom object hierarchy:
  - `dutchie_master_config` (20021) narrowed to company-level settings only: GL journal, over/short tolerance, live flag
  - `dutchie_location_config` (20023) added as new object: per-location Dutchie credentials (`dutchie_location_key`, `dutchie_integrator_key`), entity ID override, default customer/department/item; links to master config and standard location (1:1)
  - `dutchie_field_config` (20020) renamed from `dutchie_config`; relationship changed from standard location (RLOC) to `dutchie_location_config` (Rdutchielocationconfig)
  - `dutchie_process_log` (20022) relationship changed from standard location to `dutchie_location_config`
  - All custom fields removed from standard `location` object (DUTCHIE_ENABLED, DUTCHIE_LIVE, DUTCHIE_LOCATION_KEY, DUTCHIE_INTEGRATOR_KEY, DUTCHIE_LOCATION_TYPE); back-reference display fields only
  - `DUTCHIE_CUSTOMER_ID` custom field retained on standard `customer` object for patient cross-reference
  - Added menu for `dutchie_location_config` (id 20108)
- `src/dutchie-intacct/Configuration/DutchieMasterConfigRow.cs` — simplified to company-level fields only (GlJournalSymbol, MaximumOverShort, IsLive)
- `src/dutchie-intacct/Configuration/PlatformAppErpConfigProvider.cs` — updated query flow to three objects: master (no filter) → location config (RLOC filter) → field config (Rdutchielocationconfig filter); uses `DutchieMasterConfigRow.FromXElement` and `DutchieLocationConfigRow.FromXElement`

**Added**

- `src/dutchie-intacct/Configuration/DutchieLocationConfigRow.cs` — new model for `dutchie_location_config` rows; includes `FromXElement` factory; fields: RecordNo, MasterConfigRecordNo, LocationId, EntityId, DutchieLocationKey, DutchieIntegratorKey, DefaultCustomerId, DefaultDepartmentId, DefaultItemId

**Changed**

- `src/dutchie-integration/Models/ErpMappingConfig.cs` — added `DefaultItemId`, `DutchieLocationKey`, `DutchieIntegratorKey` properties

---

## 2026-03-09 — Per-location master config fields

**Changed**

- `platform-app/DutchieIntegration.xml` — added fields to `dutchie_master_config`: `entity_id` (STR0), `dutchie_location_key` (STR1), `dutchie_integrator_key` (STR2), `RDEPARTMENT` (INTG3, relId 20100), `RITEM` (INTG4, relId 20101); added back-ref fields on standard `department` and `item` objects
- `src/dutchie-intacct/Configuration/DutchieMasterConfigRow.cs` — added EntityId, DutchieLocationKey, DutchieIntegratorKey, DefaultDepartmentId, DefaultItemId; added `FromXElement` factory method

---

## 2026-03-09 — Platform Application initial implementation

**Added**

- `platform-app/DutchieIntegration.xml` — Intacct Platform Application definition; three custom objects: `dutchie_config` (GL mapping rows), `dutchie_master_config` (per-location journal/customer/tolerance), `dutchie_process_log` (sync audit log); custom fields on standard `location` (credentials, enabled flag, location type) and `customer` (DUTCHIE_CUSTOMER_ID) objects; import map for bulk field config entry
- `src/dutchie-intacct/Configuration/PlatformAppErpConfigProvider.cs` — full implementation querying `dutchie_master_config` and `dutchie_config` via Intacct SDK `QueryFunction`; builds `ErpMappingConfig` from Platform App data
- `src/dutchie-intacct/Configuration/IntacctOptions.cs` — added `LocationId` property for scoping Platform App queries

---

## 2026-03-09 — Newtonsoft.Json migration

**Changed**

- `src/dutchie-library/Clients/DutchieClientBase.cs` — replaced `System.Text.Json` with `Newtonsoft.Json`; `CamelCasePropertyNamesContractResolver`, `NullValueHandling.Ignore`, `StringEnumConverter(CamelCaseNamingStrategy)`
- `src/dutchie-integration/State/JsonFileSyncStateStore.cs` — replaced `System.Text.Json` with `Newtonsoft.Json`; `Formatting.Indented`
- `src/dutchie-library/dutchie-library.csproj` — added `Newtonsoft.Json 13.0.3`
- `src/dutchie-integration/dutchie-integration.csproj` — added `Newtonsoft.Json 13.0.3`

---

## 2026-03-09 — .env file support and credential management

**Added**

- `.env.example` — credential template for Dutchie and Intacct environment variables
- `.gitignore` — initial file; excludes `.env`, `bin/`, `obj/`, `.vs/`, `sync-state.json`, `secrets.json`, `.DS_Store`
- `src/dutchie-worker/dutchie-worker.csproj` — added `DotNetEnv 3.1.1`

**Changed**

- `src/dutchie-worker/Program.cs` — added `Env.Load(".env", new LoadOptions(Env.TraversePath(), clobberExistingVars: false))` before host builder; switched Intacct registration to `.UsePlatformAppConfig()` by default
- `src/dutchie-worker/appsettings.json` — removed `DutchieErpMappings` section; all GL mapping config now sourced from Intacct Platform App; added `Intacct:LocationId`

---

## 2026-03-09 — Initial project setup

**Added**

- `dutchie-library.slnx` — solution file (.slnx format)
- `src/dutchie-library/` — Dutchie POS REST API client: `DutchieClientBase`, `ReportingClient`, `ProductClient`, `DutchieAuthHandler` (HTTP Basic auth), `DutchieApiException`, `DutchieClientOptions`, `DutchieServiceCollectionExtensions`; models: `ClosingReport`, `Transaction`, `RegisterTransaction`, `RegisterCashSummary`, `TransactionQueryRequest`, `ProductDetail`
- `src/dutchie-integration/` — ERP-neutral abstractions (`IErpConnector`, `IErpConfigProvider`, `ISyncStateStore`), sync pipelines (`ClosingReportSyncPipeline`, `TransactionSyncPipeline`), payloads (`JournalEntryPayload`, `SalesTransactionPayload`), `ErpMappingConfig`, `JsonFileSyncStateStore`, `IntegrationServiceCollectionExtensions`
- `src/dutchie-intacct/` — Sage Intacct SDK connector (`IntacctErpConnector`), `AppSettingsErpConfigProvider`, `IntacctOptions`, `IntacctServiceCollectionExtensions` with `UsePlatformAppConfig()` chaining
- `src/dutchie-worker/` — Generic Host worker service: `ClosingReportWorker` (24h), `TransactionSyncWorker` (15m), `WorkerOptions`, `Program.cs`, `appsettings.json`
