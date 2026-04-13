import { ApiError } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

function getDownloadFileName(response: Response, fallbackFileName: string) {
  const contentDisposition = response.headers.get("content-disposition");
  if (!contentDisposition) {
    return fallbackFileName;
  }

  const fileNameMatch = contentDisposition.match(/filename="?([^"]+)"?/i);
  return fileNameMatch?.[1]?.trim() || fallbackFileName;
}

async function ensureSuccess(response: Response, fallbackMessage: string) {
  if (response.ok) {
    return;
  }

  try {
    const data = (await response.json()) as unknown;
    if (typeof data === "object" && data !== null) {
      const message = "message" in data ? data.message : undefined;
      if (typeof message === "string" && message.trim()) {
        throw new ApiError(message, response.status);
      }
    }
  } catch (error) {
    if (error instanceof ApiError) {
      throw error;
    }
  }

  throw new ApiError(fallbackMessage, response.status);
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
};
