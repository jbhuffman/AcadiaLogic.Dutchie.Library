# Requirements — DutchieLibrary

This document defines the functional and non-functional requirements for the Dutchie POS → Sage Intacct integration.

---

## 1. Overview

The integration synchronises Dutchie POS financial data to Sage Intacct on a scheduled basis. It runs as a headless .NET 8 worker service with no UI. Configuration is stored in Sage Intacct via a custom Platform Application; credentials are supplied via environment variables.

---

## 2. Stakeholders

| Role | Concern |
|---|---|
| Cannabis retailer / operator | Accurate, timely GL postings from POS system |
| Controller / accountant | Journal entries match POS daily reports; AR invoices for transactions |
| IT / DevOps | Reliable deployment, minimal dependencies, observable operation |
| Integrator / developer | Extensible to future ERPs; clear configuration model |

---

## 3. Functional Requirements

### 3.1 Closing Report Sync

| ID | Requirement |
|---|---|
| FR-01 | The system shall pull the Dutchie daily closing report on a configurable schedule (default: every 24 hours). |
| FR-02 | The closing report pull shall cover a configurable lookback window (default: last 24 hours). |
| FR-03 | The system shall map each closing-report field to an Intacct GL account according to the `dutchie_field_config` rules. |
| FR-04 | The system shall generate one Intacct journal entry per closing report run. |
| FR-05 | Each journal entry line shall carry credit/debit designation, GL account, and optional dimension overrides (customer, department, item, class) as configured. |
| FR-06 | The system shall post the journal entry as Draft when `dutchie_master_config.is_live = false`, and as Posted when `is_live = true`. |
| FR-07 | The system shall respect the `maximum_overshort` tolerance; cash variance within tolerance shall not cause an error. |
| FR-08 | Revenue, payment, and tax lines shall be separately mappable via `input_type` (`header`, `paymentSummary`, `taxRateSummary`). |

### 3.2 Transaction Sync

| ID | Requirement |
|---|---|
| FR-10 | The system shall pull Dutchie transactions modified since the last successful sync on a configurable schedule (default: every 15 minutes). |
| FR-11 | The system shall persist an incremental sync watermark (timestamp) across restarts so no transactions are missed or duplicated on resume. |
| FR-12 | Each transaction shall be mapped to an Intacct AR invoice using the configured default customer, item, and department. |
| FR-13 | The system shall record the watermark only after all transactions in a batch have been successfully posted. |

### 3.3 Configuration

| ID | Requirement |
|---|---|
| FR-20 | All GL mapping configuration shall be stored in Sage Intacct via the Dutchie Integration Platform Application (custom objects). |
| FR-21 | The system shall support one `dutchie_master_config` record per company for company-level defaults. |
| FR-22 | The system shall support one `dutchie_location_config` record per Intacct Location, each holding per-location Dutchie credentials and dimension defaults. |
| FR-23 | The system shall support many `dutchie_field_config` records per location config, each mapping one Dutchie closing-report field to an Intacct GL account. |
| FR-24 | Credentials (Dutchie keys, Intacct Web Services credentials) shall be supplied via environment variables and shall not be stored in committed source files. |
| FR-25 | All non-credential settings shall be configurable via `appsettings.json` and overridable by environment variables. |

### 3.4 Reliability and Error Handling

| ID | Requirement |
|---|---|
| FR-30 | The system shall retry Dutchie API calls that return HTTP 500 up to 5 times with a 3-second delay between attempts. |
| FR-31 | If all retries are exhausted, the system shall log an error and surface the failure; the sync job shall not crash the process. |
| FR-32 | Each sync run shall log its outcome (success, failure, records processed) to the `dutchie_process_log` Platform App object. *(Pending implementation.)* |
| FR-33 | The system shall continue running and retry on the next scheduled interval following a failed sync run. |

### 3.5 Observability

| ID | Requirement |
|---|---|
| FR-40 | The system shall emit structured logs using Microsoft.Extensions.Logging (MEL) message templates. |
| FR-41 | Logs shall be written to a timestamped, coloured console (Info+), a rolling file (Debug+, 30-day retention), and an errors-only file (Error+, 90-day retention). |
| FR-42 | Log output shall identify the sync job, attempt number, and relevant identifiers (e.g., path, location) in each message. |

---

## 4. Non-Functional Requirements

### 4.1 Platform

| ID | Requirement |
|---|---|
| NFR-01 | The solution shall target .NET 8. |
| NFR-02 | The solution shall be cross-platform (Windows, Linux, macOS). |
| NFR-03 | The worker shall require no UI and shall be suitable for headless server deployment (systemd, Docker, Windows Service). |

### 4.2 Performance

| ID | Requirement |
|---|---|
| NFR-10 | The closing report sync shall complete within 60 seconds under normal conditions. |
| NFR-11 | The transaction sync shall handle at least 500 transactions per run without timeout. |
| NFR-12 | The worker shall consume minimal resources when idle between scheduled runs. |

### 4.3 Maintainability

| ID | Requirement |
|---|---|
| NFR-20 | The ERP connector layer (`IErpConnector`, `IErpConfigProvider`) shall be abstract so a second ERP target can be added without modifying sync pipeline code. |
| NFR-21 | All source code shall use C# 12 language features, nullable reference types, and implicit usings. |
| NFR-22 | JSON serialization shall use Newtonsoft.Json 13.x throughout; System.Text.Json shall not be used. |
| NFR-23 | `.ConfigureAwait(false)` shall be used on all `await` calls in library, integration, and Intacct layers. |

### 4.4 Security

| ID | Requirement |
|---|---|
| NFR-30 | Credentials shall never appear in committed source files, `appsettings.json`, or logs. |
| NFR-31 | The `.env` file (if used) shall be listed in `.gitignore`. |
| NFR-32 | Real environment variables shall always override `.env` file values (`clobberExistingVars: false`). |
| NFR-33 | Intacct communication shall use the official Intacct SDK (XML Gateway over HTTPS). |

### 4.5 Deployment

| ID | Requirement |
|---|---|
| NFR-40 | The solution shall be publishable as a framework-dependent or self-contained executable via `dotnet publish`. |
| NFR-41 | The Platform Application XML (`DutchieIntegration.xml`) shall be idempotent — safe to re-import over an existing deployment. |
| NFR-42 | The sync watermark file (`sync-state.json`) shall survive restarts and updates without data loss. |

---

## 5. Constraints

- Dutchie closing-report date range must be between 12 hours and 31 days.
- Dutchie cash-summary `fromLastModifiedDate` must be within the last 7 days.
- Dutchie transaction queries using `TransactionId`, date range, and last-modified range are mutually exclusive — only one may be active per request.
- The Intacct SDK requires a valid Web Services Sender ID and Sender Password provisioned by Sage Intacct Support.
- The Intacct Platform Application must be deployed and configured before the worker is started; the worker does not auto-create Platform App objects.

---

## 6. Out of Scope (Current Release)

The following are documented as future work and are not required for the current release:

- Multi-location worker loop (currently one `LocationId` per process instance)
- Dutchie credentials sourced entirely from Intacct (without env vars)
- `dutchie_process_log` write implementation
- `categorySummary` / `customerTypeSummary` pipeline support
- High-availability (database-backed) sync state store
- Product sync pipeline and worker
