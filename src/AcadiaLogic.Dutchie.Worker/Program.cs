using AcadiaLogic.Dutchie;
using AcadiaLogic.Dutchie.Intacct;
using AcadiaLogic.Dutchie.Integration;
using AcadiaLogic.Dutchie.Worker;
using AcadiaLogic.Dutchie.Worker.Workers;
using DotNetEnv;
using NLog.Extensions.Logging;

// ── Load .env file ────────────────────────────────────────────────────────────
// Searches from the working directory upwards for a ".env" file.
// Existing process/OS environment variables always take precedence (NoClobber),
// so secrets injected by the host or orchestrator are never overridden.
// The file is optional — omitting it is fine in production when real env vars
// are set directly by the deployment platform.
Env.Load(".env", new LoadOptions(Env.TraversePath(), clobberExistingVars: false));

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddNLog();
var config = builder.Configuration;

// ── Dutchie POS API client ────────────────────────────────────────────────────
builder.Services.AddDutchieClient(config);

// ── Sage Intacct connector ────────────────────────────────────────────────────
// Credentials come from the "Intacct" config section, which is populated by
// environment variables at runtime (e.g. INTACCT__COMPANYID, INTACCT__USERID …).
// All GL account mappings are loaded directly from the Dutchie Integration
// Platform Application objects in Intacct (dutchie_master_config + dutchie_config).
builder.Services.AddIntacctConnector(config).UsePlatformAppConfig();

// ── Sync pipeline + state store ───────────────────────────────────────────────
builder.Services.AddDutchieIntegration(state =>
{
    state.FilePath = config["Worker:SyncStateFile"] ?? "sync-state.json";
});

// ── Worker schedule options ───────────────────────────────────────────────────
builder.Services.Configure<WorkerOptions>(config.GetSection(WorkerOptions.SectionName));

// ── Background workers ────────────────────────────────────────────────────────
builder.Services.AddHostedService<ClosingReportWorker>();
builder.Services.AddHostedService<TransactionSyncWorker>();

await builder.Build().RunAsync();
