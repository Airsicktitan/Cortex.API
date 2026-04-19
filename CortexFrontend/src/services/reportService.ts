import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

export type AdminLogExportFormat = "csv" | "json" | "txt" | "xlsx" | "sheets";

function getDownloadFileName(response: Response, fallbackFileName: string) {
  const contentDisposition = response.headers.get("content-disposition");
  if (!contentDisposition) {
    return fallbackFileName;
  }

  const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/i);
  return fileNameMatch?.[1]?.trim() || fallbackFileName;
}

function triggerDownload(blob: Blob, fileName: string) {
  const downloadUrl = window.URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = downloadUrl;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  window.URL.revokeObjectURL(downloadUrl);
}

export const reportService = {
  async exportCsv(
    token: string,
    fallbackFileName = "cortex-report.csv",
  ): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/reports/export?format=csv`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to export report");

    const blob = await response.blob();
    const fileName = getDownloadFileName(response, fallbackFileName);
    triggerDownload(blob, fileName);
  },

  async exportAdminLogs(
    token: string,
    fromUtcIso: string,
    toUtcIso: string,
    format: AdminLogExportFormat,
  ): Promise<void> {
    const searchParams = new URLSearchParams({
      format,
      from: fromUtcIso,
      to: toUtcIso,
    });

    const fallbackFileNames: Record<AdminLogExportFormat, string> = {
      csv: "cortex-logs.csv",
      json: "cortex-logs.json",
      txt: "cortex-logs.txt",
      xlsx: "cortex-logs.xlsx",
      sheets: "cortex-logs-google-sheets.xlsx",
    };

    const response = await fetch(
      `${API_BASE_URL}/admin/logs/export?${searchParams.toString()}`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    await ensureSuccess(response, "Failed to export logs");

    const blob = await response.blob();
    const fileName = getDownloadFileName(
      response,
      fallbackFileNames[format],
    );
    triggerDownload(blob, fileName);
  },
};
