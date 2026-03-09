# Deployment Guide — AcadiaLogic.Dutchie.Library

This document covers everything needed to deploy the Dutchie → Sage Intacct integration worker to a new environment.

---

## Prerequisites

| Requirement | Minimum Version | Notes |
|---|---|---|
| .NET SDK | 8.0 | Required to build and publish |
| .NET Runtime | 8.0 | Required to run the published output |
| Dutchie POS | — | API access with LocationKey + IntegratorKey |
| Sage Intacct | — | Web Services user with Platform App access |
| OS | Windows / Linux / macOS | Cross-platform; all three are supported |

---

## Step 1 — Deploy the Intacct Platform Application

The integration reads all GL mapping configuration from three custom objects in Intacct. These objects must exist before the worker is started.

**Deploy once per Intacct company.** The XML file is idempotent; re-deploying over an existing deployment is safe.

1. Log in to Sage Intacct as a Company Admin.
2. Navigate to **Platform Services → Applications**.
3. Click **Import / Update Application**.
4. Upload `platform-app/DutchieIntegration.xml` from this repository.
5. Confirm the import. The following objects will be created:
   - `dutchie_master_config` — company-level GL journal and tolerance settings
   - `dutchie_location_config` — per-location Dutchie credentials and default dimensions
   - `dutchie_field_config` — per-field GL mapping rules
   - `dutchie_process_log` — sync run audit log

### Configure the Platform App

After deploying the XML, enter your configuration data in Intacct:

1. **Create one `dutchie_master_config` record:**
   - Set the **GL Journal** (the journal all closing-report entries will be posted to).
   - Set **Maximum Over/Short** (cash discrepancy tolerance in dollars; default `1.00`).
   - Set **Live (Post, Not Draft)** to checked when ready for production posting.

2. **Create one `dutchie_location_config` record per syncing location:**
   - Link it to the master config created above.
   - Select the Intacct **Location** this config applies to.
   - Enter the **Dutchie Location Key** and **Dutchie Integrator Key** for that location.
   - Set default **Customer**, **Department**, and **Item** for dimension-less transactions.
   - Optionally set **Entity ID Override** for multi-entity SDK sessions.

3. **Create `dutchie_field_config` rows for each GL mapping rule:**
   - Link each row to the location config.
   - Set **Input Type** (`header`, `paymentSummary`, `taxRateSummary`, `categorySummary`).
   - Set **API Field** to the Dutchie field name (e.g., `cannabisSales`, `Cash`, `Excise Tax`).
   - Set **Credit / Debit** and the target **GL Account**.
   - Optionally override Customer, Department, Item, Class, Amount, or Entry Type per line.

---

## Step 2 — Build and Publish

### Build (verify)

```sh
dotnet build AcadiaLogic.Dutchie.Library.slnx
```

### Publish (self-contained, single-file)

```sh
dotnet publish src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj \
  --configuration Release \
  --output ./publish
```

For a self-contained deployment (no .NET runtime required on target):

```sh
dotnet publish src/AcadiaLogic.Dutchie.Worker/AcadiaLogic.Dutchie.Worker.csproj \
  --configuration Release \
  --self-contained true \
  --runtime linux-x64 \
  --output ./publish
```

Replace `linux-x64` with `win-x64` or `osx-arm64` as appropriate for your target.

---

## Step 3 — Configure Credentials

Credentials are supplied via environment variables. Never store them in committed config files.

### Option A — `.env` file (simple / local)

Copy the template and fill in values:

```sh
cp .env.example .env
```

Edit `.env`:

```dotenv
# Dutchie POS (may be overridden per-location from Intacct Platform App)
DUTCHIE__LOCATIONKEY=your-location-key
DUTCHIE__INTEGRATORKEY=your-integrator-key

# Sage Intacct Web Services
INTACCT__COMPANYID=your-company-id
INTACCT__USERID=your-web-services-user
INTACCT__USERPASSWORD=your-user-password
INTACCT__SENDERID=your-sender-id
INTACCT__SENDERPASSWORD=your-sender-password
INTACCT__ENTITYID=                         # optional: multi-entity entity context
INTACCT__LOCATIONID=your-intacct-location  # scopes Platform App queries to this location
```

The `.env` file is loaded at startup. Real environment variables always win over `.env` values (`clobberExistingVars: false`).

### Option B — System / Container environment variables

Set the same keys directly in your host OS, container environment, or secrets manager. No `.env` file is required.

### Option C — Secrets manager (production recommended)

Inject secrets as environment variables from your preferred secrets manager (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault, etc.) at container or service startup.

---

## Step 4 — Configure Worker Settings

Non-credential settings live in `appsettings.json` (which is committed). Override any value via environment variable using double-underscore notation:

| Setting | Env Var Override | Default | Description |
|---|---|---|---|
| `Worker:ClosingReportInterval` | `WORKER__CLOSINGREPORTINTERVAL` | `24:00:00` | How often the closing report job runs |
| `Worker:ClosingReportLookback` | `WORKER__CLOSINGREPORTLOOKBACK` | `24:00:00` | How far back each closing report pull covers |
| `Worker:TransactionSyncInterval` | `WORKER__TRANSACTIONSYNCINTERVAL` | `00:15:00` | How often the transaction sync job runs |
| `Worker:SyncStateFile` | `WORKER__SYNCSTATEFILE` | `sync-state.json` | Path to the incremental sync watermark file |

---

## Step 5 — Run the Worker

### Direct (development / simple server)

```sh
cd publish
./AcadiaLogic.Dutchie.Worker    # Linux / macOS
AcadiaLogic.Dutchie.Worker.exe  # Windows
```

### systemd (Linux server)

Create `/etc/systemd/system/dutchie-worker.service`:

```ini
[Unit]
Description=AcadiaLogic Dutchie Integration Worker
After=network.target

[Service]
Type=simple
User=dutchie
WorkingDirectory=/opt/dutchie-worker
ExecStart=/opt/dutchie-worker/AcadiaLogic.Dutchie.Worker
Restart=always
RestartSec=10
EnvironmentFile=/opt/dutchie-worker/.env

[Install]
WantedBy=multi-user.target
```

```sh
sudo systemctl daemon-reload
sudo systemctl enable dutchie-worker
sudo systemctl start dutchie-worker
sudo journalctl -u dutchie-worker -f
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["./AcadiaLogic.Dutchie.Worker"]
```

```sh
docker build -t dutchie-worker .
docker run -d \
  --name dutchie-worker \
  --env-file .env \
  -v $(pwd)/logs:/app/logs \
  -v $(pwd)/sync-state.json:/app/sync-state.json \
  dutchie-worker
```

> **Note:** Mount `logs/` and `sync-state.json` as volumes to persist log files and the sync watermark across container restarts.

---

## Step 6 — Verify Deployment

1. **Console / journal output** — watch for `Info` log lines indicating both workers started:
   ```
   ClosingReportWorker started. Interval: 24:00:00
   TransactionSyncWorker started. Interval: 00:15:00
   ```

2. **Log files** — check `logs/dutchie-worker.log` (all levels) and `logs/dutchie-errors.log` (errors only).

3. **Intacct process log** — after the first run, check the `dutchie_process_log` list in Intacct for a record showing `Status = complete` and a non-zero `Records Processed` count. *(Note: process log writing is a pending implementation.)*

4. **Intacct GL journal** — verify a journal entry appears in the configured journal after the first closing report run.

---

## Updating

### Update the worker binary

1. Publish the new version.
2. Stop the service.
3. Replace the binary in the deployment directory.
4. Start the service.

The `sync-state.json` watermark file is preserved across restarts and updates.

### Update the Platform Application

If a new version of `DutchieIntegration.xml` is released:

1. Re-import via **Platform Services → Applications → Import / Update Application**.
2. Existing configuration data (master, location, field config records) is preserved.

---

## Rollback

1. Stop the service.
2. Restore the previous binary from backup.
3. Start the service.

The `sync-state.json` watermark will be unchanged; the worker will resume from the last successful sync point.

---

## Troubleshooting

| Symptom | Likely Cause | Resolution |
|---|---|---|
| Worker exits immediately | Missing required env vars | Check logs for configuration exceptions |
| `401 Unauthorized` from Dutchie | Incorrect LocationKey / IntegratorKey | Verify credentials in `.env` or environment |
| `XmlException` from Intacct SDK | Incorrect Intacct credentials | Verify CompanyId, UserId, UserPassword, SenderId, SenderPassword |
| No journal entry posted | `is_live = false` on master config | Check the **Live** flag in `dutchie_master_config` in Intacct |
| GL mapping rows not loaded | Wrong `INTACCT__LOCATIONID` | Ensure the LocationId matches the Intacct Location linked to your `dutchie_location_config` record |
| Repeated 500 errors from Dutchie | Dutchie transient failures | Worker retries up to 5× automatically; check `logs/dutchie-errors.log` if retries exhausted |
| `sync-state.json` not found | First run or file deleted | Normal — file is created on first successful sync |
