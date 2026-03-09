# AcadiaLogic.Dutchie.Library

A .NET 8 integration library and worker service that syncs **Dutchie POS** data to **Sage Intacct** (and future ERP targets) on a scheduled basis.

## Projects

| Project | Purpose |
|---|---|
| `AcadiaLogic.Dutchie.Library` | Typed HTTP client for the Dutchie POS REST API |
| `AcadiaLogic.Dutchie.Integration` | ERP-neutral sync pipelines, abstractions, and state management |
| `AcadiaLogic.Dutchie.Intacct` | Sage Intacct SDK connector and Platform App configuration provider |
| `AcadiaLogic.Dutchie.Worker` | .NET Generic Host worker service running the two sync jobs |

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│  AcadiaLogic.Dutchie.Worker (.NET Generic Host)                      │
│                                                                      │
│  ClosingReportWorker (24h)          TransactionSyncWorker (15m)      │
│         │                                    │                       │
│  ┌──────▼──────────────────────────────────▼──────┐                 │
│  │         AcadiaLogic.Dutchie.Integration         │                 │
│  │  ClosingReportSyncPipeline  TransactionSyncPipeline               │
│  │  IErpConnector (abstract)   ISyncStateStore (abstract)            │
│  └──────┬──────────────────────────────────┬───────┘                 │
│         │                                  │                         │
│  ┌──────▼──────────┐              ┌────────▼───────┐                 │
│  │ Dutchie.Library │              │ Dutchie.Intacct│                 │
│  │  IReportingClient              │ IntacctErpConnector               │
│  │  IProductClient │              │ PlatformAppErpConfigProvider      │
│  └─────────────────┘              └────────────────┘                 │
│         │                                  │                         │
│  Dutchie POS API              Sage Intacct XML Gateway               │
└──────────────────────────────────────────────────────────────────────┘
```

### Sync Jobs

**Closing Report Worker** (default: every 24 hours)
Pulls the Dutchie daily closing report → maps revenue, payment, and tax lines to GL accounts → posts a journal entry to Intacct.

**Transaction Sync Worker** (default: every 15 minutes)
Incrementally pulls Dutchie transactions modified since the last successful run → maps each transaction to an Intacct AR invoice. Stores a watermark timestamp in `sync-state.json` so restarts pick up where they left off.

---

## Getting Started

### Prerequisites

- .NET 8 SDK
- A Dutchie POS account with API access (LocationKey + IntegratorKey)
- A Sage Intacct company with Web Services credentials (CompanyId, UserId, Password, SenderId, SenderPassword)
- The **Dutchie Integration Platform Application** deployed in Intacct (see [Platform App](#platform-application))

### Credentials Setup

Copy `.env.example` to `.env` and fill in your credentials:

```sh
cp .env.example .env
```

```dotenv
# Dutchie POS
DUTCHIE__LOCATIONKEY=your-location-key
DUTCHIE__INTEGRATORKEY=your-integrator-key

# Sage Intacct
INTACCT__COMPANYID=your-company-id
INTACCT__USERID=your-web-services-user
INTACCT__USERPASSWORD=your-user-password
INTACCT__SENDERID=your-sender-id
INTACCT__SENDERPASSWORD=your-sender-password
INTACCT__ENTITYID=                        # optional: multi-entity login context
INTACCT__LOCATIONID=your-intacct-location # filters Platform App config to this location
```

The `.env` file is optional — real environment variables always take precedence. Never commit `.env` to source control (it is listed in `.gitignore`).

### Run the Worker

```sh
cd src/AcadiaLogic.Dutchie.Worker
dotnet run
```

---

## Configuration

All settings live in `appsettings.json` and can be overridden by environment variables using the `__` double-underscore convention (e.g., `DUTCHIE__LOCATIONKEY` → `Dutchie:LocationKey`).

### `appsettings.json` reference

```json
{
  "Dutchie": {
    "LocationKey":    "",
    "IntegratorKey":  "",
    "BaseUrl":        "https://api.pos.dutchie.com"
  },
  "Intacct": {
    "CompanyId":      "",
    "UserId":         "",
    "UserPassword":   "",
    "SenderId":       "",
    "SenderPassword": "",
    "EntityId":       "",
    "LocationId":     ""
  },
  "Worker": {
    "SyncStateFile":           "sync-state.json",
    "ClosingReportInterval":   "24:00:00",
    "ClosingReportLookback":   "24:00:00",
    "TransactionSyncInterval": "00:15:00"
  }
}
```

| Key | Description | Default |
|---|---|---|
| `Dutchie:BaseUrl` | Dutchie API base URL | `https://api.pos.dutchie.com` |
| `Intacct:EntityId` | Intacct entity ID for multi-entity companies | *(top-level)* |
| `Intacct:LocationId` | Intacct Location ID used to scope Platform App config queries | *(all locations)* |
| `Worker:ClosingReportInterval` | How often the closing report job runs | `24:00:00` |
| `Worker:ClosingReportLookback` | How far back each closing report pull covers | `24:00:00` |
| `Worker:TransactionSyncInterval` | How often the transaction sync job runs | `00:15:00` |
| `Worker:SyncStateFile` | Path to the watermark state file | `sync-state.json` |

---

## Platform Application

The Intacct-side configuration is managed entirely through a **Dutchie Integration Platform Application** — a set of custom objects deployed to your Intacct company. No custom fields are added to standard Intacct objects; all configuration lives in purpose-built custom objects that link to standard objects via relationships.

The Platform App XML is located at [`platform-app/DutchieIntegration.xml`](platform-app/DutchieIntegration.xml).

### Object Hierarchy

```
dutchie_master_config          Company-level: GL journal, over/short tolerance, live flag
  └── dutchie_location_config  Per-location:  Dutchie credentials, default customer/department/item
        └── dutchie_field_config  Per-field:  Closing report field → GL account mapping rows
```

### `dutchie_master_config`

One record per company. Sets defaults shared by all locations.

| Field | Type | Description |
|---|---|---|
| GL Journal | Relationship → `gljournal` | Journal to post closing-report entries to |
| Maximum Over/Short | Decimal | Cash discrepancy tolerance in dollars |
| Live (Post, Not Draft) | Checkbox | When unchecked, entries are held as Draft |

### `dutchie_location_config`

One record per Intacct Location that syncs with Dutchie.

| Field | Type | Description |
|---|---|---|
| Master Config | Relationship → `dutchie_master_config` | Inherits journal and tolerance settings |
| Location | Relationship → `location` | The Intacct Location this config applies to |
| Entity ID Override | Text | Optional SDK entity context override (multi-entity) |
| Dutchie - Location Key | Text | Dutchie Basic Auth username for this location |
| Dutchie - Integrator Key | Text | Dutchie Basic Auth password for this location |
| Default Customer | Relationship → `customer` | Walk-in customer for unmatched transactions |
| Default Department | Relationship → `department` | Default department dimension |
| Default Item | Relationship → `item` | Default item for AR invoice lines |

### `dutchie_field_config`

One record per GL mapping rule — links a Dutchie closing-report field to an Intacct GL account.

| Field | Type | Description |
|---|---|---|
| Location Config | Relationship → `dutchie_location_config` | Scopes this row to a location |
| Input Type | Picklist | `header` / `paymentSummary` / `taxRateSummary` / `categorySummary` |
| API Field | Text | Field name from the Dutchie API (e.g. `cannabisSales`, `Cash`, `Excise Tax`) |
| Credit / Debit | Radio | `C` = Credit line, `D` = Debit line |
| GL Account | Relationship → `glaccount` | Target Intacct GL account |
| Customer Override | Relationship → `customer` | Overrides location default for this line |
| Department Override | Relationship → `department` | Overrides location default for this line |
| Item Override | Relationship → `item` | Overrides location default for this line |
| Class Override | Relationship → `class` | Optional class dimension |
| Amount | Radio | `net` (default) / `gross` / `discount` |
| Entry Type | Radio | `revenue` / `cogs` |

#### `input_type = header` — common `api_field` values

| `api_field` | Description |
|---|---|
| `cannabisSales` | Gross cannabis sales revenue |
| `nonCannabisSales` | Non-cannabis (accessories, etc.) revenue |
| `totalDiscount` | Total discounts applied |
| `defaultTax` | Tax collected (catch-all) |
| `tips` | Tips collected |
| `fees` | Fees and donations |

#### `input_type = paymentSummary` — `api_field` = payment method name

Examples: `Cash`, `CreditCard`, `DebitCard`, `CanPay`, `Check`, `GiftCard`

#### `input_type = taxRateSummary` — `api_field` = tax rate label

Use the exact tax rate name as returned by the Dutchie API for per-rate GL account overrides.

### `dutchie_process_log`

Written automatically by each sync job run.

| Field | Description |
|---|---|
| Job Name | `ClosingReport` or `Transactions` |
| Status | `complete` / `failed` / `in_progress` / `in_queue` |
| Records Processed | Count of records posted in this run |
| Raw Errors | Full exception text |
| Summarized Errors | Human-readable error summary |
| Location Config | Link to the location config this log entry belongs to |

---

## Dutchie API Client

The `AcadiaLogic.Dutchie.Library` project provides a fully typed HTTP client for the Dutchie POS REST API.

### Registration

```csharp
// Bind from appsettings "Dutchie" section
services.AddDutchieClient(configuration);

// Or configure directly
services.AddDutchieClient(options =>
{
    options.LocationKey   = "your-location-key";
    options.IntegratorKey = "your-integrator-key";
});
```

### Available Endpoints

| Interface | Method | Endpoint |
|---|---|---|
| `IReportingClient` | `GetClosingReportAsync` | `GET /reporting/closing-report` |
| `IReportingClient` | `GetRegisterTransactionsAsync` | `GET /reporting/register-transactions` |
| `IReportingClient` | `GetTransactionsAsync` | `GET /reporting/transactions` |
| `IReportingClient` | `GetCashSummaryAsync` | `GET /reporting/cash-summary` |
| `IProductClient` | `GetProductsAsync` | `GET /products` |

**API constraints:**
- `/reporting/closing-report` — date range must be between 12 hours and 31 days
- `/reporting/cash-summary` — `fromLastModifiedDate` must be within the last 7 days
- `/reporting/transactions` — `TransactionId`, date range, and last-modified range are mutually exclusive

### Retry Behavior

The client automatically retries on HTTP 500 responses (transient Dutchie service errors):
- Up to **5 retries** with a **3-second delay** between each attempt
- `Warning` log per retry attempt
- `Error` log when all retries are exhausted
- All other status codes (4xx, other 5xx) are not retried

---

## ERP Integration Abstractions

The `AcadiaLogic.Dutchie.Integration` project defines ERP-neutral interfaces that decouple the sync pipelines from any specific ERP.

### Key Interfaces

**`IErpConnector`** — Post data to the ERP:
```csharp
Task PostJournalEntryAsync(JournalEntryPayload payload, CancellationToken ct);
Task PostSalesTransactionAsync(SalesTransactionPayload payload, CancellationToken ct);
```

**`IErpConfigProvider`** — Load GL account mapping configuration:
```csharp
Task<ErpMappingConfig> GetConfigAsync(CancellationToken ct);
```

**`ISyncStateStore`** — Persist incremental sync watermarks:
```csharp
Task<DateTimeOffset?> GetLastSyncTimeAsync(string jobName, CancellationToken ct);
Task SetLastSyncTimeAsync(string jobName, DateTimeOffset syncTime, CancellationToken ct);
```

### DI Registration

```csharp
services.AddDutchieIntegration(state =>
{
    state.FilePath = "sync-state.json"; // watermark file path
});
```

### Extending to Another ERP

1. Implement `IErpConnector` for your target ERP
2. Implement `IErpConfigProvider` to load mapping config from your ERP's configuration system
3. Register your implementations in place of the Intacct ones

---

## Sage Intacct Connector

### Registration

```csharp
// Bind from appsettings "Intacct" section, use Platform App for GL mapping config
services.AddIntacctConnector(configuration)
        .UsePlatformAppConfig();

// Or use appsettings-based GL mapping (development / simple deployments)
services.AddIntacctConnector(configuration);
```

### Config Providers

| Provider | Source | Activation |
|---|---|---|
| `AppSettingsErpConfigProvider` | `appsettings.json` `DutchieErpMappings` section | Default |
| `PlatformAppErpConfigProvider` | Intacct `dutchie_master_config`, `dutchie_location_config`, `dutchie_field_config` objects | `.UsePlatformAppConfig()` |

---

## Logging

The worker uses [NLog](https://nlog-project.org/) via the Microsoft.Extensions.Logging bridge. Configuration is in [`nlog.config`](src/AcadiaLogic.Dutchie.Worker/nlog.config).

| Target | Level | Location |
|---|---|---|
| Console | Info+ | Coloured, timestamped |
| Rolling file | Debug+ | `logs/dutchie-worker.log` (30-day retention, 100 MB max per file) |
| Errors file | Error+ | `logs/dutchie-errors.log` (90-day retention) |

Microsoft framework internals are suppressed below `Warn` to reduce noise.

---

## Solution Structure

```
AcadiaLogic.Dutchie.Library.slnx
├── platform-app/
│   └── DutchieIntegration.xml          Intacct Platform Application definition
├── src/
│   ├── AcadiaLogic.Dutchie.Library/
│   │   ├── Authentication/             HTTP Basic auth delegating handler
│   │   ├── Clients/                    IReportingClient, IProductClient + implementations
│   │   └── Models/                     Dutchie API response models
│   ├── AcadiaLogic.Dutchie.Integration/
│   │   ├── Abstractions/               IErpConnector, IErpConfigProvider, ISyncStateStore
│   │   ├── Models/                     ERP-neutral payload models + ErpMappingConfig
│   │   ├── Pipeline/                   ClosingReportSyncPipeline, TransactionSyncPipeline
│   │   └── State/                      JsonFileSyncStateStore
│   ├── AcadiaLogic.Dutchie.Intacct/
│   │   ├── Configuration/              IntacctOptions, AppSettings/PlatformApp config providers
│   │   │                               DutchieMasterConfigRow, DutchieLocationConfigRow
│   │   └── Connectors/                 IntacctErpConnector
│   └── AcadiaLogic.Dutchie.Worker/
│       ├── Workers/                    ClosingReportWorker, TransactionSyncWorker
│       ├── nlog.config                 NLog targets and rules
│       ├── appsettings.json
│       └── Program.cs
└── .env.example                        Credential template
```

---

## License

[MIT](LICENSE) — Copyright (c) 2026 AcadiaLogic
