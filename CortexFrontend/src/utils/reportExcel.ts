import type { Ticket } from "../types/ticket";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "./ownerIdentity";

type CellValue = string | number;

type Worksheet = {
  name: string;
  rows: CellValue[][];
};

type SlaStatusName =
  | "On Track"
  | "At Risk"
  | "Breached"
  | "Met"
  | "Resolved Late";

interface DownloadSlaReportWorkbookOptions {
  tickets: Ticket[];
  statusOrder: readonly SlaStatusName[];
  statusCounts: Record<SlaStatusName, number>;
  statusDescriptions: Record<SlaStatusName, string>;
  actionableTickets: Ticket[];
  resolvedLateTickets: Ticket[];
}

function escapeXml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

function sanitizeWorksheetName(name: string) {
  return name.replace(/[\\/*?:[\]]/g, " ").trim().slice(0, 31) || "Sheet";
}

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString() : "";
}

function formatPercentage(count: number, total: number) {
  if (total === 0) {
    return "0%";
  }

  return `${Math.round((count / total) * 100)}%`;
}

function getOwnerLabel(ticket: Ticket) {
  return (
    readOnlySynitiOwnerLabel(ticket) ||
    readOnlyBusinessOwnerLabel(ticket) ||
    "Unassigned"
  );
}

function buildCell(value: CellValue, isHeader: boolean) {
  const type = typeof value === "number" ? "Number" : "String";
  const styleId = isHeader ? ' ss:StyleID="Header"' : "";
  return `<Cell${styleId}><Data ss:Type="${type}">${escapeXml(
    String(value),
  )}</Data></Cell>`;
}

function buildWorksheet({ name, rows }: Worksheet) {
  const safeName = sanitizeWorksheetName(name);
  const xmlRows = rows
    .map((row, rowIndex) => {
      const cells = row.map((value) => buildCell(value, rowIndex === 0)).join("");
      return `<Row>${cells}</Row>`;
    })
    .join("");

  return `<Worksheet ss:Name="${escapeXml(
    safeName,
  )}"><Table>${xmlRows}</Table></Worksheet>`;
}

function buildWorkbook(sheets: Worksheet[]) {
  return `<?xml version="1.0"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:o="urn:schemas-microsoft-com:office:office"
 xmlns:x="urn:schemas-microsoft-com:office:excel"
 xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:html="http://www.w3.org/TR/REC-html40">
 <Styles>
  <Style ss:ID="Header">
   <Font ss:Bold="1"/>
   <Interior ss:Color="#DCE6F1" ss:Pattern="Solid"/>
  </Style>
 </Styles>
 ${sheets.map(buildWorksheet).join("")}
</Workbook>`;
}

function downloadFile(filename: string, content: string) {
  const blob = new Blob([content], { type: "application/vnd.ms-excel" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function createTicketRows(tickets: Ticket[]) {
  return tickets.map((ticket) => [
    ticket.id,
    ticket.title,
    ticket.status,
    ticket.priority,
    getOwnerLabel(ticket),
    ticket.slaStatus,
    ticket.createdByDisplayName ?? "",
    formatDateTime(ticket.createdDate),
    formatDateTime(ticket.lastModifiedDate ?? ticket.createdDate),
    formatDateTime(ticket.slaTargetDate),
  ]);
}

export function downloadSlaReportWorkbook({
  tickets,
  statusOrder,
  statusCounts,
  statusDescriptions,
  actionableTickets,
  resolvedLateTickets,
}: DownloadSlaReportWorkbookOptions) {
  const totalTickets = tickets.length;
  const inSlaCount = statusCounts["On Track"] + statusCounts.Met;
  const atRiskCount = statusCounts["At Risk"];
  const outsideSlaCount = statusCounts.Breached + statusCounts["Resolved Late"];
  const generatedAt = new Date();
  const generatedAtText = generatedAt.toLocaleString();

  const summarySheet: Worksheet = {
    name: "Summary",
    rows: [
      ["Metric", "Value", "Details"],
      ["Generated", generatedAtText, "Local browser time"],
      ["Total Tickets", totalTickets, "Visible to the current user"],
      ["In SLA", inSlaCount, "On track or resolved within SLA"],
      ["At Risk", atRiskCount, "Inside the warning window"],
      ["Outside SLA", outsideSlaCount, "Breached or resolved late"],
    ],
  };

  const breakdownSheet: Worksheet = {
    name: "SLA Breakdown",
    rows: [
      ["Status", "Count", "Share", "Meaning"],
      ...statusOrder.map((status) => [
        status,
        statusCounts[status],
        formatPercentage(statusCounts[status], totalTickets),
        statusDescriptions[status],
      ]),
    ],
  };

  const attentionRows = createTicketRows(actionableTickets);
  const resolvedLateRows = createTicketRows(resolvedLateTickets);
  const allTicketRows = createTicketRows(tickets);

  const sheets: Worksheet[] = [
    summarySheet,
    breakdownSheet,
    {
      name: "Attention Needed",
      rows: [
        [
          "Ticket",
          "Title",
          "Status",
          "Priority",
          "Owner",
          "SLA Status",
          "Requester",
          "Created",
          "Last Updated",
          "Due",
        ],
        ...(attentionRows.length > 0
          ? attentionRows
          : [["No tickets currently need SLA attention."]]),
      ],
    },
    {
      name: "Resolved Late",
      rows: [
        [
          "Ticket",
          "Title",
          "Status",
          "Priority",
          "Owner",
          "SLA Status",
          "Requester",
          "Created",
          "Last Updated",
          "Due",
        ],
        ...(resolvedLateRows.length > 0
          ? resolvedLateRows
          : [["No tickets have been resolved late."]]),
      ],
    },
    {
      name: "All Tickets",
      rows: [
        [
          "Ticket",
          "Title",
          "Status",
          "Priority",
          "Owner",
          "SLA Status",
          "Requester",
          "Created",
          "Last Updated",
          "Due",
        ],
        ...(allTicketRows.length > 0 ? allTicketRows : [["No tickets available."]]),
      ],
    },
  ];

  const workbook = buildWorkbook(sheets);
  const dateSuffix = generatedAt.toISOString().slice(0, 10);

  downloadFile(`cortex-sla-report-${dateSuffix}.xml`, workbook);
}
