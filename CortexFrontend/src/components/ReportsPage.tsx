import { useEffect, useRef, useState } from "react";
import type { Ticket } from "../types/ticket";
import type {
  CustomReportDefinition,
  CustomReportResult,
} from "../types/customReport";
import type { OnlineUser } from "../types/user";
import { ReportsSkeleton } from "./LoadingSkeletons";
import SlaLegend from "./SlaLegend";
import {
  formatSlaSummary,
  getSlaBadgeClass,
  getSlaDisplayLabel,
  mapBackendSlaStatusToDisplayLabel,
} from "../utils/ticketSla";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  humanizeEnumLabel,
} from "../utils/presentation";

type ReportSection = "sla" | "online-users" | "custom";

interface ReportsPageProps {
  tickets: Ticket[];
  onlineUsers: OnlineUser[];
  customReports: CustomReportDefinition[];
  customReportResult: CustomReportResult | null;
  loading: boolean;
  onlineUsersLoading: boolean;
  customReportLoading: boolean;
  error: string | null;
  onlineUsersError: string | null;
  customReportError: string | null;
  showSlaLegend: boolean;
  canViewOnlineUsers: boolean;
  canViewCustomReports: boolean;
  activeSection: ReportSection;
  onChangeSection: (section: ReportSection) => void;
  selectedCustomReportId: number | null;
  onSelectCustomReport: (id: number) => void;
  onToggleSlaLegend: () => void;
  onRefresh: () => void;
  onRefreshOnlineUsers: () => void;
  onRefreshCustomReport: () => void;
  onExportCsv: () => void;
  onExportGoogleSheets: () => void;
  onOpenTicket: (ticket: Ticket) => void;
}

const STATUS_ORDER = [
  "On Track",
  "At Risk",
  "Breached",
  "Met",
  "Resolved Late",
] as const;

const STATUS_DESCRIPTIONS: Record<(typeof STATUS_ORDER)[number], string> = {
  "On Track": "Open tickets comfortably inside their SLA window.",
  "At Risk": "Open tickets inside the warning window before breach.",
  Breached: "Open tickets past the SLA deadline (shown as Overdue in the UI).",
  Met: "Resolved or closed before the SLA deadline (shown as Resolved On Time).",
  "Resolved Late": "Resolved or closed after the SLA deadline.",
};

function formatPercentage(count: number, total: number) {
  if (total === 0) {
    return "0%";
  }

  return `${Math.round((count / total) * 100)}%`;
}

function getOwnerLabel(ticket: Ticket) {
  const synitiOwner = formatDisplayValue(ticket.synitiOwner);
  if (synitiOwner !== "—") {
    return synitiOwner;
  }

  return formatDisplayValue(ticket.businessOwner);
}

function sortByUrgency(tickets: Ticket[]) {
  return [...tickets].sort((leftTicket, rightTicket) => {
    if (leftTicket.slaStatus === "Breached" && rightTicket.slaStatus !== "Breached") {
      return -1;
    }

    if (leftTicket.slaStatus !== "Breached" && rightTicket.slaStatus === "Breached") {
      return 1;
    }

    return leftTicket.slaRemainingMinutes - rightTicket.slaRemainingMinutes;
  });
}

function RiskTable({
  title,
  description,
  tickets,
  emptyMessage,
  onOpenTicket,
}: {
  title: string;
  description: string;
  tickets: Ticket[];
  emptyMessage: string;
  onOpenTicket: (ticket: Ticket) => void;
}) {
  return (
    <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
        <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
          {title}
        </h3>
        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
          {description}
        </p>
      </div>

      {tickets.length === 0 ? (
        <div className="px-6 py-10 text-center text-gray-500 dark:text-slate-400">
          {emptyMessage}
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
              <tr>
                <th className="px-4 py-3 font-medium">Ticket</th>
                <th className="px-4 py-3 font-medium">Priority</th>
                <th className="px-4 py-3 font-medium">Owner</th>
                <th className="px-4 py-3 font-medium">SLA</th>
                <th className="px-4 py-3 font-medium">Due</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((ticket) => (
                <tr
                  key={ticket.id}
                  className="cursor-pointer border-t border-gray-100 text-gray-700 transition-colors hover:bg-gray-50 dark:border-slate-800 dark:text-slate-200 dark:hover:bg-slate-800/50"
                  onClick={() => onOpenTicket(ticket)}
                >
                  <td className="px-4 py-3 align-top">
                    <div>
                      <p className="font-medium text-gray-900 dark:text-slate-100">
                        {ticket.id}
                      </p>
                      <p className="max-w-xs truncate text-gray-500 dark:text-slate-400">
                        {ticket.title}
                      </p>
                    </div>
                  </td>
                  <td className="px-4 py-3 align-top">{ticket.priority}</td>
                  <td className="px-4 py-3 align-top">{getOwnerLabel(ticket)}</td>
                  <td className="px-4 py-3 align-top">
                    <div className="flex flex-col gap-1">
                      <span
                        className={`inline-flex w-fit rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(getSlaDisplayLabel(ticket))}`}
                      >
                        {getSlaDisplayLabel(ticket)}
                      </span>
                      <span className="text-xs text-gray-500 dark:text-slate-400">
                        {formatSlaSummary(ticket)}
                      </span>
                    </div>
                  </td>
                  <td className="px-4 py-3 align-top whitespace-nowrap">
                    {formatDisplayDateTime(ticket.slaTargetDate)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function OnlineUsersReport({
  users,
  error,
  onRefresh,
}: {
  users: OnlineUser[];
  error: string | null;
  onRefresh: () => void;
}) {
  const adminOrDeveloperCount = users.filter(
    (user) => user.role === "Admin" || user.role === "Developer",
  ).length;
  const departmentsRepresented = new Set(
    users
      .map((user) => formatDisplayValue(user.department))
      .filter((department): department is string => department !== "—"),
  ).size;

  return error ? (
    <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
      <p className="text-red-700 dark:text-red-300">{error}</p>
    </div>
  ) : (
    <>
      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Online Now
          </p>
          <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
            {users.length}
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Users active within the configured session window.
          </p>
        </div>

        <div className="rounded-lg border border-cortex-blue/20 bg-cortex-blue-soft p-5 dark:border-cortex-blue/30 dark:bg-cortex-blue/10">
          <p className="text-sm font-medium text-cortex-ink dark:text-cortex-cyan">
            Admin / Developer
          </p>
          <p className="mt-3 text-3xl font-semibold text-cortex-ink-dark dark:text-slate-100">
            {adminOrDeveloperCount}
          </p>
          <p className="mt-2 text-sm text-cortex-ink/80 dark:text-slate-300">
            Operational users who can access advanced reports and controls.
          </p>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Departments
          </p>
          <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
            {departmentsRepresented}
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Distinct departments represented by active users.
          </p>
        </div>

        <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
          <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
            Refresh
          </p>
          <button
            onClick={onRefresh}
            className="mt-3 rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
          >
            Refresh Online Users
          </button>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            Pull the latest active presence snapshot from the API.
          </p>
        </div>
      </section>

      <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
            Online Users
          </h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
            Presence is based on recent activity heartbeats within the configured
            inactivity timeout.
          </p>
        </div>

        {users.length === 0 ? (
          <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
            No users are currently online.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                <tr>
                  <th className="px-4 py-3 font-medium">Display Name</th>
                  <th className="px-4 py-3 font-medium">Role</th>
                  <th className="px-4 py-3 font-medium">Department</th>
                  <th className="px-4 py-3 font-medium">Email</th>
                  <th className="px-4 py-3 font-medium">Last Seen</th>
                  <th className="px-4 py-3 font-medium">Last Login</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr
                    key={user.id}
                    className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                  >
                    <td className="px-4 py-3 align-top">
                      <div>
                        <p className="font-medium text-gray-900 dark:text-slate-100">
                          {user.displayName}
                        </p>
                        {user.nickName && (
                          <p className="text-xs text-gray-500 dark:text-slate-400">
                            {user.nickName}
                          </p>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 align-top">{humanizeEnumLabel(user.role)}</td>
                    <td className="px-4 py-3 align-top">
                      {formatDisplayValue(user.department)}
                    </td>
                    <td className="px-4 py-3 align-top">{user.email}</td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastSeenDateUtc)}
                    </td>
                    <td className="px-4 py-3 align-top whitespace-nowrap">
                      {formatDisplayDateTime(user.lastLoginDate)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  );
}

export default function ReportsPage({
  tickets,
  onlineUsers,
  customReports,
  customReportResult,
  loading,
  onlineUsersLoading,
  customReportLoading,
  error,
  onlineUsersError,
  customReportError,
  showSlaLegend,
  canViewOnlineUsers,
  canViewCustomReports,
  activeSection,
  onChangeSection,
  selectedCustomReportId,
  onSelectCustomReport,
  onToggleSlaLegend,
  onRefresh,
  onRefreshOnlineUsers,
  onRefreshCustomReport,
  onExportCsv,
  onExportGoogleSheets,
  onOpenTicket,
}: ReportsPageProps) {
  const [isExportMenuOpen, setIsExportMenuOpen] = useState(false);
  const exportMenuRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!isExportMenuOpen) {
      return undefined;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!exportMenuRef.current?.contains(event.target as Node)) {
        setIsExportMenuOpen(false);
      }
    };

    window.addEventListener("mousedown", handlePointerDown);
    return () => window.removeEventListener("mousedown", handlePointerDown);
  }, [isExportMenuOpen]);

  if (
    (activeSection === "sla" && loading) ||
    (activeSection === "online-users" && onlineUsersLoading) ||
    (activeSection === "custom" && customReportLoading)
  ) {
    return <ReportsSkeleton />;
  }

  const totalTickets = tickets.length;
  const statusCounts = Object.fromEntries(
    STATUS_ORDER.map((status) => [
      status,
      tickets.filter((ticket) => ticket.slaStatus === status).length,
    ]),
  ) as Record<(typeof STATUS_ORDER)[number], number>;

  const inSlaCount = statusCounts["On Track"] + statusCounts.Met;
  const atRiskCount = statusCounts["At Risk"];
  const outsideSlaCount = statusCounts.Breached + statusCounts["Resolved Late"];

  const actionableTickets = sortByUrgency(
    tickets.filter(
      (ticket) => ticket.slaStatus === "At Risk" || ticket.slaStatus === "Breached",
    ),
  );
  const resolvedLateTickets = sortByUrgency(
    tickets.filter((ticket) => ticket.slaStatus === "Resolved Late"),
  );

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-6 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
                Reports
              </h2>
              <p className="text-sm text-gray-500 dark:text-slate-400">
                Review operational insights and role-specific reporting.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              {activeSection === "sla" && (
                <>
                  <div className="relative" ref={exportMenuRef}>
                    <button
                      onClick={() =>
                        setIsExportMenuOpen((currentValue) => !currentValue)
                      }
                      className="rounded-md bg-cortex-ink px-4 py-2 text-white transition-colors hover:bg-cortex-ink-dark"
                    >
                      Export Report
                    </button>

                    {isExportMenuOpen && (
                      <div className="absolute right-0 z-20 mt-2 w-56 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-700 dark:bg-slate-900">
                        <button
                          onClick={() => {
                            setIsExportMenuOpen(false);
                            onExportCsv();
                          }}
                          className="block w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:text-slate-100 dark:hover:bg-slate-800"
                        >
                          Export as CSV
                        </button>
                        <button
                          onClick={() => {
                            setIsExportMenuOpen(false);
                            onExportGoogleSheets();
                          }}
                          className="block w-full border-t border-gray-100 px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:border-slate-800 dark:text-slate-100 dark:hover:bg-slate-800"
                        >
                          Export for Google Sheets
                        </button>
                      </div>
                    )}
                  </div>
                  <button
                    onClick={onToggleSlaLegend}
                    className="rounded-md bg-gray-100 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                  >
                    {showSlaLegend ? "Hide SLA Legend" : "Show SLA Legend"}
                  </button>
                  <button
                    onClick={onRefresh}
                    className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                  >
                    Refresh
                  </button>
                </>
              )}

              {activeSection === "online-users" && (
                <button
                  onClick={onRefreshOnlineUsers}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Refresh
                </button>
              )}

              {activeSection === "custom" && (
                <button
                  onClick={onRefreshCustomReport}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Refresh
                </button>
              )}
            </div>
          </div>

          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => onChangeSection("sla")}
              className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                activeSection === "sla"
                  ? "bg-cortex-blue text-white"
                  : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
              }`}
            >
              SLA Report
            </button>

            {canViewOnlineUsers && (
              <button
                onClick={() => onChangeSection("online-users")}
                className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                  activeSection === "online-users"
                    ? "bg-cortex-blue text-white"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                Online Users
              </button>
            )}

            {canViewCustomReports &&
              customReports
                .filter((report) => report.isEnabled)
                .map((report) => (
                  <button
                    key={report.id}
                    onClick={() => {
                      onChangeSection("custom");
                      onSelectCustomReport(report.id);
                    }}
                    className={`rounded-full px-4 py-2 text-sm font-medium transition-colors ${
                      activeSection === "custom" && selectedCustomReportId === report.id
                        ? "bg-cortex-blue text-white"
                        : "bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                    }`}
                  >
                    {report.name}
                  </button>
                ))}
          </div>
        </div>
      </section>

      {activeSection === "sla" ? (
        <>
          {showSlaLegend && <SlaLegend />}

          {error ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">{error}</p>
            </div>
          ) : totalTickets === 0 ? (
            <section className="rounded-lg border border-gray-200 bg-white px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-400">
              No ticket data is available for reporting yet.
            </section>
          ) : (
            <>
              <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                <div className="rounded-lg border border-gray-200 bg-white p-5 dark:border-slate-800 dark:bg-slate-900">
                  <p className="text-sm font-medium text-gray-500 dark:text-slate-400">
                    Total Tickets
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-gray-900 dark:text-slate-100">
                    {totalTickets}
                  </p>
                  <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
                    Visible to your current role.
                  </p>
                </div>

                <div className="rounded-lg border border-green-200 bg-green-50 p-5 dark:border-green-900/40 dark:bg-green-950/20">
                  <p className="text-sm font-medium text-green-700 dark:text-green-300">
                    In SLA
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-green-900 dark:text-green-100">
                    {inSlaCount}
                  </p>
                  <p className="mt-2 text-sm text-green-700/80 dark:text-green-300/80">
                    On track or resolved within SLA.
                  </p>
                </div>

                <div className="rounded-lg border border-yellow-200 bg-yellow-50 p-5 dark:border-yellow-900/40 dark:bg-yellow-950/20">
                  <p className="text-sm font-medium text-yellow-800 dark:text-yellow-300">
                    At Risk
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-yellow-900 dark:text-yellow-100">
                    {atRiskCount}
                  </p>
                  <p className="mt-2 text-sm text-yellow-800/80 dark:text-yellow-300/80">
                    Tickets inside the warning window.
                  </p>
                </div>

                <div className="rounded-lg border border-red-200 bg-red-50 p-5 dark:border-red-900/40 dark:bg-red-950/20">
                  <p className="text-sm font-medium text-red-700 dark:text-red-300">
                    Outside SLA
                  </p>
                  <p className="mt-3 text-3xl font-semibold text-red-900 dark:text-red-100">
                    {outsideSlaCount}
                  </p>
                  <p className="mt-2 text-sm text-red-700/80 dark:text-red-300/80">
                    Overdue open tickets or resolved after the SLA deadline.
                  </p>
                </div>
              </section>

              <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
                <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
                    SLA Status Breakdown
                  </h3>
                  <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                    Exact SLA outcomes across the current ticket set.
                  </p>
                </div>

                <div className="overflow-x-auto">
                  <table className="min-w-full text-sm">
                    <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                      <tr>
                        <th className="px-4 py-3 font-medium">Status</th>
                        <th className="px-4 py-3 font-medium">Count</th>
                        <th className="px-4 py-3 font-medium">Share</th>
                        <th className="px-4 py-3 font-medium">Meaning</th>
                      </tr>
                    </thead>
                    <tbody>
                      {STATUS_ORDER.map((status) => (
                        <tr
                          key={status}
                          className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                        >
                          <td className="px-4 py-3 align-top">
                            <span
                              className={`inline-flex rounded-full px-2 py-1 text-xs font-medium ${getSlaBadgeClass(mapBackendSlaStatusToDisplayLabel(status))}`}
                            >
                              {mapBackendSlaStatusToDisplayLabel(status)}
                            </span>
                          </td>
                          <td className="px-4 py-3 align-top font-medium text-gray-900 dark:text-slate-100">
                            {statusCounts[status]}
                          </td>
                          <td className="px-4 py-3 align-top">
                            {formatPercentage(statusCounts[status], totalTickets)}
                          </td>
                          <td className="px-4 py-3 align-top text-gray-500 dark:text-slate-400">
                            {STATUS_DESCRIPTIONS[status]}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </section>

              <div className="grid gap-6 xl:grid-cols-2">
                <RiskTable
                  title="Attention Needed"
                  description="Open tickets that are nearing or already past their SLA target."
                  tickets={actionableTickets}
                  emptyMessage="No tickets currently need SLA attention."
                  onOpenTicket={onOpenTicket}
                />

                <RiskTable
                  title="Resolved Late"
                  description="Tickets that were completed after their SLA target."
                  tickets={resolvedLateTickets}
                  emptyMessage="No tickets have been resolved late."
                  onOpenTicket={onOpenTicket}
                />
              </div>
            </>
          )}
        </>
      ) : activeSection === "online-users" ? (
        <OnlineUsersReport
          users={onlineUsers}
          error={onlineUsersError}
          onRefresh={onRefreshOnlineUsers}
        />
      ) : null}

      {activeSection === "custom" && (
        <section className="rounded-lg border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900">
          <div className="border-b border-gray-100 px-6 py-4 dark:border-slate-800">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-slate-100">
              {customReportResult?.reportName ??
                customReports.find((report) => report.id === selectedCustomReportId)?.name ??
                "Custom Report"}
            </h3>
            <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
              Custom SQL report registered in Configuration.
            </p>
          </div>

          {customReportError ? (
            <div className="rounded border-l-4 border-red-500 bg-red-50 p-4 dark:bg-red-950/40">
              <p className="text-red-700 dark:text-red-300">{customReportError}</p>
            </div>
          ) : !customReportResult ? (
            <div className="px-6 py-12 text-center text-gray-500 dark:text-slate-400">
              Select a custom report to run it.
            </div>
          ) : (
            <div className="space-y-4 px-6 py-6">
              <div className="flex flex-col gap-2 text-sm text-gray-500 dark:text-slate-400">
                <span>Generated {formatDisplayDateTime(customReportResult.generatedDateUtc)}</span>
                {customReportResult.isTruncated && (
                  <span className="text-amber-700 dark:text-amber-300">
                    Showing the first 500 rows for performance.
                  </span>
                )}
              </div>

              {customReportResult.rows.length === 0 ? (
                <div className="rounded-lg border border-gray-200 bg-gray-50 px-6 py-12 text-center text-gray-500 dark:border-slate-800 dark:bg-slate-950/40 dark:text-slate-400">
                  This report returned no rows.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="min-w-full text-sm">
                    <thead className="bg-gray-50 text-left text-gray-600 dark:bg-slate-800/80 dark:text-slate-300">
                      <tr>
                        {customReportResult.columns.map((column) => (
                          <th key={column} className="px-4 py-3 font-medium">
                            {column}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {customReportResult.rows.map((row, rowIndex) => (
                        <tr
                          key={rowIndex}
                          className="border-t border-gray-100 text-gray-700 dark:border-slate-800 dark:text-slate-200"
                        >
                          {customReportResult.columns.map((column) => (
                            <td key={`${rowIndex}-${column}`} className="px-4 py-3 align-top">
                              {String(row[column] ?? "—")}
                            </td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </section>
      )}
    </div>
  );
}
