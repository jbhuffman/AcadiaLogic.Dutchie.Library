# Features — AcadiaLogic.Dutchie.Library

This document describes the features implemented in the current release and the features planned for future releases.

---

## Current Features

### Dutchie POS API Client

**Typed HTTP client** for the Dutchie POS REST API (`AcadiaLogic.Dutchie.Library`).

- HTTP Basic authentication via `LocationKey:IntegratorKey` injected by a `DelegatingHandler` — no auth boilerplate in call sites.
- Fully typed response models for:
  - Closing reports (`ClosingReport`)
  - Register transactions (`Transaction`, line items, payment methods, taxes)
  - Cash summaries
  - Product catalog
- Newtonsoft.Json deserialization with camel-case contract and enum support.
- **Automatic retry on HTTP 500:** up to 5 retries with a 3-second fixed delay. Each retry logs a `Warning`; exhausted retries log an `Error`. All other status codes are not retried.
- Registered via `services.AddDutchieClient(configuration)` — single-line DI setup.

---

### Closing Report Sync

**Scheduled sync of the Dutchie daily closing report to Intacct GL** (`ClosingReportWorker` + `ClosingReportSyncPipeline`).

- Runs on a configurable interval (default: every 24 hours).
- Pulls the closing report for a configurable lookback window (default: last 24 hours).
- Maps closing-report fields to Intacct GL accounts via `dutchie_field_config` rules stored in the Intacct Platform App.
- Supports four input types for field mapping:
  - `header` — top-level summary fields (e.g., `cannabisSales`, `totalDiscount`, `tips`, `fees`)
  - `paymentSummary` — per-payment-method lines (e.g., `Cash`, `CreditCard`, `CanPay`)
  - `taxRateSummary` — per-tax-rate lines (e.g., `Excise Tax`, `State Sales Tax`)
  - `categorySummary` — per-product-category lines *(config supported; pipeline mapping pending)*
- Per-line control of credit/debit designation, target GL account, and dimension overrides (customer, department, item, class).
- Per-line amount mode: `net` (default), `gross`, or `discount`.
- Per-line entry type: `revenue` or `cogs`.
- Posts journal entry as **Draft** or **Posted** based on the `is_live` flag in `dutchie_master_config`.
- Recovers gracefully from errors; the process continues running and retries on the next interval.

---

### Transaction Sync

**Scheduled incremental sync of Dutchie transactions to Intacct AR invoices** (`TransactionSyncWorker` + `TransactionSyncPipeline`).

- Runs on a configurable interval (default: every 15 minutes).
- Pulls only transactions modified since the last successful sync (watermark-based incremental pull).
- Watermark persisted to `sync-state.json` — survives restarts; no transactions skipped or duplicated on resume.
- Maps each transaction to an Intacct AR invoice using configured default customer, item, and department.
- Recovers gracefully from errors; the process continues running and retries on the next interval.

---

### Intacct Platform Application — Configuration via Custom Objects

**All GL mapping configuration managed inside Sage Intacct** (`dutchie_master_config`, `dutchie_location_config`, `dutchie_field_config`, `dutchie_process_log`).

- No custom fields added to standard Intacct objects (location, customer, department, etc.) — all configuration lives in purpose-built custom objects that reference standard objects via relationships. Prevents conflicts with other customizations.
- **`dutchie_master_config`** (one per company):
  - GL Journal target for closing-report entries
  - Maximum over/short cash tolerance
  - Live/Draft posting flag
- **`dutchie_location_config`** (one per syncing location):
  - Links to an Intacct Location and to the master config
  - Holds per-location Dutchie API credentials (Location Key, Integrator Key)
  - Optional Entity ID override for multi-entity SDK sessions
  - Default Customer, Department, and Item dimensions for unmapped transactions
- **`dutchie_field_config`** (many per location):
  - One record per GL mapping rule
  - Full per-line dimension and amount overrides
- **`dutchie_process_log`** (schema defined; write implementation pending):
  - Audit log of each sync run: job name, status, records processed, errors

---

### ERP-Neutral Abstraction Layer

**Pluggable connector architecture** (`AcadiaLogic.Dutchie.Integration`).

- `IErpConnector` — post journal entries and AR invoices to any ERP.
- `IErpConfigProvider` — load GL mapping configuration from any source.
- `ISyncStateStore` — read/write incremental sync watermarks.
- Sync pipelines (`ClosingReportSyncPipeline`, `TransactionSyncPipeline`) depend only on these interfaces — no Intacct-specific code in the pipeline layer.
- A second ERP target can be added by implementing the three interfaces and registering them in DI, with no changes to pipeline code.

---

### Sage Intacct Connector

**Full Intacct SDK integration** (`AcadiaLogic.Dutchie.Intacct`).

- Uses official `Intacct.SDK` 3.2.2.
- Posts GL journal entries via `JournalEntryCreate` / `JournalEntryLineCreate`.
- Posts AR invoices via `InvoiceCreate` / `InvoiceLineCreate`.
- Queries custom Platform App objects via `QueryFunction` with typed field selection and filtering.
- Two config providers selectable at registration time:
  - `PlatformAppErpConfigProvider` — reads live config from Intacct custom objects (production).
  - `AppSettingsErpConfigProvider` — reads config from `appsettings.json` (development / simple deployments).

---

### Structured Logging with NLog

**Structured, multi-target logging** via Microsoft.Extensions.Logging + NLog.

- Coloured console output at Info+ level.
- Rolling file at Debug+ level (`logs/dutchie-worker.log`), 30-day / 100 MB per file retention.
- Errors-only file at Error+ level (`logs/dutchie-errors.log`), 90-day retention.
- Microsoft framework internals suppressed below Warn to reduce noise.
- All logs use structured message templates with named parameters.

---

### Credential and Configuration Management

- Credentials supplied via environment variables only — never in committed source files.
- Optional `.env` file support via DotNetEnv 3.1.1; real environment variables always win.
- `__` double-underscore env var convention maps directly to `appsettings.json` sections (e.g., `INTACCT__COMPANYID` → `Intacct:CompanyId`).
- All configurable intervals and paths are settable without code changes.

---

## Planned Features

The following features are documented in `CLAUDE_CONTEXT.md` as pending / future work:

| Feature | Description |
|---|---|
| Multi-location worker loop | Run both sync jobs for every `dutchie_location_config` record in a single process, using per-location Dutchie credentials loaded from Intacct |
| Intacct-driven Dutchie credentials | Allow `DutchieLocationKey` and `DutchieIntegratorKey` to come entirely from the Platform App, eliminating the need for Dutchie env vars |
| Process log writer | Write a `dutchie_process_log` record to Intacct after each sync run (schema already defined in Platform App) |
| `categorySummary` pipeline support | Map per-product-category revenue lines in the closing report to GL accounts (field config already supports this input type) |
| `customerTypeSummary` pipeline support | Map per-customer-type revenue lines to GL accounts |
| HA sync state store | Database-backed `ISyncStateStore` implementation for multi-instance / high-availability deployments |
| Product sync | `ProductSyncPipeline` and worker to sync the Dutchie product catalog to Intacct items |
