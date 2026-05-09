import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type {
  CreateExternalWorkSourceInput,
  CreateIntegrationConnectionInput,
  CreateTicketFromExternalItemInput,
  CortexField,
  ExternalBoardMappingItemInput,
  ExternalBoardMappingMode,
  ExternalFieldMappingItemInput,
  ExternalSourceReadinessResponse,
  ExternalSourceSyncResponse,
  ExternalSourceType,
  ExternalWorkItemResponse,
  ExternalWorkSourceResponse,
  IntegrationActivityLogEntry,
  IntegrationActivityStatus,
  IntegrationActivityType,
  IntegrationAuthMode,
  IntegrationConnectionHealthStatus,
  IntegrationConnectionResponse,
  IntegrationConnectionTestMode,
  IntegrationCredentialStatusDto,
  IntegrationProviderDefinitionDto,
  IntegrationProvider,
  IntegrationReadinessCheckStatus,
  IntegrationSourceFieldsOverviewResponse,
  IntegrationSyncMode,
  ManualUpsertExternalWorkItemInput,
  SharePointDiscoveredFieldResponse,
  UpdateExternalWorkSourceInput,
  UpdateIntegrationConnectionInput,
} from "../types/integrations";
import {
  AUTH_MODES,
  BOARD_MAPPING_MODES,
  CORTEX_FIELDS,
  EXTERNAL_TICKET_PRIORITIES,
  INTEGRATION_PROVIDERS,
  SOURCE_TYPES,
  SYNC_MODES,
} from "../types/integrations";
import { getUserFacingErrorMessage } from "../services/api";
import { integrationsService } from "../services/integrationsService";
import toast from "react-hot-toast";
import {
  ConfigDetailCard,
  ConfigGhostButton,
  ConfigPageBody,
  ConfigPageHeader,
  ConfigPageShell,
  ConfigPrimaryButton,
  ConfigSecondaryButton,
  configFieldClass,
} from "./configurationAdminUi";

const API_AUDIENCE = "https://cortex-api";

/**
 * Manual provider test checklist (QA / smoke):
 * - SharePoint: add connection → provider settings → optional credentials → test → add list source → discover fields →
 *   field mapping → sync / external items → activity.
 * - Jira: add connection → credentials → test (local/metadata-only) → confirm field planning guidance → confirm no live sync/discovery claim.
 * - ServiceNow: same as Jira with ServiceNow-specific settings.
 * - SAP Reference: confirm metadata-only messaging → Configuration → SAP Reference (catalog) → no live SAP / no work-item sync expectation.
 */
type IntegrationsTab = "connections" | "sources" | "fields" | "boards" | "items" | "activity";

const INTEGRATION_TAB_GUIDANCE: Record<IntegrationsTab, string> = {
  connections: "Create and manage provider-specific connection setup.",
  sources: "Discover and manage external work sources available from a connection.",
  fields: "Map provider fields into Cortex concepts before external work becomes Cortex context.",
  boards: "Control which Cortex board external work should enter.",
  items: "Review imported external records before creating Cortex tickets.",
  activity: "Review sync, credential, health, and ticket-creation activity.",
};

function Callout({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-lg border border-sky-200 bg-sky-50/90 px-4 py-3 text-sm text-sky-950 dark:border-sky-800 dark:bg-sky-950/40 dark:text-sky-100">
      <p className="font-medium text-sky-900 dark:text-sky-100">{title}</p>
      <div className="mt-1.5 text-sky-800 dark:text-sky-200/90">{children}</div>
    </div>
  );
}

function MappingChip({
  children,
  tone = "neutral",
}: {
  children: React.ReactNode;
  tone?: "neutral" | "sky" | "green" | "amber";
}) {
  const cls =
    tone === "sky"
      ? "bg-sky-100/90 text-sky-950 dark:bg-sky-900/50 dark:text-sky-100"
      : tone === "green"
        ? "bg-emerald-100/90 text-emerald-950 dark:bg-emerald-900/40 dark:text-emerald-100"
        : tone === "amber"
          ? "bg-amber-100/90 text-amber-950 dark:bg-amber-900/40 dark:text-amber-100"
          : "bg-gray-100 text-gray-800 dark:bg-slate-800 dark:text-slate-200";
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium leading-tight ${cls}`}
    >
      {children}
    </span>
  );
}

function ReadOnlySecurityCallout() {
  return (
    <div className="rounded-lg border border-amber-200/90 bg-amber-50/90 px-4 py-3 text-sm text-amber-950 dark:border-amber-800/70 dark:bg-amber-950/35 dark:text-amber-100">
      <p className="font-medium text-amber-900 dark:text-amber-100">Security and read-only posture</p>
      <p className="mt-1.5 leading-relaxed text-amber-900/95 dark:text-amber-100/90">
        Secrets are submitted through a dedicated credential flow and are never displayed after saving. External
        integrations are read-only by default. Imported context does not change routing, owners, or approvals unless
        approved Cortex rules apply. Jira and ServiceNow live sync is not enabled yet.
      </p>
    </div>
  );
}

function IntegrationSetupFlowGuide() {
  const steps: { title: string; description: string }[] = [
    {
      title: "Create connection",
      description: "Choose a provider and enter provider-specific setup details.",
    },
    {
      title: "Configure credentials",
      description: "Add secrets through the dedicated credential flow. Existing secrets are never displayed.",
    },
    {
      title: "Test connection",
      description: "Check configuration and available read-only access.",
    },
    {
      title: "Discover sources",
      description: "Find lists, projects, tables, or work sources supported by the provider.",
    },
    {
      title: "Map fields and boards",
      description: "Control how external records become Cortex context.",
    },
    {
      title: "Review external items",
      description: "Inspect imported records before creating Cortex tickets.",
    },
  ];
  return (
    <div className="rounded-lg border border-gray-200 bg-white px-4 py-4 dark:border-slate-700 dark:bg-slate-900/40">
      <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Integration setup flow</h3>
      <p className="mt-1 text-xs text-gray-600 dark:text-slate-400">
        Move through these steps in order; use the tabs for the next stage when you are ready.
      </p>
      <div className="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
        {steps.map((s, idx) => (
          <div
            key={s.title}
            className="flex gap-2.5 rounded-lg border border-gray-200/90 bg-gray-50/80 px-3 py-2.5 dark:border-slate-600 dark:bg-slate-800/50"
          >
            <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-cortex-blue/15 text-xs font-bold text-cortex-blue dark:bg-cortex-blue/25 dark:text-cortex-cyan">
              {idx + 1}
            </span>
            <div className="min-w-0">
              <p className="text-xs font-semibold text-gray-900 dark:text-slate-100">{s.title}</p>
              <p className="mt-0.5 text-xs leading-snug text-gray-600 dark:text-slate-400">{s.description}</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function formatWhen(iso?: string | null): string {
  if (!iso) {
    return "—";
  }
  try {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    });
  } catch {
    return iso;
  }
}

function toDatetimeLocalInput(iso?: string | null): string {
  if (!iso?.trim()) {
    return "";
  }
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    return "";
  }
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function formatDurationMs(ms: number | null | undefined): string {
  if (ms == null || ms < 0 || Number.isNaN(ms)) {
    return "—";
  }
  if (ms < 1000) {
    return `${ms} ms`;
  }
  const s = ms / 1000;
  if (s < 60) {
    return `${s.toFixed(1)} s`;
  }
  const m = Math.floor(s / 60);
  const rem = s - m * 60;
  return `${m}m ${rem.toFixed(0)}s`;
}

function connectionHealthBadgeClasses(status: IntegrationConnectionHealthStatus): string {
  switch (status) {
    case "Healthy":
      return "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/50 dark:text-emerald-100";
    case "NeedsAttention":
      return "bg-red-100 text-red-900 dark:bg-red-950/50 dark:text-red-100";
    case "MissingCredentials":
      return "bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-100";
    case "NotConfigured":
      return "bg-slate-200 text-slate-800 dark:bg-slate-700 dark:text-slate-100";
    case "NotTested":
      return "bg-sky-100 text-sky-900 dark:bg-sky-950/40 dark:text-sky-100";
    case "TestUnavailable":
      return "bg-violet-100 text-violet-900 dark:bg-violet-950/40 dark:text-violet-100";
    default:
      return "bg-gray-100 text-gray-800 dark:bg-slate-700 dark:text-slate-200";
  }
}

/** Shared pill layout: avoids awkward wraps in table cells; pair with connectionHealthBadgeClasses. */
function connectionHealthBadgeLayout(variant: "table" | "card"): string {
  const sizing =
    variant === "table"
      ? "px-3 py-1 text-xs font-medium leading-none"
      : "px-3 py-1.5 text-sm font-medium leading-none";
  return `inline-flex max-w-max shrink-0 items-center justify-center whitespace-nowrap rounded-full ${sizing}`;
}

/** Provider capability / maturity (safe copy for admins). */
function integrationProviderMaturityMessage(provider: IntegrationProvider): string {
  switch (provider) {
    case "SharePoint":
      return "SharePoint supports read-only external work intake using the configured Graph/app registration path.";
    case "Jira":
      return "Jira setup and credential storage are available. Live Jira validation and sync are not enabled yet.";
    case "ServiceNow":
      return "ServiceNow setup and credential storage are available. Live ServiceNow validation and sync are not enabled yet.";
    case "SapReference":
      return "SAP Reference uses stored metadata only. This is not a live SAP connection.";
    default:
      return "";
  }
}

function integrationProviderReadinessPill(provider: IntegrationProvider): { label: string; className: string } {
  switch (provider) {
    case "SharePoint":
      return {
        label: "Supported read-only path",
        className:
          "bg-slate-200/90 text-slate-900 dark:bg-slate-600 dark:text-slate-100",
      };
    case "Jira":
    case "ServiceNow":
      return {
        label: "Live validation not enabled",
        className:
          "bg-violet-100 text-violet-900 dark:bg-violet-950/50 dark:text-violet-100",
      };
    case "SapReference":
      return {
        label: "Metadata only",
        className:
          "bg-amber-100 text-amber-950 dark:bg-amber-950/40 dark:text-amber-100",
      };
    default:
      return {
        label: "Setup",
        className: "bg-gray-100 text-gray-800 dark:bg-slate-700 dark:text-slate-200",
      };
  }
}

type ProviderReadinessMatrixRow = {
  providerLabel: string;
  setupFields: string;
  credentials: string;
  healthTest: string;
  fieldDiscovery: string;
  sync: string;
  currentStatus: string;
};

const PROVIDER_READINESS_MATRIX_ROWS: ProviderReadinessMatrixRow[] = [
  {
    providerLabel: "SharePoint",
    setupFields: "Available",
    credentials: "App / credential path supported",
    healthTest: "Available (local fallback when Graph app incomplete)",
    fieldDiscovery: "Supported (list columns)",
    sync: "Supported read-only",
    currentStatus: "Supported read-only path",
  },
  {
    providerLabel: "Jira",
    setupFields: "Available",
    credentials: "Available",
    healthTest: "Local validation only",
    fieldDiscovery: "Planned (guidance only)",
    sync: "Not enabled",
    currentStatus: "Setup-ready",
  },
  {
    providerLabel: "ServiceNow",
    setupFields: "Available",
    credentials: "Available",
    healthTest: "Local validation only",
    fieldDiscovery: "Planned (guidance only)",
    sync: "Not enabled",
    currentStatus: "Setup-ready",
  },
  {
    providerLabel: "SAP Reference",
    setupFields: "Metadata source only",
    credentials: "Not required",
    healthTest: "Metadata check only",
    fieldDiscovery: "Catalog-driven",
    sync: "Not applicable",
    currentStatus: "Metadata-only",
  },
];

function ProviderReadinessMatrixSection() {
  return (
    <ConfigDetailCard
      title="Provider readiness"
      subtitle="Capability summary by provider for this release. Use it to set expectations before wiring live systems."
    >
      <div className="max-w-full overflow-x-auto rounded-lg border border-gray-100 dark:border-slate-800">
        <table className="min-w-[880px] w-full divide-y divide-gray-200 text-xs sm:text-sm dark:divide-slate-700">
          <thead className="bg-gray-50 dark:bg-slate-800/80">
            <tr>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Provider</th>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">
                Setup fields
              </th>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">
                Credentials
              </th>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">
                Health test
              </th>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">
                Field discovery
              </th>
              <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Sync</th>
              <th className="min-w-[140px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">
                Current status
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
            {PROVIDER_READINESS_MATRIX_ROWS.map((row) => (
              <tr key={row.providerLabel} className="bg-white dark:bg-slate-900">
                <td className="whitespace-nowrap px-3 py-2 font-medium text-gray-900 dark:text-slate-100">
                  {row.providerLabel}
                </td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.setupFields}</td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.credentials}</td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.healthTest}</td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.fieldDiscovery}</td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.sync}</td>
                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{row.currentStatus}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </ConfigDetailCard>
  );
}

function sourcesTabIntro(provider: IntegrationProvider | null): string {
  if (!provider) {
    return "Register lists, projects, or tables after you select a connection. Capabilities depend on the provider profile.";
  }
  switch (provider) {
    case "SharePoint":
      return "Register SharePoint lists for this connection. Read-only discovery and sync use the configured Microsoft Graph path from other tabs.";
    case "Jira":
      return "Register Jira project sources manually for planning. Live source discovery is not enabled yet; connection setup and credentials are still valuable for future validation.";
    case "ServiceNow":
      return "Register ServiceNow table sources manually for planning. Live source discovery is not enabled yet; connection setup and credentials remain available.";
    case "SapReference":
      return "SAP Reference metadata is managed through Configuration → SAP Reference (catalog). This tab does not represent a live SAP work feed.";
    default:
      return "Register external work sources supported by this provider.";
  }
}

function sourcesTabPlanningNote(provider: IntegrationProvider): string {
  switch (provider) {
    case "SharePoint":
      return "Add a SharePoint list source, then use read-only discovery and sync from the Field mapping and External items tabs.";
    case "Jira":
      return "Jira live source discovery is not enabled yet. Connection setup and credential storage are available for planning—you can still register a project source manually when your process requires it.";
    case "ServiceNow":
      return "ServiceNow live source discovery is not enabled yet. Connection setup and credential storage are available for planning—you can still register a table source manually when your process requires it.";
    case "SapReference":
      return "SAP Reference metadata is managed through Configuration → SAP Reference (catalog). This is not a live SAP work source.";
    default:
      return "Add a source when your provider supports external work in Cortex.";
  }
}

function fieldMappingNoSourceHint(provider: IntegrationProvider): string {
  switch (provider) {
    case "Jira":
      return "Select a source to edit mappings. Live Jira field discovery is not enabled yet—planning guidance still applies.";
    case "ServiceNow":
      return "Select a source to edit mappings. Live ServiceNow field discovery is not enabled yet—planning guidance still applies.";
    case "SapReference":
      return "SAP Reference follows the catalog model—not standard external work-item field mapping.";
    default:
      return "Select a source to load mapping profiles, discovered SharePoint columns, and planning hints.";
  }
}

function boardsTabNoSourceHint(provider: IntegrationProvider | undefined): string {
  if (!provider) {
    return "Select an external source above.";
  }
  switch (provider) {
    case "Jira":
    case "ServiceNow":
      return "Select a source to align boards. Live intake from these providers is not enabled yet—mappings are preparatory.";
    case "SapReference":
      return "SAP Reference metadata does not drive standard board intake from external work items.";
    default:
      return "Select an external source above.";
  }
}

function externalItemsTabCalloutBody(
  provider: IntegrationProvider | null,
): React.ReactNode {
  if (provider === "SharePoint") {
    return (
      <>
        Review imported SharePoint list rows here. Read-only sync refreshes copies; creating Cortex tickets stays an explicit
        action and does not change routing or approvals by itself.
      </>
    );
  }
  if (provider === "Jira" || provider === "ServiceNow") {
    return (
      <>
        Automated read-only sync is not enabled for this provider yet. Use <span className="font-medium">Manual upsert</span>{" "}
        for test records. Creating Cortex tickets from items remains explicit and governed.
      </>
    );
  }
  if (provider === "SapReference") {
    return (
      <>
        SAP Reference is metadata/catalog-only. External work items and SharePoint-style sync do not apply. Manage tables and
        fields under <span className="font-medium">Configuration → SAP Reference</span>.
      </>
    );
  }
  return (
    <>
      Review imported records here after you select a connection and source. Provider-specific intake depends on the selected
      source; tickets are never created automatically from this screen.
    </>
  );
}

function externalItemsSyncStripBody(provider: IntegrationProvider | null): string {
  if (provider === "SharePoint") {
    return "Reads the selected SharePoint list and updates external work items. Cortex tickets are not created automatically.";
  }
  if (provider === "Jira" || provider === "ServiceNow") {
    return "Automated sync is not enabled for this provider yet. Use manual upsert to add test rows while planning continues.";
  }
  if (provider === "SapReference") {
    return "SAP Reference does not perform list or ticket sync from Integrations. Use the SAP Reference Catalog for metadata.";
  }
  return "Select a SharePoint list source to run read-only sync when mappings and readiness allow.";
}

function externalItemsEmptySecondary(provider: IntegrationProvider | null): string {
  if (provider === "SharePoint") {
    return "Run read-only sync after field mappings are saved, or use manual upsert to insert a test item.";
  }
  if (provider === "Jira" || provider === "ServiceNow") {
    return "Live provider sync is not enabled. Use manual upsert for sample rows while setup continues.";
  }
  if (provider === "SapReference") {
    return "SAP Reference does not populate this queue. Use Configuration → SAP Reference for catalog metadata.";
  }
  return "Select a source that supports external items in Cortex.";
}

function activityEmptySecondary(provider: IntegrationProvider | null): string {
  if (provider === "SharePoint") {
    return "After you run discovery, sync, credential changes, or tests, entries appear here with full audit context.";
  }
  if (provider === "Jira" || provider === "ServiceNow") {
    return "Credential and test events still appear here. Live sync and discovery are not enabled yet for this provider.";
  }
  if (provider === "SapReference") {
    return "Expect metadata-safe events only (for example, connection tests). There is no live SAP or work-item sync trail.";
  }
  return "Run discovery, sync, manual upserts, or credential changes to build a trail.";
}

function connectionHealthSupplementaryNote(
  provider: IntegrationProvider,
  testMode: IntegrationConnectionTestMode,
  status: IntegrationConnectionHealthStatus,
): string | null {
  if (provider === "SharePoint" && testMode === "LocalValidation" && status === "TestUnavailable") {
    return "SharePoint settings were checked locally. Complete Microsoft Graph application credentials for live validation, or continue when your host-managed Graph app already covers this connection.";
  }
  if (provider === "Jira" && testMode === "NotAvailable") {
    return "This test records local configuration checks only. Live Jira API validation is not enabled yet.";
  }
  if (provider === "ServiceNow" && testMode === "NotAvailable") {
    return "This test records local configuration checks only. Live ServiceNow API validation is not enabled yet.";
  }
  if (provider === "SapReference" && testMode === "LocalValidation") {
    return "SAP Reference is metadata-only. No live SAP system is contacted from this test.";
  }
  if (testMode === "LocalValidation" && status === "TestUnavailable") {
    return "Configuration checked locally. Live provider validation is limited for this setup.";
  }
  return null;
}

function computeIntegrationNextAction(connection: IntegrationConnectionResponse | null): string {
  if (!connection?.health) {
    return "Select a connection to see recommended next steps.";
  }
  const { health: h, provider } = connection;

  if (provider === "SapReference") {
    return "Manage SAP metadata under Configuration → SAP Reference (catalog). This connection is metadata-only—not a live SAP work feed.";
  }

  switch (h.status) {
    case "NotConfigured":
      if (provider === "SharePoint") {
        return "Complete required SharePoint settings (tenant, site URL or ID, and permission context).";
      }
      if (provider === "Jira") {
        return "Complete required Jira settings (base URL, project key, and issue type).";
      }
      if (provider === "ServiceNow") {
        return "Complete required ServiceNow settings (instance URL and table).";
      }
      return "Complete required provider settings.";
    case "MissingCredentials":
      if (provider === "SharePoint") {
        return "Configure credentials or Graph app settings before testing or syncing.";
      }
      if (provider === "Jira") {
        return "Configure Jira credentials before future validation or sync.";
      }
      if (provider === "ServiceNow") {
        return "Configure ServiceNow credentials before future validation or sync.";
      }
      return "Configure credentials before testing or syncing.";
    case "NotTested":
      if (provider === "SharePoint") {
        return "Run Test connection to validate settings and Microsoft Graph access when the app registration path is complete.";
      }
      if (provider === "Jira" || provider === "ServiceNow") {
        return "Run Test connection to record local configuration checks (live provider APIs are not invoked yet).";
      }
      return "Run Test connection.";
    case "TestUnavailable":
      if (provider === "SharePoint" && h.lastTestedAtUtc) {
        return "SharePoint completed a local check. Align Graph app registration if you need live validation, or proceed with mappings and read-only sync when ready.";
      }
      if (provider === "SharePoint") {
        return "Run Test connection for local checks; finish Graph application registration to enable live validation.";
      }
      if (provider === "Jira") {
        return "Connection setup is ready. Live Jira validation is not enabled yet.";
      }
      if (provider === "ServiceNow") {
        return "Connection setup is ready. Live ServiceNow validation is not enabled yet.";
      }
      if (h.lastTestedAtUtc) {
        return "Connection setup is ready for future provider validation.";
      }
      return "Run Test connection.";
    case "Healthy":
      if (provider === "SharePoint") {
        return "Connection is ready for supported read-only discovery and sync.";
      }
      if (provider === "Jira" || provider === "ServiceNow") {
        return "Connection is healthy for current checks. Live provider sync is not enabled yet.";
      }
      return "Connection is ready for supported read-only operations.";
    case "NeedsAttention":
      if (provider === "SharePoint") {
        return "Review SharePoint Graph configuration, tenant, site, list, or permissions.";
      }
      return "Review provider settings and credentials, then run Test connection again.";
    default:
      return "Review connection status and activity.";
  }
}

function humanizeIntegrationActivityType(t: IntegrationActivityType): string {
  switch (t) {
    case "DiscoverFields":
      return "Discovery";
    case "SyncSource":
      return "Sync history";
    case "ManualUpsert":
      return "Manual upsert";
    case "CredentialConfigured":
      return "Credential configured";
    case "CredentialRotated":
      return "Credential rotated";
    case "CredentialCleared":
      return "Credential cleared";
    case "ConnectionTested":
      return "Connection tested";
    default:
      return t;
  }
}

function humanizeIntegrationActivityStatus(s: IntegrationActivityStatus): string {
  switch (s) {
    case "Success":
      return "Success";
    case "Failed":
      return "Failed";
    case "Partial":
      return "Partial";
    default:
      return s;
  }
}

function activityStatusRowClass(s: IntegrationActivityStatus): string {
  switch (s) {
    case "Success":
      return "text-green-800 dark:text-green-200/90";
    case "Failed":
      return "text-red-800 dark:text-red-200/90";
    case "Partial":
      return "text-amber-900 dark:text-amber-100/90";
    default:
      return "text-gray-800 dark:text-slate-200";
  }
}

function integrationActivityResultSummary(row: IntegrationActivityLogEntry): string {
  if (row.activityType === "DiscoverFields") {
    return row.message?.trim() || "Discovery completed.";
  }
  if (row.activityType === "ManualUpsert") {
    return row.message?.trim() || "Manual upsert completed.";
  }
  if (
    row.activityType === "CredentialConfigured" ||
    row.activityType === "CredentialRotated" ||
    row.activityType === "CredentialCleared"
  ) {
    return row.message?.trim() || humanizeIntegrationActivityType(row.activityType);
  }
  if (row.activityType === "ConnectionTested") {
    return row.message?.trim() || "Connection test completed.";
  }
  const c = row.createdCount ?? 0;
  const u = row.updatedCount ?? 0;
  const un = row.unchangedCount ?? 0;
  const sk = row.skippedCount ?? 0;
  const er = row.errorCount ?? 0;
  return `Created ${c} · Updated ${u} · Unchanged ${un} · Skipped ${sk} · Errors ${er}`;
}

function normalizeExternalPriority(p?: string | null): string {
  if (!p?.trim()) {
    return "Medium";
  }
  const hit = EXTERNAL_TICKET_PRIORITIES.find(
    (x) => x.toLowerCase() === p.trim().toLowerCase(),
  );
  return hit ?? "Medium";
}

/** Readable label for a linked Cortex ticket id in integrations UI. */
function formatLinkedTicketDisplay(ticketId: string): string {
  const id = ticketId.trim();
  return id ? `Ticket #${id}` : "—";
}

/** Display labels only; API values stay as enum strings. */
function humanizeExternalSourceType(sourceType: ExternalSourceType): string {
  switch (sourceType) {
    case "SharePointList":
      return "SharePoint List";
    case "JiraProject":
      return "Jira Project";
    case "ServiceNowTable":
      return "ServiceNow Table";
    default:
      return sourceType;
  }
}

function humanizeIntegrationSyncMode(syncMode: IntegrationSyncMode): string {
  switch (syncMode) {
    case "ReadOnly":
      return "Read only";
    case "ImportToCortex":
      return "Import to Cortex";
    case "TwoWay":
      return "Two-way";
    case "Manual":
      return "Manual (operator-triggered)";
    default:
      return syncMode;
  }
}

function humanizeIntegrationAuthMode(authMode: IntegrationAuthMode): string {
  switch (authMode) {
    case "Manual":
      return "Manual";
    case "OAuth":
      return "OAuth";
    case "AppRegistration":
      return "App registration";
    case "ApiToken":
      return "API token";
    case "OAuthClientCredentials":
      return "OAuth (client credentials)";
    case "ReferenceMetadata":
      return "Reference metadata";
    default:
      return authMode;
  }
}

function humanizeExternalBoardMappingMode(mode: ExternalBoardMappingMode): string {
  switch (mode) {
    case "ReferenceOnly":
      return "Reference only";
    case "Import":
      return "Import";
    case "Mirror":
      return "Mirror";
    default:
      return mode;
  }
}

type ConnectionModalCreateDraft = CreateIntegrationConnectionInput & { providerSettings: Record<string, string> };
type ConnectionModalEditDraft = UpdateIntegrationConnectionInput & {
  provider: IntegrationProvider;
  providerSettings: Record<string, string>;
};

function buildProviderSettingsPayload(
  settings: Record<string, string>,
  excludedKeys: ReadonlySet<string> = new Set(),
): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(settings)) {
    if (excludedKeys.has(k)) {
      continue;
    }
    const t = v.trim();
    if (t) {
      out[k] = t;
    }
  }
  return out;
}

function connectionProviderLabel(
  defs: IntegrationProviderDefinitionDto[] | null,
  provider: IntegrationProvider,
): string {
  return defs?.find((d) => d.provider === provider)?.displayName ?? provider;
}

const CORTEX_FIELD_ADMIN_LABELS: Partial<Record<CortexField, string>> = {
  Department: "Department / Domain",
  BusinessOwner: "External owner context",
  SynitiOwner: "Syniti owner context",
  Category: "Supporting context",
};

/** Readable Cortex field labels in dropdowns; values stay PascalCase enums. */
function humanizeCortexFieldDisplay(field: CortexField): string {
  return CORTEX_FIELD_ADMIN_LABELS[field] ?? field.replace(/([A-Z])/g, " $1").trim();
}

function providerFieldGuidanceBullets(provider: IntegrationProvider): string[] {
  switch (provider) {
    case "Jira":
      return [
        "Summary → Cortex Title",
        "Description → Cortex Description",
        "Priority → Cortex Priority",
        "Components / labels → department-style or supporting context (mapping-dependent)",
        "Epic link and related fields → supporting / related-work context only",
      ];
    case "ServiceNow":
      return [
        "Short description → Cortex Title",
        "Description → Cortex Description",
        "Impact and urgency → inform Cortex Priority (review together; no auto-combine yet)",
        "Assignment group → external owner context",
        "Category / subcategory → supporting or domain-style context",
      ];
    case "SharePoint":
      return [
        "Title → Cortex Title",
        "Description / details → Cortex Description",
        "Priority / severity → Cortex Priority",
        "Business area / department → Department / Domain",
        "SAP table / SAP field columns → reference context where applicable",
        "Reconciliation / readiness / cutover-style columns → Syniti knowledge context where applicable",
      ];
    case "SapReference":
      return [
        "SAP Reference metadata is managed through the SAP Reference Catalog, not standard work-item field mapping.",
      ];
    default:
      return ["Select a supported provider to see mapping guidance."];
  }
}

function fieldMappingNextAction(
  overview: IntegrationSourceFieldsOverviewResponse | null,
  discoveredFieldCount: number,
): string | null {
  if (!overview) {
    return null;
  }
  if (overview.discoveryMode === "LiveSharePointList") {
    if (discoveredFieldCount === 0) {
      return "Run Discover fields to load current SharePoint columns, then save mappings that use those internal names.";
    }
    return "Review discovered columns against your mapping rows, add mapping notes where needed, then save mapping.";
  }
  if (overview.discoveryMode === "PlanningStatic") {
    return "Live field discovery is not enabled for this provider yet. Use the planning rows as advisory guidance only.";
  }
  if (overview.discoveryMode === "NotApplicable" && overview.provider === "SapReference") {
    return "Use the SAP Reference Catalog for SAP metadata rather than this mapping profile.";
  }
  return null;
}

function findPlanningMappingKey(
  fieldKey: string,
  mappings: { externalFieldName: string; externalFieldKey?: string | null }[],
) {
  const k = fieldKey.trim().toLowerCase();
  return mappings.find(
    (m) =>
      m.externalFieldName.trim().toLowerCase() === k ||
      (m.externalFieldKey?.trim().toLowerCase() ?? "") === k,
  );
}

function readinessHeadline(r: ExternalSourceReadinessResponse): string {
  if (r.canSync) {
    return "Ready for SharePoint discovery and read-only sync.";
  }
  if (r.canDiscoverFields) {
    return "Some setup is complete, but Cortex needs more information before sync.";
  }
  return "Setup required before live discovery or sync can run.";
}

function readinessCheckRowClass(status: IntegrationReadinessCheckStatus): string {
  switch (status) {
    case "Passed":
      return "text-green-800 dark:text-green-200/90";
    case "Warning":
      return "text-amber-900 dark:text-amber-100/90";
    case "Failed":
      return "text-red-800 dark:text-red-200/90";
    default:
      return "text-gray-800 dark:text-slate-200";
  }
}

function primaryReadinessHint(
  readiness: ExternalSourceReadinessResponse | null,
  which: "discover" | "sync",
): string | null {
  if (!readiness) {
    return null;
  }
  if (which === "discover" && readiness.canDiscoverFields) {
    return null;
  }
  if (which === "sync" && readiness.canSync) {
    return null;
  }
  const failed = readiness.checks.filter((c) => c.status === "Failed");
  if (failed.length > 0) {
    return failed.map((c) => c.message).join(" ");
  }
  if (which === "sync" && readiness.canDiscoverFields && !readiness.canSync) {
    const fm = readiness.checks.find((c) => c.key === "fieldMappings");
    return fm?.message ?? "Save field mappings before syncing.";
  }
  const warn = readiness.checks.find((c) => c.status === "Warning");
  return warn?.message ?? null;
}

function mappingRowIdentity(name: string, key?: string | null): string {
  const k = key?.trim();
  const n = name.trim();
  return (k || n).toLowerCase();
}

function humanizeConnectionSyncStatus(status?: string | null): string {
  if (!status?.trim()) {
    return "—";
  }
  return status.trim();
}

function DetailField({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="border-b border-gray-100 py-3 last:border-b-0 dark:border-slate-800">
      <div className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">{label}</div>
      <div className="mt-1 text-sm text-gray-900 dark:text-slate-100">{children}</div>
    </div>
  );
}

const emptyFieldRow = (): ExternalFieldMappingItemInput => ({
  externalFieldName: "",
  externalFieldKey: "",
  cortexField: "Title",
  isRequired: false,
  transformHint: "",
});

const emptyBoardRow = (): ExternalBoardMappingItemInput => ({
  boardId: 0,
  mappingMode: "ReferenceOnly",
  isDefault: false,
});

export interface IntegrationsPageProps {
  ticketBoards: TicketBoardDefinition[];
  ticketBoardLoading: boolean;
  onRefreshTicketBoards: () => void;
  onOpenCortexTicketById?: (ticketId: string) => void | Promise<void>;
}

export default function IntegrationsPage({
  ticketBoards,
  ticketBoardLoading,
  onRefreshTicketBoards,
  onOpenCortexTicketById,
}: IntegrationsPageProps) {
  const { getAccessTokenSilently } = useAuth0();
  const getToken = useCallback(async () => {
    return getAccessTokenSilently({
      authorizationParams: { audience: API_AUDIENCE },
    });
  }, [getAccessTokenSilently]);

  const [providerDefinitions, setProviderDefinitions] = useState<IntegrationProviderDefinitionDto[] | null>(null);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const token = await getToken();
        const res = await integrationsService.listProviderDefinitions(token);
        if (!cancelled) {
          setProviderDefinitions(res.providers);
        }
      } catch {
        if (!cancelled) {
          setProviderDefinitions(null);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [getToken]);

  const [tab, setTab] = useState<IntegrationsTab>("connections");
  const [banner, setBanner] = useState<{ type: "ok" | "err"; text: string } | null>(null);

  const [connections, setConnections] = useState<IntegrationConnectionResponse[]>([]);
  const [connectionsLoading, setConnectionsLoading] = useState(true);
  const [connectionsError, setConnectionsError] = useState<string | null>(null);

  const [selectedConnectionId, setSelectedConnectionId] = useState<number | null>(null);
  const [sources, setSources] = useState<ExternalWorkSourceResponse[]>([]);
  const [sourcesLoading, setSourcesLoading] = useState(false);
  const [sourcesError, setSourcesError] = useState<string | null>(null);

  const [selectedSourceId, setSelectedSourceId] = useState<number | null>(null);

  const [fieldDraft, setFieldDraft] = useState<ExternalFieldMappingItemInput[]>([]);
  const [fieldLoading, setFieldLoading] = useState(false);
  const [fieldSaving, setFieldSaving] = useState(false);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [fieldsOverview, setFieldsOverview] = useState<IntegrationSourceFieldsOverviewResponse | null>(null);

  const [boardDraft, setBoardDraft] = useState<ExternalBoardMappingItemInput[]>([]);
  const [boardLoading, setBoardLoading] = useState(false);
  const [boardSaving, setBoardSaving] = useState(false);
  const [boardError, setBoardError] = useState<string | null>(null);

  const [items, setItems] = useState<ExternalWorkItemResponse[]>([]);
  const [itemsLoading, setItemsLoading] = useState(false);
  const [itemsError, setItemsError] = useState<string | null>(null);
  const [itemDetail, setItemDetail] = useState<ExternalWorkItemResponse | null>(null);
  /** Review queue: filter external work items in the Items tab (local only). */
  const [itemsLinkFilter, setItemsLinkFilter] = useState<
    "all" | "needsTicket" | "linked"
  >("all");
  const [itemsSearch, setItemsSearch] = useState("");
  const [itemsPriorityFilter, setItemsPriorityFilter] = useState("");
  const [itemsStatusFilter, setItemsStatusFilter] = useState("");

  const [createTicketOpen, setCreateTicketOpen] = useState(false);
  const [createTicketFor, setCreateTicketFor] = useState<ExternalWorkItemResponse | null>(null);
  const [createTicketDraft, setCreateTicketDraft] = useState({
    title: "",
    description: "",
    boardId: "" as number | "",
    priority: "Medium",
    dueDateUtc: "",
    department: "",
    category: "",
    requester: "",
    assignedTo: "",
  });
  const [createTicketSaving, setCreateTicketSaving] = useState(false);
  const [createTicketError, setCreateTicketError] = useState<string | null>(null);

  const [connectionModal, setConnectionModal] = useState<
    | { mode: "create"; draft: ConnectionModalCreateDraft }
    | { mode: "edit"; id: number; draft: ConnectionModalEditDraft }
    | null
  >(null);

  const [credentialStatusDetail, setCredentialStatusDetail] = useState<IntegrationCredentialStatusDto | null>(null);
  const [credentialDraft, setCredentialDraft] = useState<Record<string, string>>({});
  const [credentialStatusLoading, setCredentialStatusLoading] = useState(false);
  const [credentialStatusError, setCredentialStatusError] = useState<string | null>(null);
  const [credentialSaveLoading, setCredentialSaveLoading] = useState(false);
  const [credentialClearLoading, setCredentialClearLoading] = useState(false);
  const [connectionTestLoading, setConnectionTestLoading] = useState(false);
  const [sourceModal, setSourceModal] = useState<
    | { mode: "create"; draft: CreateExternalWorkSourceInput }
    | { mode: "edit"; id: number; draft: UpdateExternalWorkSourceInput & { provider: IntegrationProvider; sourceType: ExternalSourceType; externalSourceId: string } }
    | null
  >(null);

  const [upsertOpen, setUpsertOpen] = useState(false);
  const [upsertDraft, setUpsertDraft] = useState<ManualUpsertExternalWorkItemInput>({
    externalItemId: "",
    title: "",
    externalUrl: "",
    description: "",
    status: "",
    priority: "",
    requester: "",
    assignedTo: "",
    department: "",
    category: "",
    dueDateUtc: "",
    lastModifiedUtc: "",
    rawJson: "",
  });
  const [upsertSaving, setUpsertSaving] = useState(false);

  const [discoveredFields, setDiscoveredFields] = useState<SharePointDiscoveredFieldResponse[]>([]);
  const [discoverLoading, setDiscoverLoading] = useState(false);
  const [discoverError, setDiscoverError] = useState<string | null>(null);

  const [syncLoading, setSyncLoading] = useState(false);
  const [syncSummary, setSyncSummary] = useState<
    | { kind: "success"; data: ExternalSourceSyncResponse }
    | { kind: "error"; message: string }
    | null
  >(null);

  const [sourceReadiness, setSourceReadiness] = useState<ExternalSourceReadinessResponse | null>(null);
  const [readinessLoading, setReadinessLoading] = useState(false);
  const [readinessError, setReadinessError] = useState<string | null>(null);

  const [activityRows, setActivityRows] = useState<IntegrationActivityLogEntry[]>([]);
  const [activityLoading, setActivityLoading] = useState(false);
  const [activityError, setActivityError] = useState<string | null>(null);

  const selectedConnection = useMemo(
    () => connections.find((c) => c.id === selectedConnectionId) ?? null,
    [connections, selectedConnectionId],
  );

  const integrationNextAction = useMemo(
    () => computeIntegrationNextAction(selectedConnection),
    [selectedConnection],
  );

  const credentialConnDef = useMemo(
    () =>
      selectedConnection !== null
        ? providerDefinitions?.find((d) => d.provider === selectedConnection.provider)
        : undefined,
    [providerDefinitions, selectedConnection],
  );

  const credentialSecretFields = useMemo(
    () => (credentialConnDef?.fields ?? []).filter((f) => f.isSecret),
    [credentialConnDef],
  );

  const selectedSource = useMemo(
    () => sources.find((s) => s.id === selectedSourceId) ?? null,
    [sources, selectedSourceId],
  );

  const itemsContextProvider = useMemo(
    () => selectedSource?.provider ?? selectedConnection?.provider ?? null,
    [selectedSource, selectedConnection],
  );

  const externalItemsQueueCounts = useMemo(() => {
    const total = items.length;
    const linked = items.filter((i) => Boolean(i.cortexTicketId?.trim())).length;
    return { total, linked, needs: total - linked };
  }, [items]);

  const externalItemStatusOptions = useMemo(() => {
    const seen = new Set<string>();
    for (const i of items) {
      const s = i.status?.trim();
      if (s) {
        seen.add(s);
      }
    }
    return Array.from(seen).sort((a, b) => a.localeCompare(b));
  }, [items]);

  const filteredExternalItems = useMemo(() => {
    let list = items;
    if (itemsLinkFilter === "needsTicket") {
      list = list.filter((i) => !i.cortexTicketId?.trim());
    } else if (itemsLinkFilter === "linked") {
      list = list.filter((i) => Boolean(i.cortexTicketId?.trim()));
    }

    const q = itemsSearch.trim().toLowerCase();
    if (q) {
      list = list.filter((i) => {
        const parts = [
          i.title,
          i.externalItemId,
          i.requester,
          i.assignedTo,
          i.department,
          i.category,
          i.status,
          i.priority,
        ]
          .map((x) => (x ?? "").toLowerCase())
          .join("\n");
        return parts.includes(q);
      });
    }

    if (itemsPriorityFilter) {
      const p = itemsPriorityFilter.toLowerCase();
      list = list.filter(
        (i) => (i.priority ?? "").trim().toLowerCase() === p,
      );
    }

    if (itemsStatusFilter) {
      const s = itemsStatusFilter.toLowerCase();
      list = list.filter(
        (i) => (i.status ?? "").trim().toLowerCase() === s,
      );
    }

    return list;
  }, [
    items,
    itemsLinkFilter,
    itemsSearch,
    itemsPriorityFilter,
    itemsStatusFilter,
  ]);

  const loadSourceReadiness = useCallback(
    async (sourceId: number) => {
      setReadinessLoading(true);
      setReadinessError(null);
      try {
        const token = await getToken();
        const r = await integrationsService.getSourceReadiness(token, sourceId);
        setSourceReadiness(r);
      } catch {
        setSourceReadiness(null);
        setReadinessError("Unable to check source readiness.");
      } finally {
        setReadinessLoading(false);
      }
    },
    [getToken],
  );

  const loadConnections = useCallback(async () => {
    setConnectionsLoading(true);
    setConnectionsError(null);
    try {
      const token = await getToken();
      const list = await integrationsService.listConnections(token);
      setConnections(list);
      setSelectedConnectionId((prev) => {
        if (prev !== null && list.some((c) => c.id === prev)) {
          return prev;
        }
        return list[0]?.id ?? null;
      });
    } catch (e) {
      setConnectionsError(getUserFacingErrorMessage(e, "Unable to load connections."));
    } finally {
      setConnectionsLoading(false);
    }
  }, [getToken]);

  const refreshCredentialStatus = useCallback(async () => {
    if (selectedConnectionId === null) {
      setCredentialStatusDetail(null);
      setCredentialStatusError(null);
      setCredentialDraft({});
      return;
    }
    setCredentialStatusLoading(true);
    setCredentialStatusError(null);
    try {
      const token = await getToken();
      const s = await integrationsService.getCredentialStatus(token, selectedConnectionId);
      setCredentialStatusDetail(s);
    } catch {
      setCredentialStatusDetail(null);
      setCredentialStatusError(
        "Unable to load credential status. Administrator access may be required.",
      );
    } finally {
      setCredentialStatusLoading(false);
    }
  }, [getToken, selectedConnectionId]);

  useEffect(() => {
    if (tab !== "connections" || selectedConnectionId === null || connections.length === 0) {
      setCredentialStatusDetail(null);
      setCredentialStatusError(null);
      setCredentialDraft({});
      return;
    }
    void refreshCredentialStatus();
  }, [tab, selectedConnectionId, connections.length, refreshCredentialStatus]);

  useEffect(() => {
    setCredentialDraft({});
  }, [selectedConnectionId]);

  const loadSources = useCallback(
    async (connectionId: number) => {
      setSourcesLoading(true);
      setSourcesError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.listSources(token, connectionId);
        setSources(list);
        setSelectedSourceId((prev) => {
          if (prev !== null && list.some((s) => s.id === prev)) {
            return prev;
          }
          return list[0]?.id ?? null;
        });
      } catch (e) {
        setSourcesError(getUserFacingErrorMessage(e, "Unable to load sources."));
        setSources([]);
        setSelectedSourceId(null);
      } finally {
        setSourcesLoading(false);
      }
    },
    [getToken],
  );

  const loadFieldMappings = useCallback(
    async (sourceId: number) => {
      setFieldLoading(true);
      setFieldError(null);
      try {
        const token = await getToken();
        const [list, overview] = await Promise.all([
          integrationsService.getFieldMappings(token, sourceId),
          integrationsService.getSourceFieldsOverview(token, sourceId),
        ]);
        setFieldDraft(
          list.map((m) => ({
            externalFieldName: m.externalFieldName,
            externalFieldKey: m.externalFieldKey ?? "",
            cortexField: m.cortexField,
            isRequired: m.isRequired,
            transformHint: m.transformHint ?? "",
          })),
        );
        setFieldsOverview(overview);
      } catch (e) {
        setFieldError(getUserFacingErrorMessage(e, "Unable to load field mappings."));
        setFieldDraft([]);
        setFieldsOverview(null);
      } finally {
        setFieldLoading(false);
      }
    },
    [getToken],
  );

  const loadBoardMappings = useCallback(
    async (sourceId: number) => {
      setBoardLoading(true);
      setBoardError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.getBoardMappings(token, sourceId);
        setBoardDraft(
          list.map((m) => ({
            boardId: m.boardId,
            mappingMode: m.mappingMode,
            isDefault: m.isDefault,
          })),
        );
      } catch (e) {
        setBoardError(getUserFacingErrorMessage(e, "Unable to load board mappings."));
        setBoardDraft([]);
      } finally {
        setBoardLoading(false);
      }
    },
    [getToken],
  );

  const loadItems = useCallback(
    async (sourceId: number) => {
      setItemsLoading(true);
      setItemsError(null);
      try {
        const token = await getToken();
        const list = await integrationsService.listWorkItems(token, sourceId);
        setItems(list);
      } catch (e) {
        setItemsError(getUserFacingErrorMessage(e, "Unable to load external work items."));
        setItems([]);
      } finally {
        setItemsLoading(false);
      }
    },
    [getToken],
  );

  const loadActivity = useCallback(
    async (connectionId: number) => {
      setActivityLoading(true);
      setActivityError(null);
      try {
        const token = await getToken();
        const rows = await integrationsService.getConnectionActivity(token, connectionId, { take: 50 });
        setActivityRows(rows);
      } catch {
        setActivityError("Unable to load integration activity.");
        setActivityRows([]);
      } finally {
        setActivityLoading(false);
      }
    },
    [getToken],
  );

  const runConnectionTest = useCallback(async () => {
    if (selectedConnectionId === null) {
      return;
    }
    setConnectionTestLoading(true);
    try {
      const token = await getToken();
      const r = await integrationsService.testConnection(token, selectedConnectionId);
      await loadConnections();
      if (r.testSucceeded) {
        toast.success("Connection test completed.");
      } else {
        toast.error("Connection test completed with issues.");
      }
      void loadActivity(selectedConnectionId);
    } catch (e) {
      toast.error(getUserFacingErrorMessage(e, "Connection test could not be completed."));
    } finally {
      setConnectionTestLoading(false);
    }
  }, [getToken, selectedConnectionId, loadConnections, loadActivity]);

  useEffect(() => {
    void loadConnections();
  }, [loadConnections]);

  useEffect(() => {
    if (selectedConnectionId !== null) {
      void loadSources(selectedConnectionId);
    } else {
      setSources([]);
      setSelectedSourceId(null);
    }
  }, [selectedConnectionId, loadSources]);

  useEffect(() => {
    if (selectedSourceId === null) {
      setSourceReadiness(null);
      setReadinessError(null);
      return;
    }
    void loadSourceReadiness(selectedSourceId);
  }, [selectedSourceId, loadSourceReadiness]);

  useEffect(() => {
    if (tab === "fields" && selectedSourceId !== null) {
      void loadFieldMappings(selectedSourceId);
    }
  }, [tab, selectedSourceId, loadFieldMappings]);

  useEffect(() => {
    if (tab === "boards" && selectedSourceId !== null) {
      void loadBoardMappings(selectedSourceId);
      if (ticketBoards.length === 0 && !ticketBoardLoading) {
        void onRefreshTicketBoards();
      }
    }
  }, [tab, selectedSourceId, loadBoardMappings, ticketBoards.length, ticketBoardLoading, onRefreshTicketBoards]);

  useEffect(() => {
    if (tab === "items" && selectedSourceId !== null) {
      void loadItems(selectedSourceId);
    }
  }, [tab, selectedSourceId, loadItems]);

  useEffect(() => {
    if (tab === "activity" && selectedConnectionId !== null) {
      void loadActivity(selectedConnectionId);
    }
  }, [tab, selectedConnectionId, loadActivity]);

  useEffect(() => {
    setDiscoveredFields([]);
    setDiscoverError(null);
    setSyncSummary(null);
    setFieldsOverview(null);
  }, [selectedSourceId]);

  useEffect(() => {
    setItemDetail(null);
  }, [tab, selectedSourceId]);

  useEffect(() => {
    setItemsLinkFilter("all");
    setItemsSearch("");
    setItemsPriorityFilter("");
    setItemsStatusFilter("");
  }, [selectedSourceId]);

  useEffect(() => {
    if (!itemDetail || itemsLoading) {
      return;
    }
    if (!items.some((i) => i.id === itemDetail.id)) {
      setItemDetail(null);
    }
  }, [items, itemsLoading, itemDetail]);

  useEffect(() => {
    if (!itemDetail) {
      return;
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setItemDetail(null);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [itemDetail]);

  const discoverActionDisabled =
    !selectedSourceId ||
    discoverLoading ||
    fieldLoading ||
    readinessLoading ||
    !!readinessError ||
    !sourceReadiness?.canDiscoverFields;

  const syncActionDisabled =
    !selectedSourceId ||
    syncLoading ||
    readinessLoading ||
    !!readinessError ||
    !sourceReadiness?.canSync;

  const showBanner = (type: "ok" | "err", text: string) => {
    setBanner({ type, text });
    window.setTimeout(() => setBanner(null), 6000);
  };

  const startCreateCortexTicket = useCallback(
    async (item: ExternalWorkItemResponse) => {
      if (item.cortexTicketId?.trim()) {
        showBanner("err", "This external item is already linked to a Cortex ticket.");
        return;
      }
      if (selectedSourceId === null) {
        showBanner("err", "Select an external source first.");
        return;
      }
      setCreateTicketError(null);
      let defaultBoard: number | undefined;
      try {
        const token = await getToken();
        const maps = await integrationsService.getBoardMappings(token, selectedSourceId);
        defaultBoard = maps.find((m) => m.isDefault)?.boardId ?? maps[0]?.boardId;
      } catch {
        /* board dropdown still populated from ticketBoards */
      }
      const enabledBoards = ticketBoards.filter((b) => b.isEnabled);
      const boardId =
        defaultBoard !== undefined && enabledBoards.some((b) => b.id === defaultBoard)
          ? defaultBoard
          : (enabledBoards[0]?.id ?? "");
      setCreateTicketDraft({
        title: item.title ?? "",
        description: item.description ?? "",
        boardId,
        priority: normalizeExternalPriority(item.priority),
        dueDateUtc: toDatetimeLocalInput(item.dueDateUtc),
        department: item.department ?? "",
        category: item.category ?? "",
        requester: item.requester ?? "",
        assignedTo: item.assignedTo ?? "",
      });
      setCreateTicketFor(item);
      setCreateTicketOpen(true);
    },
    [getToken, selectedSourceId, ticketBoards],
  );

  const submitCreateCortexTicket = useCallback(async () => {
    if (!createTicketFor || selectedSourceId === null) {
      return;
    }
    if (createTicketDraft.boardId === "" || createTicketDraft.boardId === 0) {
      setCreateTicketError("Select a Cortex board.");
      return;
    }
    if (!createTicketDraft.title.trim()) {
      setCreateTicketError("Title is required.");
      return;
    }
    setCreateTicketSaving(true);
    setCreateTicketError(null);
    try {
      const token = await getToken();
      const body: CreateTicketFromExternalItemInput = {
        boardId: createTicketDraft.boardId,
        title: createTicketDraft.title.trim(),
        description: createTicketDraft.description.trim() || null,
        priority: createTicketDraft.priority,
        department: createTicketDraft.department.trim() || null,
        category: createTicketDraft.category.trim() || null,
        requester: createTicketDraft.requester.trim() || null,
        assignedTo: createTicketDraft.assignedTo.trim() || null,
        dueDateUtc: createTicketDraft.dueDateUtc
          ? new Date(createTicketDraft.dueDateUtc).toISOString()
          : null,
      };
      const result = await integrationsService.createTicketFromExternalItem(
        token,
        createTicketFor.id,
        body,
      );
      showBanner("ok", result.message);
      setCreateTicketOpen(false);
      setCreateTicketFor(null);
      await loadItems(selectedSourceId);
      setItemDetail(result.externalItem);
    } catch (e) {
      setCreateTicketError(
        getUserFacingErrorMessage(e, "Unable to create Cortex ticket from this external item."),
      );
    } finally {
      setCreateTicketSaving(false);
    }
  }, [
    createTicketDraft,
    createTicketFor,
    getToken,
    loadItems,
    selectedSourceId,
  ]);

  const openCreateConnection = () => {
    const providers = providerDefinitions?.length
      ? providerDefinitions
      : INTEGRATION_PROVIDERS.map((p) => ({
          provider: p,
          displayName: p,
          description: "",
          allowedAuthModes: ["Manual" as IntegrationAuthMode],
          allowedSyncModes: ["ReadOnly" as IntegrationSyncMode],
          fields: [],
          supportsFieldDiscovery: false,
          supportsSync: false,
          supportsTicketCreationFromExternalItem: false,
          referenceMetadataOnly: false,
        }));
    const first = providers.find((d) => d.provider === "SharePoint") ?? providers[0];
    const auth0 = first.allowedAuthModes[0] ?? "Manual";
    const sync0 = first.allowedSyncModes[0] ?? "ReadOnly";
    setConnectionModal({
      mode: "create",
      draft: {
        provider: first.provider,
        displayName: "",
        authMode: auth0,
        syncMode: sync0,
        isEnabled: true,
        providerSettings: {},
      },
    });
  };

  const openEditConnection = (c: IntegrationConnectionResponse) => {
    const settings: Record<string, string> = {};
    for (const [k, v] of Object.entries(c.safeProviderSettings ?? {})) {
      if (v != null && String(v).length > 0) {
        settings[k] = String(v);
      }
    }
    setConnectionModal({
      mode: "edit",
      id: c.id,
      draft: {
        provider: c.provider,
        displayName: c.displayName,
        authMode: c.authMode,
        syncMode: c.syncMode,
        isEnabled: c.isEnabled,
        providerSettings: settings,
      },
    });
  };

  const saveConnectionModal = async () => {
    if (!connectionModal) {
      return;
    }
    const def = providerDefinitions?.find((d) => d.provider === connectionModal.draft.provider);
    const secretKeySet = new Set(
      (def?.fields ?? []).filter((f) => f.isSecret).map((f) => f.key),
    );
    try {
      const token = await getToken();
      if (connectionModal.mode === "create") {
        const d = connectionModal.draft;
        if (!d.displayName.trim()) {
          showBanner("err", "Display name is required.");
          return;
        }
        const psPayload = buildProviderSettingsPayload(d.providerSettings, secretKeySet);
        await integrationsService.createConnection(token, {
          provider: d.provider,
          displayName: d.displayName.trim(),
          tenantId: d.provider === "SharePoint" ? psPayload.tenantId?.trim() || null : null,
          organizationId: d.provider === "SharePoint" ? psPayload.siteUrl?.trim() || null : null,
          providerSettings: psPayload,
          authMode: d.authMode ?? "Manual",
          syncMode: d.syncMode ?? "ReadOnly",
          isEnabled: d.isEnabled ?? true,
        });
        showBanner(
          "ok",
          "Connection created. Configure any required credentials under Connection credentials below.",
        );
      } else {
        const d = connectionModal.draft;
        if (!d.displayName.trim()) {
          showBanner("err", "Display name is required.");
          return;
        }
        const psPayload = buildProviderSettingsPayload(d.providerSettings, secretKeySet);
        await integrationsService.updateConnection(token, connectionModal.id, {
          displayName: d.displayName.trim(),
          tenantId: d.provider === "SharePoint" ? psPayload.tenantId?.trim() || null : undefined,
          organizationId: d.provider === "SharePoint" ? psPayload.siteUrl?.trim() || null : undefined,
          providerSettings: psPayload,
          authMode: d.authMode ?? undefined,
          syncMode: d.syncMode ?? undefined,
          isEnabled: d.isEnabled ?? undefined,
        });
        showBanner("ok", "Connection updated.");
      }
      setConnectionModal(null);
      await loadConnections();
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
      if (tab === "connections") {
        void refreshCredentialStatus();
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save connection."));
    }
  };

  const saveConnectionCredentials = async () => {
    if (selectedConnectionId === null || credentialSecretFields.length === 0) {
      return;
    }
    const secrets: Record<string, string | null> = {};
    for (const f of credentialSecretFields) {
      const v = credentialDraft[f.key]?.trim();
      if (v) {
        secrets[f.key] = v;
      }
    }
    if (Object.keys(secrets).length === 0) {
      showBanner("err", "Enter a new credential value to configure or rotate this connection.");
      return;
    }
    setCredentialSaveLoading(true);
    try {
      const token = await getToken();
      await integrationsService.configureCredentials(token, selectedConnectionId, { secrets });
      setCredentialDraft({});
      showBanner(
        "ok",
        "Credential saved. Cortex will not show the current value. Use Rotate to supply a new value when needed.",
      );
      await refreshCredentialStatus();
      await loadConnections();
      if (tab === "activity") {
        void loadActivity(selectedConnectionId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save credentials."));
    } finally {
      setCredentialSaveLoading(false);
    }
  };

  const clearConnectionCredentials = async () => {
    if (selectedConnectionId === null) {
      return;
    }
    if (
      !window.confirm(
        "Clearing this credential may prevent Cortex from testing or syncing this connection until a new credential is configured.",
      )
    ) {
      return;
    }
    setCredentialClearLoading(true);
    try {
      const token = await getToken();
      await integrationsService.clearCredentials(token, selectedConnectionId);
      setCredentialDraft({});
      showBanner("ok", "Credential cleared.");
      await refreshCredentialStatus();
      await loadConnections();
      if (tab === "activity") {
        void loadActivity(selectedConnectionId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to clear credentials."));
    } finally {
      setCredentialClearLoading(false);
    }
  };

  const toggleConnectionEnabled = async (c: IntegrationConnectionResponse) => {
    try {
      const token = await getToken();
      await integrationsService.setConnectionEnabled(token, c.id, !c.isEnabled);
      showBanner("ok", c.isEnabled ? "Connection disabled." : "Connection enabled.");
      await loadConnections();
      if (selectedConnectionId === c.id) {
        await loadSources(c.id);
      }
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to update connection."));
    }
  };

  const openCreateSource = () => {
    if (!selectedConnection) {
      showBanner("err", "Select a connection first.");
      return;
    }
    if (selectedConnection.provider === "SapReference") {
      showBanner(
        "err",
        "SAP Reference connections are metadata-only and do not use external work sources here. Manage catalog data under Configuration → SAP Reference.",
      );
      return;
    }
    setSourceModal({
      mode: "create",
      draft: {
        provider: selectedConnection.provider,
        sourceType: "SharePointList",
        externalSourceId: "",
        name: "",
        externalUrl: "",
        isEnabled: true,
      },
    });
  };

  const openEditSource = (s: ExternalWorkSourceResponse) => {
    setSourceModal({
      mode: "edit",
      id: s.id,
      draft: {
        provider: s.provider,
        sourceType: s.sourceType,
        externalSourceId: s.externalSourceId,
        name: s.name,
        externalUrl: s.externalUrl ?? "",
        isEnabled: s.isEnabled,
      },
    });
  };

  const saveSourceModal = async () => {
    if (!sourceModal || selectedConnectionId === null) {
      return;
    }
    try {
      const token = await getToken();
      if (sourceModal.mode === "create") {
        const d = sourceModal.draft;
        if (!d.name.trim() || !d.externalSourceId.trim()) {
          showBanner("err", "Name and external source ID are required.");
          return;
        }
        await integrationsService.createSource(token, selectedConnectionId, {
          provider: d.provider,
          sourceType: d.sourceType,
          externalSourceId: d.externalSourceId.trim(),
          name: d.name.trim(),
          externalUrl: d.externalUrl?.trim() || null,
          isEnabled: d.isEnabled ?? true,
        });
        showBanner("ok", "Source created.");
      } else {
        const d = sourceModal.draft;
        if (!d.name.trim()) {
          showBanner("err", "Name is required.");
          return;
        }
        await integrationsService.updateSource(token, sourceModal.id, {
          name: d.name.trim(),
          externalUrl: d.externalUrl?.trim() || null,
          provider: d.provider,
          sourceType: d.sourceType,
          externalSourceId: d.externalSourceId.trim() || undefined,
          isEnabled: d.isEnabled ?? undefined,
        });
        showBanner("ok", "Source updated.");
      }
      setSourceModal(null);
      await loadSources(selectedConnectionId);
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save source."));
    }
  };

  const toggleSourceEnabled = async (s: ExternalWorkSourceResponse) => {
    if (selectedConnectionId === null) {
      return;
    }
    try {
      const token = await getToken();
      await integrationsService.setSourceEnabled(token, s.id, !s.isEnabled);
      showBanner("ok", s.isEnabled ? "Source disabled." : "Source enabled.");
      await loadSources(selectedConnectionId);
      if (selectedSourceId !== null) {
        void loadSourceReadiness(selectedSourceId);
      }
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to update source."));
    }
  };

  const saveFieldMappings = async () => {
    if (selectedSourceId === null) {
      return;
    }
    for (const row of fieldDraft) {
      if (!row.externalFieldName.trim()) {
        showBanner("err", "Each row needs an external field name.");
        return;
      }
    }
    setFieldSaving(true);
    try {
      const token = await getToken();
      const body = fieldDraft.map((row) => ({
        externalFieldName: row.externalFieldName.trim(),
        externalFieldKey: row.externalFieldKey?.trim() || null,
        cortexField: row.cortexField,
        isRequired: row.isRequired,
        transformHint: row.transformHint?.trim() || null,
      }));
      await integrationsService.replaceFieldMappings(token, selectedSourceId, body);
      showBanner("ok", "Mapping saved.");
      await loadFieldMappings(selectedSourceId);
      void loadSourceReadiness(selectedSourceId);
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save field mappings."));
    } finally {
      setFieldSaving(false);
    }
  };

  const saveBoardMappings = async () => {
    if (selectedSourceId === null) {
      return;
    }
    for (const row of boardDraft) {
      if (!row.boardId || !ticketBoards.some((b) => b.id === row.boardId)) {
        showBanner("err", "Each row needs a valid Cortex board.");
        return;
      }
    }
    setBoardSaving(true);
    try {
      const token = await getToken();
      await integrationsService.replaceBoardMappings(token, selectedSourceId, boardDraft);
      showBanner("ok", "Board mappings saved.");
      await loadBoardMappings(selectedSourceId);
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save board mappings."));
    } finally {
      setBoardSaving(false);
    }
  };

  const submitUpsert = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (!upsertDraft.externalItemId.trim() || !upsertDraft.title.trim()) {
      showBanner("err", "External item ID and title are required.");
      return;
    }
    setUpsertSaving(true);
    try {
      const token = await getToken();
      const raw =
        upsertDraft.rawJson?.trim() ||
        JSON.stringify({
          externalItemId: upsertDraft.externalItemId.trim(),
          title: upsertDraft.title.trim(),
          description: upsertDraft.description?.trim() || undefined,
          status: upsertDraft.status?.trim() || undefined,
          priority: upsertDraft.priority?.trim() || undefined,
        });
      const body: ManualUpsertExternalWorkItemInput = {
        externalItemId: upsertDraft.externalItemId.trim(),
        title: upsertDraft.title.trim(),
        externalUrl: upsertDraft.externalUrl?.trim() || null,
        description: upsertDraft.description?.trim() || null,
        status: upsertDraft.status?.trim() || null,
        priority: upsertDraft.priority?.trim() || null,
        requester: upsertDraft.requester?.trim() || null,
        assignedTo: upsertDraft.assignedTo?.trim() || null,
        department: upsertDraft.department?.trim() || null,
        category: upsertDraft.category?.trim() || null,
        dueDateUtc: upsertDraft.dueDateUtc
          ? new Date(upsertDraft.dueDateUtc).toISOString()
          : null,
        lastModifiedUtc: upsertDraft.lastModifiedUtc
          ? new Date(upsertDraft.lastModifiedUtc).toISOString()
          : null,
        rawJson: raw,
      };
      await integrationsService.manualUpsertWorkItem(token, selectedSourceId, body);
      setUpsertOpen(false);
      showBanner("ok", "External work item saved.");
      await loadItems(selectedSourceId);
      if (tab === "activity" && selectedConnectionId !== null) {
        void loadActivity(selectedConnectionId);
      }
      setUpsertDraft({
        externalItemId: "",
        title: "",
        externalUrl: "",
        description: "",
        status: "",
        priority: "",
        requester: "",
        assignedTo: "",
        department: "",
        category: "",
        dueDateUtc: "",
        lastModifiedUtc: "",
        rawJson: "",
      });
    } catch (e) {
      showBanner("err", getUserFacingErrorMessage(e, "Unable to save work item."));
    } finally {
      setUpsertSaving(false);
    }
  };

  const discoverFieldsFromSource = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (
      readinessLoading ||
      readinessError ||
      !sourceReadiness?.canDiscoverFields ||
      discoverLoading ||
      fieldLoading
    ) {
      return;
    }
    setDiscoverLoading(true);
    setDiscoverError(null);
    try {
      const token = await getToken();
      const list = await integrationsService.discoverSharePointFields(token, selectedSourceId);
      setDiscoveredFields(list);
    } catch (e) {
      setDiscoverError(getUserFacingErrorMessage(e, "Unable to discover fields."));
      setDiscoveredFields([]);
    } finally {
      setDiscoverLoading(false);
      void loadFieldMappings(selectedSourceId);
      void loadSourceReadiness(selectedSourceId);
      if (tab === "activity" && selectedConnectionId !== null) {
        void loadActivity(selectedConnectionId);
      }
    }
  };

  const addDiscoveredFieldToMapping = (field: SharePointDiscoveredFieldResponse) => {
    const id = mappingRowIdentity(field.externalFieldName, field.externalFieldKey);
    if (!id) {
      return;
    }
    const exists = fieldDraft.some(
      (row) => mappingRowIdentity(row.externalFieldName, row.externalFieldKey) === id,
    );
    if (exists) {
      return;
    }
    const suggested = field.suggestedCortexField;
    const cortex: CortexField =
      suggested && CORTEX_FIELDS.includes(suggested) ? suggested : "Unknown";
    setFieldDraft([
      ...fieldDraft,
      {
        externalFieldName: field.externalFieldName,
        externalFieldKey: field.externalFieldKey?.trim() || "",
        cortexField: cortex,
        isRequired: false,
        transformHint: "",
      },
    ]);
  };

  const syncExternalSourceNow = async () => {
    if (selectedSourceId === null) {
      return;
    }
    if (readinessLoading || readinessError || !sourceReadiness?.canSync || syncLoading) {
      return;
    }
    setSyncLoading(true);
    setSyncSummary(null);
    try {
      const token = await getToken();
      const result = await integrationsService.syncSharePointSource(token, selectedSourceId);
      setSyncSummary({ kind: "success", data: result });
      await loadItems(selectedSourceId);
      await loadConnections();
      if (selectedConnectionId !== null) {
        await loadSources(selectedConnectionId);
      }
    } catch (e) {
      const msg = getUserFacingErrorMessage(e, "Sync failed.");
      setSyncSummary({ kind: "error", message: msg });
    } finally {
      setSyncLoading(false);
      void loadSourceReadiness(selectedSourceId);
      if (tab === "activity" && selectedConnectionId !== null) {
        void loadActivity(selectedConnectionId);
      }
    }
  };

  const selectSourceForMapping = (sourceId: number) => {
    setSelectedSourceId(sourceId);
    setTab("fields");
  };

  const tabButtons: { id: IntegrationsTab; label: string }[] = [
    { id: "connections", label: "Connections" },
    { id: "sources", label: "Sources" },
    { id: "fields", label: "Field mapping" },
    { id: "boards", label: "Board mapping" },
    { id: "items", label: "External items" },
    { id: "activity", label: "Activity" },
  ];

  const modalConnDef =
    connectionModal !== null
      ? providerDefinitions?.find((d) => d.provider === connectionModal.draft.provider)
      : undefined;
  const modalProviderChoices =
    providerDefinitions?.map((d) => ({ value: d.provider, label: d.displayName })) ??
    INTEGRATION_PROVIDERS.map((p) => ({ value: p, label: p }));
  const modalAuthChoices =
    modalConnDef?.allowedAuthModes?.length ? modalConnDef.allowedAuthModes : AUTH_MODES;
  const modalSyncChoices =
    modalConnDef?.allowedSyncModes?.length ? modalConnDef.allowedSyncModes : SYNC_MODES;

  const updateConnectionDraftSettings = (key: string, value: string) => {
    setConnectionModal((m) => {
      if (!m) {
        return m;
      }
      const nextSettings = { ...m.draft.providerSettings, [key]: value };
      if (m.mode === "create") {
        return { mode: "create" as const, draft: { ...m.draft, providerSettings: nextSettings } };
      }
      return { mode: "edit" as const, id: m.id, draft: { ...m.draft, providerSettings: nextSettings } };
    });
  };

  return (
    <div className="min-w-0 max-w-full space-y-6">
      {banner ? (
        <div
          className={
            banner.type === "ok"
              ? "rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-900 dark:border-green-900 dark:bg-green-950/40 dark:text-green-100"
              : "rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/40 dark:text-red-100"
          }
        >
          {banner.text}
        </div>
      ) : null}

      <section className="min-w-0 max-w-full rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">Integrations</h2>
        <p className="mt-1 text-sm leading-relaxed text-gray-600 dark:text-slate-400">
          Connect external work sources, configure provider-specific setup, and inspect external work before importing it
          into Cortex.
        </p>
      </section>

      <ConfigPageShell>
        <ConfigPageHeader
          title="External integrations"
          description="Walk through connection setup, credentials, health tests, sources, mappings, and activity. Use the provider readiness table for an honest view of what each integration supports in this release—SharePoint includes read-only discovery and sync; Jira and ServiceNow are setup-first; SAP Reference stays catalog metadata only."
        />
        <ConfigPageBody>
          <div className="space-y-6">
            <IntegrationSetupFlowGuide />
            <ReadOnlySecurityCallout />
            <ProviderReadinessMatrixSection />
            <div>
              <div className="flex min-w-0 max-w-full flex-wrap gap-2 border-b border-gray-200 pb-4 dark:border-slate-700">
                {tabButtons.map((b) => (
                  <button
                    key={b.id}
                    type="button"
                    onClick={() => setTab(b.id)}
                    className={`rounded-lg px-4 py-2 text-sm font-medium transition ${
                      tab === b.id
                        ? "bg-cortex-blue text-white shadow-sm dark:bg-cortex-blue"
                        : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                    }`}
                  >
                    {b.label}
                  </button>
                ))}
              </div>
              <p className="mt-3 text-sm leading-snug text-gray-600 dark:text-slate-400">
                {INTEGRATION_TAB_GUIDANCE[tab]}
              </p>
            </div>

            {connections.length > 0 ? (
              <ConfigDetailCard
                title="Connection readiness"
                subtitle="Summary for the connection you have selected below (defaults follow your current table selection)."
              >
                {selectedConnection ? (
                  <div className="space-y-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="text-base font-semibold text-gray-900 dark:text-slate-100">
                          {selectedConnection.displayName}
                        </p>
                        <div className="mt-1.5 flex flex-wrap items-center gap-2">
                          <span className="text-sm text-gray-700 dark:text-slate-300">
                            {connectionProviderLabel(providerDefinitions, selectedConnection.provider)}
                          </span>
                          {(() => {
                            const readinessPill = integrationProviderReadinessPill(selectedConnection.provider);
                            return (
                              <span
                                className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${readinessPill.className}`}
                              >
                                {readinessPill.label}
                              </span>
                            );
                          })()}
                          <span
                            className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                              selectedConnection.isEnabled
                                ? "bg-emerald-100 text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100"
                                : "bg-slate-200 text-slate-800 dark:bg-slate-600 dark:text-slate-100"
                            }`}
                          >
                            {selectedConnection.isEnabled ? "Enabled" : "Disabled"}
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                      <div>
                        <p className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-500">
                          Credentials
                        </p>
                        <p className="mt-1 text-sm text-gray-900 dark:text-slate-100">
                          {selectedConnection.credentialConfigured ? "Configured" : "Not configured"}
                        </p>
                      </div>
                      <div>
                        <p className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-500">
                          Health
                        </p>
                        <div className="mt-1">
                          {selectedConnection.health ? (
                            <span
                              title={selectedConnection.health.statusLabel}
                              className={`${connectionHealthBadgeLayout("table")} ${connectionHealthBadgeClasses(
                                selectedConnection.health.status,
                              )}`}
                            >
                              {selectedConnection.health.statusLabel}
                            </span>
                          ) : (
                            "—"
                          )}
                        </div>
                      </div>
                      <div>
                        <p className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-500">
                          Last tested
                        </p>
                        <p className="mt-1 text-sm text-gray-900 dark:text-slate-100">
                          {selectedConnection.health?.lastTestedAtUtc
                            ? formatWhen(selectedConnection.health.lastTestedAtUtc)
                            : "—"}
                        </p>
                      </div>
                      <div className="sm:col-span-2 xl:col-span-3">
                        <p className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-500">
                          Last sync
                        </p>
                        <p className="mt-1 text-sm text-gray-900 dark:text-slate-100">
                          {!selectedConnection.lastSyncUtc
                            ? "Never synced"
                            : `${humanizeConnectionSyncStatus(selectedConnection.lastSyncStatus)} · ${formatWhen(
                                selectedConnection.lastSyncUtc,
                              )}`}
                        </p>
                      </div>
                    </div>

                    <div className="rounded-lg border border-gray-200 bg-gray-50/90 px-4 py-3 dark:border-slate-600 dark:bg-slate-800/50">
                      <p className="text-xs font-semibold uppercase tracking-wide text-gray-600 dark:text-slate-400">
                        Next recommended action
                      </p>
                      <p className="mt-1.5 text-sm font-medium text-gray-900 dark:text-slate-100">
                        {integrationNextAction}
                      </p>
                    </div>
                  </div>
                ) : (
                  <p className="text-sm text-gray-600 dark:text-slate-400">
                    Use the Connections tab to add a connection, or choose a connection from the selector when another tab
                    is open.
                  </p>
                )}
              </ConfigDetailCard>
            ) : null}

            <div className="min-w-0 max-w-full space-y-6">
            {tab !== "connections" && (
              <ConfigDetailCard
                title="Selection"
                subtitle="Choose a connection and an external source for field mapping, external items, and activity tabs."
              >
                <div className="grid gap-4 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Connection</label>
                    <select
                      className={configFieldClass}
                      value={selectedConnectionId ?? ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        setSelectedConnectionId(v ? Number(v) : null);
                      }}
                      disabled={connectionsLoading || connections.length === 0}
                    >
                      <option value="">— Select —</option>
                      {connections.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.displayName}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External source</label>
                    <select
                      className={configFieldClass}
                      value={selectedSourceId ?? ""}
                      onChange={(e) => {
                        const v = e.target.value;
                        setSelectedSourceId(v ? Number(v) : null);
                      }}
                      disabled={!selectedConnectionId || sourcesLoading || sources.length === 0}
                    >
                      <option value="">— Select —</option>
                      {sources.map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                {selectedSourceId ? (
                  <div className="mt-4 rounded-lg border border-gray-200 bg-gray-50/90 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/50">
                    <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Source readiness</h3>
                    {readinessLoading ? (
                      <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                    ) : readinessError ? (
                      <p className="mt-2 text-sm text-amber-900 dark:text-amber-100/90">{readinessError}</p>
                    ) : sourceReadiness ? (
                      <>
                        <p className="mt-2 text-sm text-gray-800 dark:text-slate-200">{readinessHeadline(sourceReadiness)}</p>
                        <ul className="mt-3 space-y-2 text-xs">
                          {sourceReadiness.checks.map((c) => (
                            <li
                              key={c.key}
                              className={`flex gap-2 rounded-md px-2 py-1 ${readinessCheckRowClass(c.status)}`}
                            >
                              <span className="shrink-0 font-medium" aria-hidden>
                                {c.status === "Passed" ? "✓" : c.status === "Warning" ? "!" : "✗"}
                              </span>
                              <span className="min-w-0">
                                <span className="font-medium">{c.label}</span>
                                <span className="block text-gray-600 dark:text-slate-400">{c.message}</span>
                              </span>
                            </li>
                          ))}
                        </ul>
                      </>
                    ) : (
                      <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">No readiness data.</p>
                    )}
                  </div>
                ) : null}
              </ConfigDetailCard>
            )}

            {tab === "connections" && (
              <div className="space-y-5">
                <div className="flex flex-wrap items-end justify-between gap-3">
                  <div className="min-w-0 max-w-2xl">
                    <h2 className="text-base font-semibold text-gray-900 dark:text-slate-100">Connections</h2>
                    <p className="mt-1 text-sm leading-snug text-gray-600 dark:text-slate-400">
                      Register each external system once, then finish credentials and health checks before adding sources on
                      the Sources tab.
                    </p>
                  </div>
                  <ConfigPrimaryButton onClick={openCreateConnection}>Add connection</ConfigPrimaryButton>
                </div>
                {connectionsError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{connectionsError}</p>
                ) : null}
                {connectionsLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading connections…</p>
                ) : connections.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    <p className="font-medium text-gray-800 dark:text-slate-200">No connections yet</p>
                    <p className="mx-auto mt-2 max-w-lg">
                      Add a connection to register an external system. Use the provider readiness table above to compare
                      capabilities before you invest in governance or credentials.
                    </p>
                  </div>
                ) : (
                  <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                    <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Name</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Provider</th>
                          <th className="min-w-[10.5rem] whitespace-nowrap px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                            Health
                          </th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Last tested</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Auth</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Sync</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Enabled</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Credentials</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Last sync</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Created</th>
                          <th className="px-4 py-3 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {connections.map((c) => (
                          <tr key={c.id} className="bg-white dark:bg-slate-900">
                            <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">{c.displayName}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">
                              {connectionProviderLabel(providerDefinitions, c.provider)}
                            </td>
                            <td className="align-middle whitespace-nowrap px-4 py-3">
                              {c.health ? (
                                <span
                                  title={c.health.statusLabel}
                                  className={`${connectionHealthBadgeLayout("table")} ${connectionHealthBadgeClasses(
                                    c.health.status,
                                  )}`}
                                >
                                  {c.health.statusLabel}
                                </span>
                              ) : (
                                <span className="text-gray-500 dark:text-slate-500">—</span>
                              )}
                            </td>
                            <td className="px-4 py-3 text-gray-600 dark:text-slate-400">
                              {c.health?.lastTestedAtUtc ? formatWhen(c.health.lastTestedAtUtc) : "—"}
                            </td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeIntegrationAuthMode(c.authMode)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeIntegrationSyncMode(c.syncMode)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{c.isEnabled ? "Yes" : "No"}</td>
                            <td className="max-w-[200px] px-4 py-3 text-gray-700 dark:text-slate-300">
                              {c.credentialConfigured ? (
                                <span className="font-medium text-green-800 dark:text-green-200/90">Credential configured</span>
                              ) : (
                                <span>Credential not configured</span>
                              )}
                              {c.lastCredentialUpdatedAtUtc ? (
                                <div className="mt-0.5 text-xs text-gray-500 dark:text-slate-500">
                                  Updated {formatWhen(c.lastCredentialUpdatedAtUtc)}
                                </div>
                              ) : null}
                            </td>
                            <td
                              className="max-w-[220px] px-4 py-3 text-gray-700 dark:text-slate-300"
                              title={
                                c.lastSyncMessage?.trim()
                                  ? c.lastSyncMessage.trim()
                                  : c.lastSyncUtc
                                    ? undefined
                                    : "No sync has completed for this connection yet."
                              }
                            >
                              {!c.lastSyncUtc ? (
                                <span className="text-gray-600 dark:text-slate-400">Never synced</span>
                              ) : (
                                <div className="space-y-0.5">
                                  <div className="font-medium text-gray-900 dark:text-slate-100">
                                    {humanizeConnectionSyncStatus(c.lastSyncStatus)}
                                  </div>
                                  <div className="text-xs text-gray-600 dark:text-slate-400">
                                    {formatWhen(c.lastSyncUtc)}
                                  </div>
                                  {c.lastSyncMessage?.trim() ? (
                                    <div className="line-clamp-2 text-xs text-gray-500 dark:text-slate-500">
                                      {c.lastSyncMessage.trim()}
                                    </div>
                                  ) : null}
                                </div>
                              )}
                            </td>
                            <td className="px-4 py-3 text-gray-600 dark:text-slate-400">{formatWhen(c.createdAtUtc)}</td>
                            <td className="space-x-2 whitespace-nowrap px-4 py-3 text-right">
                              <ConfigGhostButton className="!py-1.5" onClick={() => openEditConnection(c)}>
                                Edit
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => void toggleConnectionEnabled(c)}>
                                {c.isEnabled ? "Disable" : "Enable"}
                              </ConfigGhostButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                {!connectionsLoading && connections.length > 0 ? (
                  <ConfigDetailCard
                    title="Connection health"
                    subtitle={
                      selectedConnection
                        ? integrationProviderMaturityMessage(selectedConnection.provider)
                        : "Select a connection to review health and run a safe test."
                    }
                  >
                    <div className="space-y-4">
                      {selectedConnection?.health ? (
                        <>
                          <div className="flex flex-wrap items-center gap-3">
                            <span
                              title={selectedConnection.health.statusLabel}
                              className={`${connectionHealthBadgeLayout("card")} ${connectionHealthBadgeClasses(
                                selectedConnection.health.status,
                              )}`}
                            >
                              {selectedConnection.health.statusLabel}
                            </span>
                            {selectedConnection.health.lastTestedAtUtc ? (
                              <span className="text-sm text-gray-600 dark:text-slate-400">
                                Last tested {formatWhen(selectedConnection.health.lastTestedAtUtc)}
                              </span>
                            ) : (
                              <span className="text-sm text-gray-600 dark:text-slate-400">Not tested yet</span>
                            )}
                          </div>
                          <p className="text-sm text-gray-800 dark:text-slate-200">{selectedConnection.health.message}</p>
                          {(() => {
                            const sup = connectionHealthSupplementaryNote(
                              selectedConnection.provider,
                              selectedConnection.health.testMode,
                              selectedConnection.health.status,
                            );
                            return sup ? (
                              <p className="text-xs text-gray-600 dark:text-slate-400">{sup}</p>
                            ) : null;
                          })()}
                          {selectedConnection.health.missingRequiredSettingKeys.length > 0 ? (
                            <p className="text-xs text-gray-700 dark:text-slate-300">
                              <span className="font-medium">Missing required settings: </span>
                              {selectedConnection.health.missingRequiredSettingKeys.join(", ")}
                            </p>
                          ) : null}
                          {selectedConnection.health.invalidFormatSettingKeys.length > 0 ? (
                            <p className="text-xs text-gray-700 dark:text-slate-300">
                              <span className="font-medium">Invalid format: </span>
                              {selectedConnection.health.invalidFormatSettingKeys.join(", ")}
                            </p>
                          ) : null}
                          {selectedConnection.health.missingCredentialFieldKeys.length > 0 ? (
                            <p className="text-xs text-gray-700 dark:text-slate-300">
                              <span className="font-medium">Missing credential fields: </span>
                              {selectedConnection.health.missingCredentialFieldKeys.join(", ")}
                            </p>
                          ) : null}
                          <div className="flex flex-wrap justify-end gap-2 pt-1">
                            <ConfigPrimaryButton
                              disabled={connectionTestLoading || selectedConnectionId === null}
                              onClick={() => void runConnectionTest()}
                            >
                              {connectionTestLoading ? "Testing connection..." : "Test connection"}
                            </ConfigPrimaryButton>
                          </div>
                        </>
                      ) : (
                        <p className="text-sm text-gray-600 dark:text-slate-400">
                          Select a connection below to view health details.
                        </p>
                      )}
                    </div>
                  </ConfigDetailCard>
                ) : null}
                {!connectionsLoading && connections.length > 0 ? (
                  <ConfigDetailCard
                    title="Connection credentials"
                    subtitle="Configure or rotate secrets for a connection. Values are sent only to the credential endpoint and are never returned or shown in this UI."
                  >
                    <div className="space-y-4">
                      <div>
                        <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Connection</label>
                        <select
                          className={configFieldClass}
                          value={selectedConnectionId ?? ""}
                          onChange={(e) => {
                            const raw = e.target.value;
                            setSelectedConnectionId(raw ? Number(raw) : null);
                          }}
                        >
                          {connections.map((c) => (
                            <option key={c.id} value={c.id}>
                              {c.displayName}
                            </option>
                          ))}
                        </select>
                      </div>
                      {selectedConnection ? (
                        <>
                          {credentialSecretFields.length === 0 ? (
                            <Callout title="No stored secrets required for this provider profile">
                              {selectedConnection.provider === "SapReference" ? (
                                <>
                                  SAP Reference is metadata-only in Cortex. Do not enter live SAP credentials; reference data is
                                  managed separately.
                                </>
                              ) : selectedConnection.provider === "SharePoint" ? (
                                <>
                                  SharePoint can rely on the host Microsoft Graph app registration and tenant scope you
                                  already configured. Per-connection stored credentials are optional—use them only when your
                                  governance model requires it.
                                </>
                              ) : (
                                <>
                                  This provider does not expose secret fields for the secure credential store in this
                                  release. Non-secret settings remain in the connection editor.
                                </>
                              )}
                            </Callout>
                          ) : (
                            <>
                              <Callout title="Dedicated credential flow">
                                Enter a new credential value to configure or rotate this connection. Cortex will not show the
                                current value. After saving, fields clear automatically.
                              </Callout>
                              {selectedConnection.provider === "Jira" || selectedConnection.provider === "ServiceNow" ? (
                                <p className="text-xs text-gray-600 dark:text-slate-400">
                                  Secrets are stored for future validation. Cortex does not call live Jira or ServiceNow APIs
                                  from Integrations in this release.
                                </p>
                              ) : null}
                              {credentialStatusError ? (
                                <p className="text-sm text-amber-800 dark:text-amber-200/90">{credentialStatusError}</p>
                              ) : null}
                              {credentialStatusLoading ? (
                                <p className="text-sm text-gray-500 dark:text-slate-400">Loading credential status…</p>
                              ) : credentialStatusDetail ? (
                                <div className="rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 text-sm dark:border-slate-700 dark:bg-slate-800/50">
                                  <div className="grid gap-3 sm:grid-cols-2">
                                    <DetailField label="Credential status">
                                      {credentialStatusDetail.credentialConfigured
                                        ? "Credential configured"
                                        : "Credential not configured"}
                                    </DetailField>
                                    <DetailField label="Auth mode">
                                      {humanizeIntegrationAuthMode(credentialStatusDetail.authMode)}
                                    </DetailField>
                                    <DetailField label="Last updated">
                                      {formatWhen(
                                        credentialStatusDetail.lastRotatedAtUtc ??
                                          credentialStatusDetail.lastConfiguredAtUtc ??
                                          null,
                                      )}
                                    </DetailField>
                                    <DetailField label="Last validated">
                                      {formatWhen(credentialStatusDetail.lastValidatedAtUtc)}
                                    </DetailField>
                                  </div>
                                  {credentialStatusDetail.configuredSecretFieldLabels.length > 0 ? (
                                    <p className="mt-3 text-xs text-gray-600 dark:text-slate-400">
                                      <span className="font-medium text-gray-800 dark:text-slate-200">Configured fields: </span>
                                      {credentialStatusDetail.configuredSecretFieldLabels.join(", ")}
                                    </p>
                                  ) : null}
                                  {credentialStatusDetail.credentialStatus.trim() &&
                                  credentialStatusDetail.credentialStatus !== "NotConfigured" &&
                                  credentialStatusDetail.credentialStatus !== "Configured" ? (
                                    <p className="mt-2 text-xs text-gray-600 dark:text-slate-400">
                                      {credentialStatusDetail.credentialStatus}
                                    </p>
                                  ) : null}
                                </div>
                              ) : null}
                              <div className="space-y-3">
                                {credentialSecretFields.map((field) => (
                                  <div key={field.key}>
                                    <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                                      {field.label}
                                      {field.required ? <span className="text-red-600 dark:text-red-400"> *</span> : null}
                                      <span className="ml-2 text-[10px] font-semibold uppercase tracking-wide text-amber-800 dark:text-amber-200/90">
                                        Secret
                                      </span>
                                    </label>
                                    {field.fieldType === "textarea" ? (
                                      <textarea
                                        className={configFieldClass}
                                        rows={3}
                                        autoComplete="off"
                                        placeholder="Enter new value. Existing secrets are never displayed."
                                        value={credentialDraft[field.key] ?? ""}
                                        onChange={(e) =>
                                          setCredentialDraft((d) => ({ ...d, [field.key]: e.target.value }))
                                        }
                                      />
                                    ) : (
                                      <input
                                        className={configFieldClass}
                                        type="password"
                                        autoComplete="off"
                                        placeholder="Enter new value. Existing secrets are never displayed."
                                        value={credentialDraft[field.key] ?? ""}
                                        onChange={(e) =>
                                          setCredentialDraft((d) => ({ ...d, [field.key]: e.target.value }))
                                        }
                                      />
                                    )}
                                    {field.helpText ? (
                                      <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">{field.helpText}</p>
                                    ) : null}
                                  </div>
                                ))}
                              </div>
                              <div className="flex flex-wrap justify-end gap-2">
                                {(credentialStatusDetail?.credentialConfigured ||
                                  selectedConnection?.credentialConfigured) ? (
                                  <ConfigSecondaryButton
                                    disabled={credentialClearLoading || credentialSaveLoading}
                                    onClick={() => void clearConnectionCredentials()}
                                  >
                                    {credentialClearLoading ? "Clearing…" : "Clear credential"}
                                  </ConfigSecondaryButton>
                                ) : null}
                                <ConfigPrimaryButton
                                  disabled={credentialSaveLoading || credentialClearLoading}
                                  onClick={() => void saveConnectionCredentials()}
                                >
                                  {credentialSaveLoading
                                    ? "Saving…"
                                    : (credentialStatusDetail?.credentialConfigured ??
                                        selectedConnection?.credentialConfigured)
                                      ? "Rotate credential"
                                      : "Configure credential"}
                                </ConfigPrimaryButton>
                              </div>
                            </>
                          )}
                        </>
                      ) : null}
                    </div>
                  </ConfigDetailCard>
                ) : null}
              </div>
            )}

            {tab === "sources" && (
              <div className="space-y-4">
                <p className="text-sm leading-snug text-gray-700 dark:text-slate-300">
                  {sourcesTabIntro(selectedConnection?.provider ?? null)}
                </p>
                <div className="flex flex-wrap justify-end gap-2">
                  <ConfigSecondaryButton onClick={() => selectedConnectionId && void loadSources(selectedConnectionId)} disabled={!selectedConnectionId || sourcesLoading}>
                    Refresh sources
                  </ConfigSecondaryButton>
                  <ConfigPrimaryButton onClick={openCreateSource} disabled={!selectedConnectionId}>
                    Add source
                  </ConfigPrimaryButton>
                </div>
                {sourcesError ? <p className="text-sm text-red-600 dark:text-red-400">{sourcesError}</p> : null}
                {!selectedConnectionId ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">Select a connection above to manage sources.</p>
                ) : sourcesLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading sources…</p>
                ) : sources.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    <p className="font-medium text-gray-800 dark:text-slate-200">No sources for this connection yet</p>
                    <p className="mx-auto mt-2 max-w-lg text-left">
                      {selectedConnection ? sourcesTabPlanningNote(selectedConnection.provider) : "Add a source when ready."}
                    </p>
                  </div>
                ) : (
                  <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                    <table className="min-w-[960px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Name</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Type</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Provider</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">External ID</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">URL</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Enabled</th>
                          <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">Created</th>
                          <th className="px-4 py-3 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {sources.map((s) => (
                          <tr key={s.id} className="bg-white dark:bg-slate-900">
                            <td className="px-4 py-3 font-medium text-gray-900 dark:text-slate-100">{s.name}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{humanizeExternalSourceType(s.sourceType)}</td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{s.provider}</td>
                            <td className="max-w-[140px] truncate px-4 py-3 text-gray-700 dark:text-slate-300" title={s.externalSourceId}>
                              {s.externalSourceId}
                            </td>
                            <td className="max-w-[160px] truncate px-4 py-3 text-cortex-blue dark:text-cortex-cyan">
                              {s.externalUrl ? (
                                <a href={s.externalUrl} target="_blank" rel="noreferrer" className="hover:underline">
                                  Link
                                </a>
                              ) : (
                                "—"
                              )}
                            </td>
                            <td className="px-4 py-3 text-gray-700 dark:text-slate-300">{s.isEnabled ? "Yes" : "No"}</td>
                            <td className="px-4 py-3 text-gray-600 dark:text-slate-400">{formatWhen(s.createdAtUtc)}</td>
                            <td className="space-x-2 whitespace-nowrap px-4 py-3 text-right">
                              <ConfigGhostButton className="!py-1.5" onClick={() => selectSourceForMapping(s.id)}>
                                Map fields
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => openEditSource(s)}>
                                Edit
                              </ConfigGhostButton>
                              <ConfigGhostButton className="!py-1.5" onClick={() => void toggleSourceEnabled(s)}>
                                {s.isEnabled ? "Disable" : "Enable"}
                              </ConfigGhostButton>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            )}

            {tab === "fields" && (
              <div className="space-y-4">
                <div>
                  <h3 className="text-base font-semibold text-gray-900 dark:text-slate-100">Field mapping profiles</h3>
                  <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
                    Map provider fields into Cortex concepts before external work becomes Cortex context.
                  </p>
                </div>

                <Callout title="Governance boundary">
                  Mappings help Cortex interpret external records. They do not directly change routing, owners, or
                  approvals. After mappings are saved, Cortex rules continue to evaluate canonical Cortex fields and
                  approved ticket creation flows only.
                </Callout>

                {!selectedSourceId ? (
                  <div className="space-y-2 text-sm text-gray-600 dark:text-slate-400">
                    <p>Select a source to view fields and mappings.</p>
                    {selectedConnection ? (
                      <p className="text-xs text-gray-500 dark:text-slate-500">{fieldMappingNoSourceHint(selectedConnection.provider)}</p>
                    ) : null}
                  </div>
                ) : (
                  <div className="rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-slate-700 dark:bg-slate-900">
                    {fieldLoading && !fieldsOverview ? (
                      <p className="text-sm text-gray-500 dark:text-slate-400">Loading source context…</p>
                    ) : fieldsOverview ? (
                      <div className="space-y-2 text-sm text-gray-800 dark:text-slate-200">
                        <p>
                          <span className="font-medium text-gray-900 dark:text-slate-100">Provider: </span>
                          {connectionProviderLabel(providerDefinitions, fieldsOverview.provider)} ({fieldsOverview.provider})
                        </p>
                        <p>
                          <span className="font-medium text-gray-900 dark:text-slate-100">Connection: </span>
                          {fieldsOverview.connectionDisplayName?.trim() || "—"}
                        </p>
                        <p>
                          <span className="font-medium text-gray-900 dark:text-slate-100">Source: </span>
                          {fieldsOverview.sourceName} · {humanizeExternalSourceType(fieldsOverview.sourceType)}
                        </p>
                        <p>
                          <span className="font-medium text-gray-900 dark:text-slate-100">Field discovery: </span>
                          {fieldsOverview.discoveryStatusMessage}
                        </p>
                        <p className="text-gray-700 dark:text-slate-300">
                          <span className="font-medium text-gray-900 dark:text-slate-100">Mapped fields: </span>
                          {fieldsOverview.mappedFieldCount}
                          {fieldsOverview.discoveryMode === "LiveSharePointList" ? (
                            <>
                              <span className="mx-1">·</span>
                              <span className="font-medium text-gray-900 dark:text-slate-100">Discovered this session: </span>
                              {discoveredFields.length}
                            </>
                          ) : null}
                          {fieldsOverview.planningFieldCount > 0 ? (
                            <>
                              <span className="mx-1">·</span>
                              <span className="font-medium text-gray-900 dark:text-slate-100">Planning reference fields: </span>
                              {fieldsOverview.planningFieldCount}
                            </>
                          ) : null}
                        </p>
                        {fieldMappingNextAction(fieldsOverview, discoveredFields.length) ? (
                          <p className="text-xs text-gray-600 dark:text-slate-400">
                            <span className="font-medium text-gray-800 dark:text-slate-200">Next: </span>
                            {fieldMappingNextAction(fieldsOverview, discoveredFields.length)}
                          </p>
                        ) : null}
                      </div>
                    ) : null}
                  </div>
                )}

                {fieldsOverview ? (
                  <div className="rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/40">
                    <p className="text-sm font-medium text-gray-900 dark:text-slate-100">Provider mapping guidance</p>
                    <ul className="mt-2 list-inside list-disc space-y-1 text-sm text-gray-700 dark:text-slate-300">
                      {providerFieldGuidanceBullets(fieldsOverview.provider).map((line) => (
                        <li key={line}>{line}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}

                {fieldsOverview &&
                fieldsOverview.discoveryMode === "PlanningStatic" &&
                fieldsOverview.planningFields.length > 0 ? (
                  <div className="min-w-0 max-w-full space-y-2 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
                    <h4 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Common fields (planning)</h4>
                    <p className="text-sm text-gray-600 dark:text-slate-400">
                      These rows are advisory for setup planning. They do not represent live discovered fields for this
                      provider.
                    </p>
                    <div className="max-w-full overflow-x-auto rounded-lg border border-gray-100 dark:border-slate-800">
                      <table className="min-w-[960px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">External field</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Field key</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Data type</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Indicators</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Recommended Cortex field</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Mapping note</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {fieldsOverview.planningFields.map((p) => {
                            const mapped = findPlanningMappingKey(p.fieldKey, fieldsOverview.currentMappings);
                            return (
                              <tr key={p.fieldKey} className="bg-white dark:bg-slate-900">
                                <td className="px-3 py-2 text-gray-900 dark:text-slate-100">{p.displayName}</td>
                                <td className="px-3 py-2 font-mono text-xs text-gray-700 dark:text-slate-300">{p.fieldKey}</td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{p.dataType}</td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">
                                  <div className="flex flex-wrap gap-1">
                                    {p.isRequired ? <MappingChip tone="amber">Required</MappingChip> : null}
                                    {p.isCustom ? <MappingChip tone="sky">Custom</MappingChip> : null}
                                    {p.recommendedCortexField ? <MappingChip tone="green">Recommended</MappingChip> : null}
                                    {mapped ? (
                                      <MappingChip tone="neutral">Mapped</MappingChip>
                                    ) : (
                                      <MappingChip tone="neutral">Unmapped</MappingChip>
                                    )}
                                  </div>
                                </td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">
                                  {p.recommendedCortexField
                                    ? humanizeCortexFieldDisplay(p.recommendedCortexField)
                                    : "—"}
                                  {p.confidenceLabel ? (
                                    <span className="ml-2 text-xs text-gray-500 dark:text-slate-500">
                                      ({p.confidenceLabel})
                                    </span>
                                  ) : null}
                                </td>
                                <td className="max-w-[280px] px-3 py-2 text-xs text-gray-600 dark:text-slate-400">
                                  {p.recommendationReason ?? "—"}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : null}

                <div className="flex min-w-0 max-w-full flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/40">
                  <div className="flex flex-wrap items-center gap-3">
                    <ConfigPrimaryButton
                      onClick={() => void discoverFieldsFromSource()}
                      disabled={discoverActionDisabled}
                    >
                      {discoverLoading ? "Discovering…" : "Discover fields"}
                    </ConfigPrimaryButton>
                    <p className="min-w-0 flex-1 text-sm text-gray-600 dark:text-slate-400">
                      For SharePoint lists, read list columns and surface advisory Cortex targets. Other providers show
                      planning guidance until live discovery is enabled.
                    </p>
                  </div>
                  {!selectedSourceId ? (
                    <p className="text-xs text-gray-500 dark:text-slate-500">Select an external source above to enable discovery.</p>
                  ) : readinessLoading ? (
                    <p className="text-xs text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                  ) : readinessError ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">{readinessError}</p>
                  ) : primaryReadinessHint(sourceReadiness, "discover") ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">
                      {primaryReadinessHint(sourceReadiness, "discover")}
                    </p>
                  ) : null}
                </div>

                <Callout title="Saving mapping profiles">
                  Saving replaces the full mapping list for this source. Use mapping notes to document how external values
                  should be interpreted after they are mapped.
                  <span className="mt-2 block text-sky-900/90 dark:text-sky-200/90">
                    Mapping notes are optional guidance for admins and do not bypass Cortex governance.
                  </span>
                </Callout>
                {discoverError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{discoverError}</p>
                ) : null}
                {discoveredFields.length > 0 ? (
                  <div className="min-w-0 max-w-full space-y-2 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-900">
                    <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">Discovered source fields</h3>
                    <p className="text-sm text-gray-600 dark:text-slate-400">
                      These fields were read from the external source. Review the suggestions before adding them to the
                      mapping table.
                    </p>
                    <div className="max-w-full overflow-x-auto rounded-lg border border-gray-100 dark:border-slate-800">
                      <table className="min-w-[900px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">External field</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Field key</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Data type</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Indicators</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Recommended Cortex field</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Advisory note</th>
                            <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Action</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {discoveredFields.map((f) => {
                            const label = f.displayName?.trim() || f.externalFieldName;
                            const keyDisp = f.externalFieldKey?.trim() || f.externalFieldName || "—";
                            const already = fieldDraft.some(
                              (row) =>
                                mappingRowIdentity(row.externalFieldName, row.externalFieldKey)
                                === mappingRowIdentity(f.externalFieldName, f.externalFieldKey),
                            );
                            const confLabel = f.confidenceLabel?.trim() ?? "";
                            const confTone: "sky" | "green" | "amber" | "neutral" =
                              confLabel === "Strong"
                                ? "sky"
                                : confLabel === "Suggested"
                                  ? "green"
                                  : confLabel === "Possible"
                                    ? "amber"
                                    : "neutral";
                            return (
                              <tr key={`${f.externalFieldName}:${f.externalFieldKey ?? ""}`} className="bg-white dark:bg-slate-900">
                                <td className="px-3 py-2 text-gray-900 dark:text-slate-100" title={label}>
                                  {label}
                                  {f.isHidden ? (
                                    <span className="ml-2 text-xs text-gray-500 dark:text-slate-500">(hidden)</span>
                                  ) : null}
                                </td>
                                <td className="max-w-[180px] truncate px-3 py-2 font-mono text-xs text-gray-700 dark:text-slate-300" title={keyDisp}>
                                  {keyDisp}
                                </td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{f.type?.trim() || "—"}</td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">
                                  <div className="flex flex-wrap gap-1">
                                    {f.isReadOnly ? <MappingChip tone="neutral">Read-only</MappingChip> : null}
                                    {f.isRequired ? <MappingChip tone="amber">Required</MappingChip> : null}
                                    {f.isCustom ? <MappingChip tone="sky">Custom</MappingChip> : null}
                                    {f.suggestedCortexField ? <MappingChip tone="green">Recommended</MappingChip> : null}
                                    {f.confidenceLabel?.trim() ? (
                                      <MappingChip tone={confTone}>{f.confidenceLabel}</MappingChip>
                                    ) : null}
                                    {already ? <MappingChip tone="neutral">Mapped</MappingChip> : null}
                                  </div>
                                </td>
                                <td className="px-3 py-2 text-gray-700 dark:text-slate-300">
                                  {f.suggestedCortexField
                                    ? humanizeCortexFieldDisplay(f.suggestedCortexField)
                                    : "—"}
                                </td>
                                <td className="max-w-[240px] px-3 py-2 text-xs text-gray-600 dark:text-slate-400">
                                  {f.recommendationReason?.trim() || "—"}
                                </td>
                                <td className="px-3 py-2 text-right">
                                  {already ? (
                                    <span className="text-xs text-gray-500 dark:text-slate-500">In mapping table</span>
                                  ) : (
                                    <ConfigGhostButton className="!py-1" onClick={() => addDiscoveredFieldToMapping(f)}>
                                      Add to mapping
                                    </ConfigGhostButton>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ) : null}
                {!selectedSourceId ? (
                  <div className="space-y-1 text-sm text-gray-600 dark:text-slate-400">
                    <p>Select an external source above.</p>
                    {selectedConnection ? (
                      <p className="text-xs text-gray-500 dark:text-slate-500">
                        {fieldMappingNoSourceHint(selectedConnection.provider)}
                      </p>
                    ) : null}
                  </div>
                ) : fieldError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{fieldError}</p>
                ) : fieldLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading mappings…</p>
                ) : (
                  <>
                    <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                      <table className="min-w-[1200px] w-max max-w-none divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="min-w-[220px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">External field</th>
                            <th className="min-w-[220px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Field key</th>
                            <th className="min-w-[190px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Cortex field</th>
                            <th className="w-[90px] min-w-[90px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Required</th>
                            <th className="min-w-[280px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Mapping note</th>
                            <th className="w-[100px] min-w-[100px] px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {fieldDraft.map((row, idx) => (
                            <tr key={idx} className="bg-white dark:bg-slate-900">
                              <td className="min-w-[220px] px-3 py-2 align-top">
                                <input
                                  className={configFieldClass}
                                  value={row.externalFieldName}
                                  title={row.externalFieldName || undefined}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, externalFieldName: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="min-w-[220px] px-3 py-2 align-top">
                                <input
                                  className={configFieldClass}
                                  value={row.externalFieldKey ?? ""}
                                  title={row.externalFieldKey?.trim() ? row.externalFieldKey : undefined}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, externalFieldKey: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="min-w-[190px] px-3 py-2 align-top">
                                <select
                                  className={`${configFieldClass} min-w-[190px]`}
                                  value={row.cortexField}
                                  title={humanizeCortexFieldDisplay(row.cortexField)}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, cortexField: e.target.value as CortexField };
                                    setFieldDraft(next);
                                  }}
                                >
                                  {CORTEX_FIELDS.map((f) => (
                                    <option key={f} value={f}>
                                      {humanizeCortexFieldDisplay(f)}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="w-[90px] min-w-[90px] px-3 py-2 align-top">
                                <input
                                  type="checkbox"
                                  checked={row.isRequired}
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, isRequired: e.target.checked };
                                    setFieldDraft(next);
                                  }}
                                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                                />
                              </td>
                              <td className="min-w-[280px] px-3 py-2 align-top">
                                <input
                                  className={`${configFieldClass} min-w-[260px]`}
                                  value={row.transformHint ?? ""}
                                  title={row.transformHint ?? ""}
                                  placeholder="Example: Map P1 to Critical, P2 to High, P3 to Medium"
                                  onChange={(e) => {
                                    const next = [...fieldDraft];
                                    next[idx] = { ...row, transformHint: e.target.value };
                                    setFieldDraft(next);
                                  }}
                                />
                              </td>
                              <td className="w-[100px] min-w-[100px] whitespace-nowrap px-3 py-2 text-right align-top">
                                <ConfigGhostButton
                                  className="!py-1 text-red-600 dark:text-red-400"
                                  onClick={() => setFieldDraft(fieldDraft.filter((_, i) => i !== idx))}
                                >
                                  Remove
                                </ConfigGhostButton>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <ConfigSecondaryButton onClick={() => setFieldDraft([...fieldDraft, emptyFieldRow()])}>Add row</ConfigSecondaryButton>
                      <ConfigPrimaryButton onClick={() => void saveFieldMappings()} disabled={fieldSaving}>
                        {fieldSaving ? "Saving…" : "Save mapping"}
                      </ConfigPrimaryButton>
                    </div>
                  </>
                )}
              </div>
            )}

            {tab === "boards" && (
              <div className="space-y-4">
                <Callout title="Board mapping">
                  Choose how external work aligns with Cortex boards. Reference-only modes do not create Cortex tickets
                  automatically.
                </Callout>
                {!selectedSourceId ? (
                  <div className="space-y-1 text-sm text-gray-600 dark:text-slate-400">
                    <p>{boardsTabNoSourceHint(selectedConnection?.provider)}</p>
                  </div>
                ) : boardError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{boardError}</p>
                ) : boardLoading || ticketBoardLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading…</p>
                ) : ticketBoards.length === 0 ? (
                  <p className="text-sm text-gray-600 dark:text-slate-400">
                    No Cortex boards found. Define boards under Configuration → Boards first.
                  </p>
                ) : (
                  <>
                    <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-slate-700">
                      <table className="min-w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                        <thead className="bg-gray-50 dark:bg-slate-800/80">
                          <tr>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Cortex board</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Mapping mode</th>
                            <th className="px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Default</th>
                            <th className="px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300" />
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                          {boardDraft.map((row, idx) => (
                            <tr key={idx} className="bg-white dark:bg-slate-900">
                              <td className="px-3 py-2">
                                <select
                                  className={configFieldClass}
                                  value={row.boardId || ticketBoards[0]?.id}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = { ...row, boardId: Number(e.target.value) };
                                    setBoardDraft(next);
                                  }}
                                >
                                  {ticketBoards.filter((b) => b.isEnabled).map((b) => (
                                    <option key={b.id} value={b.id}>
                                      {b.name}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="px-3 py-2">
                                <select
                                  className={configFieldClass}
                                  value={row.mappingMode}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = {
                                      ...row,
                                      mappingMode: e.target.value as ExternalBoardMappingMode,
                                    };
                                    setBoardDraft(next);
                                  }}
                                >
                                  {BOARD_MAPPING_MODES.map((m) => (
                                    <option key={m} value={m}>
                                      {humanizeExternalBoardMappingMode(m)}
                                    </option>
                                  ))}
                                </select>
                              </td>
                              <td className="px-3 py-2">
                                <input
                                  type="checkbox"
                                  checked={row.isDefault}
                                  onChange={(e) => {
                                    const next = [...boardDraft];
                                    next[idx] = { ...row, isDefault: e.target.checked };
                                    setBoardDraft(next);
                                  }}
                                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                                />
                              </td>
                              <td className="px-3 py-2 text-right">
                                <ConfigGhostButton
                                  className="!py-1 text-red-600 dark:text-red-400"
                                  onClick={() => setBoardDraft(boardDraft.filter((_, i) => i !== idx))}
                                >
                                  Remove
                                </ConfigGhostButton>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <ConfigSecondaryButton
                        onClick={() => {
                          const b = ticketBoards.find((x) => x.isEnabled) ?? ticketBoards[0];
                          setBoardDraft([
                            ...boardDraft,
                            { ...emptyBoardRow(), boardId: b?.id ?? 0 },
                          ]);
                        }}
                        disabled={!ticketBoards.length}
                      >
                        Add row
                      </ConfigSecondaryButton>
                      <ConfigPrimaryButton onClick={() => void saveBoardMappings()} disabled={boardSaving}>
                        {boardSaving ? "Saving…" : "Save board mappings"}
                      </ConfigPrimaryButton>
                    </div>
                  </>
                )}
              </div>
            )}

            {tab === "items" && (
              <div className="min-w-0 max-w-full space-y-4">
                <Callout title="External items">{externalItemsTabCalloutBody(itemsContextProvider)}</Callout>
                <div className="flex min-w-0 max-w-full flex-col gap-2 rounded-lg border border-gray-200 bg-gray-50/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-800/40">
                  <div className="flex flex-wrap items-center gap-3">
                    <ConfigPrimaryButton
                      onClick={() => void syncExternalSourceNow()}
                      disabled={syncActionDisabled}
                    >
                      {syncLoading ? "Syncing…" : "Sync now"}
                    </ConfigPrimaryButton>
                    <p className="min-w-0 flex-1 text-sm text-gray-600 dark:text-slate-400">
                      {externalItemsSyncStripBody(itemsContextProvider)}
                    </p>
                  </div>
                  {!selectedSourceId ? (
                    <p className="text-xs text-gray-500 dark:text-slate-500">Select an external source above to run a sync.</p>
                  ) : readinessLoading ? (
                    <p className="text-xs text-gray-600 dark:text-slate-400">Checking source readiness…</p>
                  ) : readinessError ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">{readinessError}</p>
                  ) : primaryReadinessHint(sourceReadiness, "sync") ? (
                    <p className="text-xs text-amber-800 dark:text-amber-200/90">
                      {primaryReadinessHint(sourceReadiness, "sync")}
                    </p>
                  ) : null}
                </div>
                {syncSummary?.kind === "success" ? (
                  <div className="rounded-lg border border-green-200 bg-green-50/90 px-4 py-3 text-sm text-green-950 dark:border-green-900 dark:bg-green-950/40 dark:text-green-100">
                    <p className="font-medium text-green-900 dark:text-green-100">Sync complete</p>
                    <ul className="mt-2 list-inside list-disc space-y-0.5 text-green-900/95 dark:text-green-100/95">
                      <li>Created: {syncSummary.data.createdCount}</li>
                      <li>Updated: {syncSummary.data.updatedCount}</li>
                      <li>Unchanged: {syncSummary.data.unchangedCount}</li>
                      <li>Skipped: {syncSummary.data.skippedCount}</li>
                      <li>Errors: {syncSummary.data.errorCount}</li>
                      <li>Items processed: {syncSummary.data.itemCount}</li>
                    </ul>
                    {syncSummary.data.message?.trim() ? (
                      <p className="mt-2 text-green-900/90 dark:text-green-100/90">{syncSummary.data.message.trim()}</p>
                    ) : null}
                  </div>
                ) : null}
                {syncSummary?.kind === "error" ? (
                  <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/40 dark:text-red-100">
                    <p className="font-medium">Sync failed</p>
                    <p className="mt-1">{syncSummary.message}</p>
                  </div>
                ) : null}

                <div className="rounded-lg border border-gray-200 bg-white/80 px-4 py-3 dark:border-slate-700 dark:bg-slate-900/50">
                  <p className="text-xs text-gray-600 dark:text-slate-400">
                    Creating a Cortex ticket is manual. Sync does not create tickets automatically.
                  </p>
                  <div className="mt-3 flex flex-wrap items-center gap-2">
                    {(
                      [
                        { id: "all" as const, label: "All", count: externalItemsQueueCounts.total },
                        {
                          id: "needsTicket" as const,
                          label: "Needs Cortex ticket",
                          count: externalItemsQueueCounts.needs,
                        },
                        {
                          id: "linked" as const,
                          label: "Linked to Cortex ticket",
                          count: externalItemsQueueCounts.linked,
                        },
                      ] as const
                    ).map((chip) => (
                      <button
                        key={chip.id}
                        type="button"
                        onClick={() => setItemsLinkFilter(chip.id)}
                        className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition-colors ${
                          itemsLinkFilter === chip.id
                            ? "border-cortex-blue bg-cortex-blue-soft text-cortex-ink dark:border-cortex-blue dark:bg-cortex-blue/20 dark:text-slate-100"
                            : "border-gray-200 bg-white text-gray-700 hover:bg-gray-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/80"
                        }`}
                      >
                        {chip.label}{" "}
                        <span className="font-medium opacity-75 tabular-nums">{chip.count}</span>
                      </button>
                    ))}
                  </div>
                  <div className="mt-3 flex min-w-0 flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">
                    <div className="min-w-0 flex-1 sm:max-w-md">
                      <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                        Search
                      </label>
                      <input
                        type="search"
                        placeholder="Search external items..."
                        value={itemsSearch}
                        onChange={(e) => setItemsSearch(e.target.value)}
                        className={configFieldClass}
                        autoComplete="off"
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                        Priority
                      </label>
                      <select
                        className={configFieldClass}
                        value={itemsPriorityFilter}
                        onChange={(e) => setItemsPriorityFilter(e.target.value)}
                      >
                        <option value="">All priorities</option>
                        {EXTERNAL_TICKET_PRIORITIES.map((p) => (
                          <option key={p} value={p}>
                            {p}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                        Status
                      </label>
                      <select
                        className={configFieldClass}
                        value={itemsStatusFilter}
                        onChange={(e) => setItemsStatusFilter(e.target.value)}
                        disabled={externalItemStatusOptions.length === 0}
                      >
                        <option value="">All statuses</option>
                        {externalItemStatusOptions.map((s) => (
                          <option key={s} value={s}>
                            {s}
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>
                </div>

                <div className="flex flex-wrap justify-end gap-2">
                  <ConfigSecondaryButton
                    onClick={() => selectedSourceId && void loadItems(selectedSourceId)}
                    disabled={!selectedSourceId || itemsLoading}
                  >
                    Refresh list
                  </ConfigSecondaryButton>
                  <ConfigPrimaryButton onClick={() => setUpsertOpen(true)} disabled={!selectedSourceId}>
                    Manual upsert test item
                  </ConfigPrimaryButton>
                </div>
                {!selectedSourceId ? (
                  <div className="space-y-1 text-sm text-gray-600 dark:text-slate-400">
                    <p>Select an external source above.</p>
                    {selectedConnection ? (
                      <p className="text-xs text-gray-500 dark:text-slate-500">
                        {fieldMappingNoSourceHint(selectedConnection.provider)}
                      </p>
                    ) : null}
                  </div>
                ) : itemsError ? (
                  <p className="text-sm text-red-600 dark:text-red-400">{itemsError}</p>
                ) : itemsLoading ? (
                  <p className="text-sm text-gray-500 dark:text-slate-400">Loading items…</p>
                ) : items.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    <p className="font-medium text-gray-800 dark:text-slate-200">No external items yet</p>
                    <p className="mx-auto mt-2 max-w-lg">
                      No external items have been captured for this source yet.{" "}
                      {externalItemsEmptySecondary(itemsContextProvider)}
                    </p>
                  </div>
                ) : filteredExternalItems.length === 0 ? (
                  <div className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                    {itemsSearch.trim() ? (
                      <p>No external items match this search.</p>
                    ) : itemsLinkFilter === "needsTicket" && externalItemsQueueCounts.needs === 0 ? (
                      <p>All external items for this source are already linked to Cortex tickets.</p>
                    ) : itemsLinkFilter === "linked" && externalItemsQueueCounts.linked === 0 ? (
                      <p>No external items have been promoted to Cortex tickets yet.</p>
                    ) : (
                      <p>No external items match the selected filters.</p>
                    )}
                  </div>
                ) : (
                  <div className="min-w-0 max-w-full overflow-hidden rounded-lg border border-gray-200 dark:border-slate-700">
                    <div className="w-full max-w-full overflow-x-auto overscroll-x-contain">
                      <table className="min-w-[1200px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                      <thead className="bg-gray-50 dark:bg-slate-800/80">
                        <tr>
                          <th className="min-w-[220px] max-w-[280px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Title</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Status</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Priority</th>
                          <th className="min-w-[100px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Requester</th>
                          <th className="min-w-[100px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Assigned</th>
                          <th className="min-w-[80px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Dept</th>
                          <th className="min-w-[80px] px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Category</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Due</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Modified</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Last seen</th>
                          <th className="min-w-[160px] whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Linked Cortex ticket</th>
                          <th className="whitespace-nowrap px-3 py-2 text-left font-medium text-gray-700 dark:text-slate-300">Link</th>
                          <th className="whitespace-nowrap px-3 py-2 text-right font-medium text-gray-700 dark:text-slate-300">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                        {filteredExternalItems.map((it) => (
                          <tr key={it.id} className="bg-white dark:bg-slate-900">
                            <td className="max-w-[280px] min-w-[220px] px-3 py-2 font-medium text-gray-900 dark:text-slate-100">
                              <span className="block truncate" title={it.title || undefined}>
                                {it.title}
                              </span>
                            </td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-700 dark:text-slate-300">{it.status ?? "—"}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-700 dark:text-slate-300">{it.priority ?? "—"}</td>
                            <td className="max-w-[160px] truncate px-3 py-2 text-gray-700 dark:text-slate-300" title={it.requester ?? undefined}>
                              {it.requester ?? "—"}
                            </td>
                            <td className="max-w-[160px] truncate px-3 py-2 text-gray-700 dark:text-slate-300" title={it.assignedTo ?? undefined}>
                              {it.assignedTo ?? "—"}
                            </td>
                            <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{it.department ?? "—"}</td>
                            <td className="px-3 py-2 text-gray-700 dark:text-slate-300">{it.category ?? "—"}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.dueDateUtc)}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.lastModifiedUtc)}</td>
                            <td className="whitespace-nowrap px-3 py-2 text-gray-600 dark:text-slate-400">{formatWhen(it.lastSeenUtc)}</td>
                            <td className="min-w-[160px] whitespace-nowrap px-3 py-2">
                              {it.cortexTicketId?.trim() ? (
                                onOpenCortexTicketById ? (
                                  <button
                                    type="button"
                                    onClick={() => void onOpenCortexTicketById(it.cortexTicketId!)}
                                    className="font-semibold text-cortex-blue hover:underline dark:text-cortex-cyan"
                                  >
                                    {formatLinkedTicketDisplay(it.cortexTicketId)}
                                  </button>
                                ) : (
                                  <span className="font-semibold text-gray-900 dark:text-slate-100">
                                    {formatLinkedTicketDisplay(it.cortexTicketId)}
                                  </span>
                                )
                              ) : (
                                <span className="text-gray-500 dark:text-slate-500">Not linked</span>
                              )}
                            </td>
                            <td className="whitespace-nowrap px-3 py-2">
                              {it.externalUrl ? (
                                <a href={it.externalUrl} target="_blank" rel="noreferrer" className="text-cortex-blue hover:underline dark:text-cortex-cyan">
                                  Open
                                </a>
                              ) : (
                                "—"
                              )}
                            </td>
                            <td className="whitespace-nowrap px-3 py-2 text-right">
                              <div className="flex flex-wrap justify-end gap-1">
                                <ConfigGhostButton
                                  className="!whitespace-nowrap !py-1.5"
                                  onClick={() => setItemDetail(it)}
                                >
                                  View details
                                </ConfigGhostButton>
                                {it.cortexTicketId?.trim() ? (
                                  onOpenCortexTicketById ? (
                                    <ConfigGhostButton
                                      className="!whitespace-nowrap !py-1.5"
                                      onClick={() => void onOpenCortexTicketById(it.cortexTicketId!)}
                                    >
                                      Open ticket
                                    </ConfigGhostButton>
                                  ) : null
                                ) : (
                                  <ConfigGhostButton
                                    className="!whitespace-nowrap !py-1.5"
                                    onClick={() => void startCreateCortexTicket(it)}
                                  >
                                    Create Cortex ticket
                                  </ConfigGhostButton>
                                )}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    </div>
                  </div>
                )}
              </div>
            )}

            {tab === "activity" && (
              <div className="min-w-0 max-w-full space-y-4">
                <Callout title="Activity">
                  Includes discovery, credential lifecycle, connection tests, sync, manual updates, and ticket-creation
                  attempts. External systems are not modified beyond the read actions you start from Integrations.
                </Callout>
                {!selectedConnectionId ? (
                  <div className="space-y-1 text-sm text-gray-600 dark:text-slate-400">
                    <p>Select a connection above.</p>
                  </div>
                ) : (
                  <>
                    <div className="flex flex-wrap justify-end gap-2">
                      <ConfigSecondaryButton
                        onClick={() => void loadActivity(selectedConnectionId)}
                        disabled={activityLoading}
                      >
                        Refresh activity
                      </ConfigSecondaryButton>
                    </div>
                    {activityError ? (
                      <p className="text-sm text-amber-800 dark:text-amber-200/90">{activityError}</p>
                    ) : null}
                    {activityLoading ? (
                      <p className="text-sm text-gray-500 dark:text-slate-400">Loading activity…</p>
                    ) : activityRows.length === 0 ? (
                      <div className="rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center text-sm text-gray-600 dark:border-slate-600 dark:text-slate-400">
                        <p className="font-medium text-gray-800 dark:text-slate-200">
                          No integration activity has been recorded for this connection yet.
                        </p>
                        <p className="mx-auto mt-2 max-w-lg text-gray-600 dark:text-slate-400">
                          <span className="block">{activityEmptySecondary(selectedConnection?.provider ?? null)}</span>
                          <span className="mt-2 block">
                            External systems are only touched by the explicit read actions you start from Integrations.
                          </span>
                        </p>
                      </div>
                    ) : (
                      <div className="min-w-0 max-w-full overflow-hidden rounded-lg border border-gray-200 dark:border-slate-700">
                        <div className="w-full max-w-full overflow-x-auto overscroll-x-contain">
                          <table className="min-w-[900px] w-full divide-y divide-gray-200 text-sm dark:divide-slate-700">
                            <thead className="bg-gray-50 dark:bg-slate-800/80">
                              <tr>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Activity
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Source
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Status
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Triggered by
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Started
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Duration
                                </th>
                                <th className="px-4 py-3 text-left font-medium text-gray-700 dark:text-slate-300">
                                  Result
                                </th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100 dark:divide-slate-800">
                              {activityRows.map((row) => (
                                <tr key={row.id} className="bg-white dark:bg-slate-900">
                                  <td className="px-4 py-3 text-gray-900 dark:text-slate-100">
                                    {humanizeIntegrationActivityType(row.activityType)}
                                  </td>
                                  <td className="whitespace-nowrap px-4 py-3 text-gray-600 dark:text-slate-400">
                                    {row.sourceId != null ? `#${row.sourceId}` : "—"}
                                  </td>
                                  <td className={`px-4 py-3 font-medium ${activityStatusRowClass(row.status)}`}>
                                    {humanizeIntegrationActivityStatus(row.status)}
                                  </td>
                                  <td className="px-4 py-3 text-gray-700 dark:text-slate-300">
                                    {row.triggeredByDisplayName?.trim() || "—"}
                                  </td>
                                  <td className="whitespace-nowrap px-4 py-3 text-gray-700 dark:text-slate-300">
                                    {formatWhen(row.startedAtUtc)}
                                  </td>
                                  <td className="whitespace-nowrap px-4 py-3 text-gray-700 dark:text-slate-300">
                                    {formatDurationMs(row.durationMs ?? undefined)}
                                  </td>
                                  <td className="min-w-[240px] px-4 py-3 text-gray-800 dark:text-slate-200">
                                    <span className="block">{integrationActivityResultSummary(row)}</span>
                                    {row.status === "Failed" && row.errorMessage?.trim() ? (
                                      <span className="mt-1 block text-xs text-red-700 dark:text-red-300/90">
                                        {row.errorMessage.trim()}
                                      </span>
                                    ) : null}
                                    {row.status === "Partial" && row.message?.trim() ? (
                                      <span className="mt-1 block text-xs text-amber-800 dark:text-amber-100/80">
                                        {row.message.trim()}
                                      </span>
                                    ) : null}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </div>
                    )}
                  </>
                )}
              </div>
            )}
          </div>
          </div>
        </ConfigPageBody>
      </ConfigPageShell>

      {connectionModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              Integration connection setup
            </h3>
            <p className="mt-1 text-xs text-gray-600 dark:text-slate-400">
              {connectionModal.mode === "create" ? "Register a new governed provider connection." : "Update this connection."}
            </p>
            {modalConnDef?.description ? (
              <p className="mt-2 text-sm text-gray-700 dark:text-slate-300">{modalConnDef.description}</p>
            ) : null}
            {!providerDefinitions ? (
              <p className="mt-2 text-xs text-amber-800 dark:text-amber-200/90">
                Loading provider setup definitions… If this message remains, verify your connection and try again.
              </p>
            ) : null}
            <div className="mt-4 space-y-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                  Display name<span className="text-red-600 dark:text-red-400"> *</span>
                </label>
                <input
                  className={configFieldClass}
                  value={connectionModal.draft.displayName}
                  onChange={(e) => {
                    const v = e.target.value;
                    setConnectionModal((m) => {
                      if (!m) return m;
                      if (m.mode === "create") {
                        return { mode: "create", draft: { ...m.draft, displayName: v } };
                      }
                      return { mode: "edit", id: m.id, draft: { ...m.draft, displayName: v } };
                    });
                  }}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                  Provider<span className="text-red-600 dark:text-red-400"> *</span>
                </label>
                <select
                  className={configFieldClass}
                  value={connectionModal.draft.provider}
                  onChange={(e) => {
                    const v = e.target.value as IntegrationProvider;
                    const nextDef = providerDefinitions?.find((p) => p.provider === v);
                    const authN = nextDef?.allowedAuthModes[0] ?? "Manual";
                    const syncN = nextDef?.allowedSyncModes[0] ?? "ReadOnly";
                    setConnectionModal((m) => {
                      if (!m || m.mode !== "create") {
                        return m;
                      }
                      return {
                        mode: "create" as const,
                        draft: {
                          ...m.draft,
                          provider: v,
                          providerSettings: {},
                          authMode: authN,
                          syncMode: syncN,
                        },
                      };
                    });
                  }}
                  disabled={connectionModal.mode === "edit"}
                >
                  {modalProviderChoices.map((p) => (
                    <option key={p.value} value={p.value}>
                      {p.label}
                    </option>
                  ))}
                </select>
                {connectionModal.mode === "edit" ? (
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">Provider cannot be changed after creation.</p>
                ) : (
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">
                    Changing the provider clears unsaved provider-specific values.
                  </p>
                )}
                <div className="mt-3 rounded-lg border border-gray-200 bg-slate-50/90 px-3 py-2.5 text-xs leading-relaxed text-gray-800 dark:border-slate-600 dark:bg-slate-800/50 dark:text-slate-200">
                  {integrationProviderMaturityMessage(connectionModal.draft.provider)}
                </div>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Auth mode</label>
                  <select
                    className={configFieldClass}
                    value={connectionModal.draft.authMode ?? "Manual"}
                    onChange={(e) => {
                      const v = e.target.value as IntegrationAuthMode;
                      setConnectionModal((m) => {
                        if (!m) return m;
                        if (m.mode === "create") {
                          return { mode: "create", draft: { ...m.draft, authMode: v } };
                        }
                        return { mode: "edit", id: m.id, draft: { ...m.draft, authMode: v } };
                      });
                    }}
                  >
                    {modalAuthChoices.map((a) => (
                      <option key={a} value={a}>
                        {humanizeIntegrationAuthMode(a)}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Sync mode</label>
                  <select
                    className={configFieldClass}
                    value={connectionModal.draft.syncMode ?? "ReadOnly"}
                    onChange={(e) => {
                      const v = e.target.value as IntegrationSyncMode;
                      setConnectionModal((m) => {
                        if (!m) return m;
                        if (m.mode === "create") {
                          return { mode: "create", draft: { ...m.draft, syncMode: v } };
                        }
                        return { mode: "edit", id: m.id, draft: { ...m.draft, syncMode: v } };
                      });
                    }}
                  >
                    {modalSyncChoices.map((s) => (
                      <option key={s} value={s}>
                        {humanizeIntegrationSyncMode(s)}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              {modalConnDef?.referenceMetadataOnly ? (
                <Callout title="Read-only reference">
                  This provider stores reference metadata only. It is not a live line-of-business connector.
                </Callout>
              ) : null}
              {(modalConnDef?.fields ?? []).some((f) => f.isSecret) ? (
                <Callout title="Credentials">
                  Create or update the connection with non-secret settings first, then configure secrets under{" "}
                  <strong>Connection credentials</strong> on this tab. Secrets are submitted through a dedicated endpoint and
                  are never displayed after saving.
                </Callout>
              ) : null}
              {(modalConnDef?.fields ?? [])
                .filter((field) => !field.isSecret)
                .map((field) => (
                <div key={field.key}>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">
                    {field.label}
                    {field.required ? <span className="text-red-600 dark:text-red-400"> *</span> : null}
                  </label>
                  {field.fieldType === "textarea" ? (
                    <textarea
                      className={configFieldClass}
                      rows={3}
                      placeholder={field.placeholder ?? ""}
                      value={connectionModal.draft.providerSettings[field.key] ?? ""}
                      onChange={(e) => updateConnectionDraftSettings(field.key, e.target.value)}
                    />
                  ) : field.fieldType === "select" && field.allowedValues?.length ? (
                    <select
                      className={configFieldClass}
                      value={connectionModal.draft.providerSettings[field.key] ?? ""}
                      onChange={(e) => updateConnectionDraftSettings(field.key, e.target.value)}
                    >
                      <option value="">—</option>
                      {field.allowedValues.map((av) => (
                        <option key={av} value={av}>
                          {av}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      className={configFieldClass}
                      type={field.fieldType === "url" ? "url" : "text"}
                      placeholder={field.placeholder ?? ""}
                      value={connectionModal.draft.providerSettings[field.key] ?? ""}
                      onChange={(e) => updateConnectionDraftSettings(field.key, e.target.value)}
                    />
                  )}
                  {field.helpText ? (
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">{field.helpText}</p>
                  ) : null}
                </div>
              ))}
              <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                <input
                  type="checkbox"
                  checked={connectionModal.draft.isEnabled ?? true}
                  onChange={(e) =>
                    setConnectionModal((m) => {
                      if (!m) return m;
                      if (m.mode === "create") {
                        return { mode: "create", draft: { ...m.draft, isEnabled: e.target.checked } };
                      }
                      return { mode: "edit", id: m.id, draft: { ...m.draft, isEnabled: e.target.checked } };
                    })
                  }
                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                />
                Enabled
              </label>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setConnectionModal(null)}>Cancel</ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void saveConnectionModal()}>Save</ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {sourceModal ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              {sourceModal.mode === "create" ? "Add external source" : "Edit external source"}
            </h3>
            <div className="mt-4 space-y-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Name</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.name}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, name: e.target.value } })
                  }
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Source type</label>
                <select
                  className={configFieldClass}
                  value={sourceModal.draft.sourceType}
                  onChange={(e) =>
                    setSourceModal({
                      ...sourceModal,
                      draft: { ...sourceModal.draft, sourceType: e.target.value as ExternalSourceType },
                    })
                  }
                >
                  {SOURCE_TYPES.map((t) => (
                    <option key={t} value={t}>
                      {humanizeExternalSourceType(t)}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External source ID</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.externalSourceId}
                  onChange={(e) =>
                    setSourceModal({
                      ...sourceModal,
                      draft: { ...sourceModal.draft, externalSourceId: e.target.value },
                    })
                  }
                />
                <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                  Identifier in the source system (for example list GUID, project key, or table name).
                </p>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">External URL</label>
                <input
                  className={configFieldClass}
                  value={sourceModal.draft.externalUrl ?? ""}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, externalUrl: e.target.value } })
                  }
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Provider</label>
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm text-gray-900 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100">
                  {sourceModal.draft.provider}
                </div>
                <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                  Inherited from the connection to this source system. It cannot be changed here.
                </p>
              </div>
              <label className="flex items-center gap-2 text-sm text-gray-800 dark:text-slate-200">
                <input
                  type="checkbox"
                  checked={sourceModal.draft.isEnabled ?? true}
                  onChange={(e) =>
                    setSourceModal({ ...sourceModal, draft: { ...sourceModal.draft, isEnabled: e.target.checked } })
                  }
                  className="h-4 w-4 rounded border-gray-300 dark:border-slate-600"
                />
                Enabled
              </label>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setSourceModal(null)}>Cancel</ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void saveSourceModal()}>Save</ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {itemDetail ? (
        <div
          className="fixed inset-0 z-[60] flex justify-end bg-black/40"
          onClick={() => setItemDetail(null)}
          role="presentation"
        >
          <div
            className="flex h-full w-full max-w-md flex-col border-l border-gray-200 bg-white shadow-xl dark:border-slate-700 dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-labelledby="external-item-detail-title"
          >
            <div className="flex shrink-0 items-start justify-between gap-3 border-b border-gray-100 px-5 py-4 dark:border-slate-800">
              <h3 id="external-item-detail-title" className="pr-2 text-lg font-semibold text-gray-900 dark:text-slate-100">
                External work item
              </h3>
              <ConfigSecondaryButton onClick={() => setItemDetail(null)}>Close</ConfigSecondaryButton>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-6">
              <div>
                <DetailField label="Title">
                  <span className="break-words">{itemDetail.title || "—"}</span>
                </DetailField>
                <DetailField label="Description">
                  {itemDetail.description
                    ? (
                        <p className="whitespace-pre-wrap break-words text-gray-800 dark:text-slate-200">
                          {itemDetail.description}
                        </p>
                      )
                    : "—"}
                </DetailField>
                <DetailField label="Status">{itemDetail.status ?? "—"}</DetailField>
                <DetailField label="Priority">{itemDetail.priority ?? "—"}</DetailField>
                <DetailField label="Requester">{itemDetail.requester ?? "—"}</DetailField>
                <DetailField label="Assigned">{itemDetail.assignedTo ?? "—"}</DetailField>
                <DetailField label="Department">{itemDetail.department ?? "—"}</DetailField>
                <DetailField label="Category">{itemDetail.category ?? "—"}</DetailField>
                <DetailField label="Due date">{formatWhen(itemDetail.dueDateUtc)}</DetailField>
                <DetailField label="Last modified">{formatWhen(itemDetail.lastModifiedUtc)}</DetailField>
                <DetailField label="Last seen">{formatWhen(itemDetail.lastSeenUtc)}</DetailField>
                <DetailField label="External source">{itemDetail.sourceName}</DetailField>
                <DetailField label="External item ID">
                  <span className="break-all font-mono text-xs">{itemDetail.externalItemId}</span>
                </DetailField>
                <DetailField label="Source system">{itemDetail.provider}</DetailField>
                <DetailField label="External link">
                  {itemDetail.externalUrl ? (
                    <a
                      href={itemDetail.externalUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="break-all text-cortex-blue hover:underline dark:text-cortex-cyan"
                    >
                      {itemDetail.externalUrl}
                    </a>
                  ) : (
                    "—"
                  )}
                </DetailField>
                <div className="border-b border-gray-100 py-3 last:border-b-0 dark:border-slate-800">
                  <div className="text-xs font-medium uppercase tracking-wide text-gray-500 dark:text-slate-400">
                    Linked Cortex ticket
                  </div>
                  {itemDetail.cortexTicketId ? (
                    <div className="mt-2 space-y-2">
                      <p className="text-base font-semibold text-gray-900 dark:text-slate-100">
                        {formatLinkedTicketDisplay(itemDetail.cortexTicketId)}
                      </p>
                      {onOpenCortexTicketById ? (
                        <button
                          type="button"
                          onClick={() => void onOpenCortexTicketById(itemDetail.cortexTicketId!)}
                          className="text-sm font-medium text-cortex-blue hover:underline dark:text-cortex-cyan"
                        >
                          Open ticket
                        </button>
                      ) : null}
                      <p className="text-[11px] leading-snug text-gray-500 dark:text-slate-500">
                        Cortex ticket was created manually and linked to this external item. The external
                        source was not updated.
                      </p>
                    </div>
                  ) : (
                    <div className="mt-2 space-y-2">
                      <p className="text-sm text-gray-600 dark:text-slate-400">Not linked</p>
                      <ConfigPrimaryButton
                        className="!py-1.5"
                        onClick={() => void startCreateCortexTicket(itemDetail)}
                      >
                        Create Cortex ticket
                      </ConfigPrimaryButton>
                    </div>
                  )}
                </div>
              </div>
              <div className="mt-5 rounded-lg border border-sky-200 bg-sky-50/90 px-3 py-3 text-xs text-sky-900 dark:border-sky-800 dark:bg-sky-950/40 dark:text-sky-100">
                Cortex insight over external work items will appear here after live sync and analysis are enabled.
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {createTicketOpen && createTicketFor ? (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4">
          <div
            className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900"
            role="dialog"
            aria-modal="true"
            aria-labelledby="create-ticket-from-external-title"
          >
            <h3
              id="create-ticket-from-external-title"
              className="text-lg font-semibold text-gray-900 dark:text-slate-100"
            >
              Create Cortex ticket
            </h3>
            <p className="mt-2 text-sm text-gray-600 dark:text-slate-400">
              Creating a Cortex ticket does not update the external source. The ticket will follow the normal Cortex
              approval process.
            </p>
            <div className="mt-4 rounded-lg border border-gray-200 bg-gray-50/90 px-3 py-3 text-xs text-gray-800 dark:border-slate-700 dark:bg-slate-800/60 dark:text-slate-200">
              <p className="font-medium text-gray-900 dark:text-slate-100">External source context</p>
              <ul className="mt-2 list-inside list-disc space-y-0.5">
                <li>
                  Source: {createTicketFor.sourceName} ({createTicketFor.provider})
                </li>
                <li className="font-mono">External item ID: {createTicketFor.externalItemId}</li>
                <li>
                  {createTicketFor.externalUrl ? (
                    <a
                      href={createTicketFor.externalUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="text-cortex-blue hover:underline dark:text-cortex-cyan"
                    >
                      External link
                    </a>
                  ) : (
                    "No external link on this item"
                  )}
                </li>
              </ul>
            </div>
            <div className="mt-4 grid gap-3">
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Title</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.title}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, title: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Description</label>
                <textarea
                  className={`${configFieldClass} min-h-[100px]`}
                  value={createTicketDraft.description}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, description: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Board</label>
                <select
                  className={configFieldClass}
                  value={createTicketDraft.boardId === "" ? "" : String(createTicketDraft.boardId)}
                  onChange={(e) => {
                    const v = e.target.value;
                    setCreateTicketDraft({
                      ...createTicketDraft,
                      boardId: v ? Number(v) : "",
                    });
                  }}
                  disabled={ticketBoardLoading || ticketBoards.filter((b) => b.isEnabled).length === 0}
                >
                  <option value="">— Select board —</option>
                  {ticketBoards
                    .filter((b) => b.isEnabled)
                    .map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name}
                      </option>
                    ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Priority</label>
                <select
                  className={configFieldClass}
                  value={createTicketDraft.priority}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, priority: e.target.value })}
                >
                  {EXTERNAL_TICKET_PRIORITIES.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Due date</label>
                <input
                  className={configFieldClass}
                  type="datetime-local"
                  value={createTicketDraft.dueDateUtc}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, dueDateUtc: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Department</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.department}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, department: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Category</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.category}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, category: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Requester</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.requester}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, requester: e.target.value })}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Assigned to</label>
                <input
                  className={configFieldClass}
                  value={createTicketDraft.assignedTo}
                  onChange={(e) => setCreateTicketDraft({ ...createTicketDraft, assignedTo: e.target.value })}
                />
              </div>
            </div>
            {createTicketError ? (
              <p className="mt-3 text-sm text-red-600 dark:text-red-400">{createTicketError}</p>
            ) : null}
            <div className="mt-6 flex flex-wrap justify-end gap-2">
              <ConfigSecondaryButton
                onClick={() => {
                  setCreateTicketOpen(false);
                  setCreateTicketFor(null);
                  setCreateTicketError(null);
                }}
                disabled={createTicketSaving}
              >
                Cancel
              </ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void submitCreateCortexTicket()} disabled={createTicketSaving}>
                {createTicketSaving ? "Creating…" : "Create ticket"}
              </ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}

      {upsertOpen && selectedSourceId !== null ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-700 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">Manual upsert test item</h3>
            <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
              Source: <span className="font-medium">{selectedSource?.name ?? `#${selectedSourceId}`}</span>
            </p>
            <div className="mt-4 grid gap-3">
              {(
                [
                  ["externalItemId", "External item ID", "text"],
                  ["title", "Title", "text"],
                  ["externalUrl", "External URL", "text"],
                  ["description", "Description", "text"],
                  ["status", "Status", "text"],
                  ["priority", "Priority", "text"],
                  ["requester", "Requester", "text"],
                  ["assignedTo", "Assigned to", "text"],
                  ["department", "Department", "text"],
                  ["category", "Category", "text"],
                  ["dueDateUtc", "Due (local)", "datetime-local"],
                  ["lastModifiedUtc", "Last modified (local)", "datetime-local"],
                ] as const
              ).map(([key, label, type]) => (
                <div key={key}>
                  <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">{label}</label>
                  <input
                    className={configFieldClass}
                    type={type}
                    value={String(upsertDraft[key] ?? "")}
                    onChange={(e) =>
                      setUpsertDraft({ ...upsertDraft, [key]: e.target.value })
                    }
                  />
                </div>
              ))}
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600 dark:text-slate-400">Extra data (JSON)</label>
                <textarea
                  className={configFieldClass}
                  rows={4}
                  placeholder="Optional. If empty, a small JSON object is generated from the fields above."
                  value={upsertDraft.rawJson ?? ""}
                  onChange={(e) => setUpsertDraft({ ...upsertDraft, rawJson: e.target.value })}
                />
              </div>
            </div>
            <div className="mt-6 flex justify-end gap-2">
              <ConfigSecondaryButton onClick={() => setUpsertOpen(false)} disabled={upsertSaving}>
                Cancel
              </ConfigSecondaryButton>
              <ConfigPrimaryButton onClick={() => void submitUpsert()} disabled={upsertSaving}>
                {upsertSaving ? "Saving…" : "Save item"}
              </ConfigPrimaryButton>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
