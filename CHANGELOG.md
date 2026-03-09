# Changelog

All notable changes to this project are documented here.
Format: `YYYY-MM-DD HH:MM UTC` — `Category` — description. New entries go at the top.

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

- `src/AcadiaLogic.Dutchie.Worker/nlog.config` — NLog configuration with three targets: coloured console (Info+), rolling file `logs/dutchie-worker.log` (Debug+, 30-day retention), errors-only file `logs/dutchie-errors.log` (Error+, 90-day retention); Microsoft internals suppressed below Warn
- `src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj` — added `NLog.Extensions.Hosting 5.3.15`; added `nlog.config` as `PreserveNewest` content item

**Changed**

- `src/AcadiaLogic.Dutchie.Library/Clients/DutchieClientBase.cs` — added `ILogger` constructor parameter and retry loop in `GetAsync`: up to 5 retries on HTTP 500, 3-second fixed delay between attempts; logs `Warning` per retry and `Error` when retries exhausted
- `src/AcadiaLogic.Dutchie.Library/Clients/ReportingClient.cs` — added `ILogger<ReportingClient>` constructor parameter, passed to base
- `src/AcadiaLogic.Dutchie.Library/Clients/ProductClient.cs` — added `ILogger<ProductClient>` constructor parameter, passed to base
- `src/AcadiaLogic.Dutchie.Worker/Program.cs` — added `builder.Logging.ClearProviders()` and `builder.Logging.AddNLog()` to wire NLog as the MEL provider

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
- `src/AcadiaLogic.Dutchie.Intacct/Configuration/DutchieMasterConfigRow.cs` — simplified to company-level fields only (GlJournalSymbol, MaximumOverShort, IsLive)
- `src/AcadiaLogic.Dutchie.Intacct/Configuration/PlatformAppErpConfigProvider.cs` — updated query flow to three objects: master (no filter) → location config (RLOC filter) → field config (Rdutchielocationconfig filter); uses `DutchieMasterConfigRow.FromXElement` and `DutchieLocationConfigRow.FromXElement`

**Added**

- `src/AcadiaLogic.Dutchie.Intacct/Configuration/DutchieLocationConfigRow.cs` — new model for `dutchie_location_config` rows; includes `FromXElement` factory; fields: RecordNo, MasterConfigRecordNo, LocationId, EntityId, DutchieLocationKey, DutchieIntegratorKey, DefaultCustomerId, DefaultDepartmentId, DefaultItemId

**Changed**

- `src/AcadiaLogic.Dutchie.Integration/Models/ErpMappingConfig.cs` — added `DefaultItemId`, `DutchieLocationKey`, `DutchieIntegratorKey` properties

---

## 2026-03-09 — Per-location master config fields

**Changed**

- `platform-app/DutchieIntegration.xml` — added fields to `dutchie_master_config`: `entity_id` (STR0), `dutchie_location_key` (STR1), `dutchie_integrator_key` (STR2), `RDEPARTMENT` (INTG3, relId 20100), `RITEM` (INTG4, relId 20101); added back-ref fields on standard `department` and `item` objects
- `src/AcadiaLogic.Dutchie.Intacct/Configuration/DutchieMasterConfigRow.cs` — added EntityId, DutchieLocationKey, DutchieIntegratorKey, DefaultDepartmentId, DefaultItemId; added `FromXElement` factory method

---

## 2026-03-09 — Platform Application initial implementation

**Added**

- `platform-app/DutchieIntegration.xml` — Intacct Platform Application definition; three custom objects: `dutchie_config` (GL mapping rows), `dutchie_master_config` (per-location journal/customer/tolerance), `dutchie_process_log` (sync audit log); custom fields on standard `location` (credentials, enabled flag, location type) and `customer` (DUTCHIE_CUSTOMER_ID) objects; import map for bulk field config entry
- `src/AcadiaLogic.Dutchie.Intacct/Configuration/PlatformAppErpConfigProvider.cs` — full implementation querying `dutchie_master_config` and `dutchie_config` via Intacct SDK `QueryFunction`; builds `ErpMappingConfig` from Platform App data
- `src/AcadiaLogic.Dutchie.Intacct/Configuration/IntacctOptions.cs` — added `LocationId` property for scoping Platform App queries

---

## 2026-03-09 — Newtonsoft.Json migration

**Changed**

- `src/AcadiaLogic.Dutchie.Library/Clients/DutchieClientBase.cs` — replaced `System.Text.Json` with `Newtonsoft.Json`; `CamelCasePropertyNamesContractResolver`, `NullValueHandling.Ignore`, `StringEnumConverter(CamelCaseNamingStrategy)`
- `src/AcadiaLogic.Dutchie.Integration/State/JsonFileSyncStateStore.cs` — replaced `System.Text.Json` with `Newtonsoft.Json`; `Formatting.Indented`
- `src/AcadiaLogic.Dutchie.Library/AcadiaLogic.Dutchie.Library.csproj` — added `Newtonsoft.Json 13.0.3`
- `src/AcadiaLogic.Dutchie.Integration/AcadiaLogic.Dutchie.Integration.csproj` — added `Newtonsoft.Json 13.0.3`

---

## 2026-03-09 — .env file support and credential management

**Added**

- `.env.example` — credential template for Dutchie and Intacct environment variables
- `.gitignore` — initial file; excludes `.env`, `bin/`, `obj/`, `.vs/`, `sync-state.json`, `secrets.json`, `.DS_Store`
- `src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj` — added `DotNetEnv 3.1.1`

**Changed**

- `src/AcadiaLogic.Dutchie.Worker/Program.cs` — added `Env.Load(".env", new LoadOptions(Env.TraversePath(), clobberExistingVars: false))` before host builder; switched Intacct registration to `.UsePlatformAppConfig()` by default
- `src/AcadiaLogic.Dutchie.Worker/appsettings.json` — removed `DutchieErpMappings` section; all GL mapping config now sourced from Intacct Platform App; added `Intacct:LocationId`

---

## 2026-03-09 — Initial project setup

**Added**

- `AcadiaLogic.Dutchie.Library.slnx` — solution file (.slnx format)
- `src/AcadiaLogic.Dutchie.Library/` — Dutchie POS REST API client: `DutchieClientBase`, `ReportingClient`, `ProductClient`, `DutchieAuthHandler` (HTTP Basic auth), `DutchieApiException`, `DutchieClientOptions`, `DutchieServiceCollectionExtensions`; models: `ClosingReport`, `Transaction`, `RegisterTransaction`, `RegisterCashSummary`, `TransactionQueryRequest`, `ProductDetail`
- `src/AcadiaLogic.Dutchie.Integration/` — ERP-neutral abstractions (`IErpConnector`, `IErpConfigProvider`, `ISyncStateStore`), sync pipelines (`ClosingReportSyncPipeline`, `TransactionSyncPipeline`), payloads (`JournalEntryPayload`, `SalesTransactionPayload`), `ErpMappingConfig`, `JsonFileSyncStateStore`, `IntegrationServiceCollectionExtensions`
- `src/AcadiaLogic.Dutchie.Intacct/` — Sage Intacct SDK connector (`IntacctErpConnector`), `AppSettingsErpConfigProvider`, `IntacctOptions`, `IntacctServiceCollectionExtensions` with `UsePlatformAppConfig()` chaining
- `src/AcadiaLogic.Dutchie.Worker/` — Generic Host worker service: `ClosingReportWorker` (24h), `TransactionSyncWorker` (15m), `WorkerOptions`, `Program.cs`, `appsettings.json`
