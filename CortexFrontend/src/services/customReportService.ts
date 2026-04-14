import type {
  CustomReportDefinition,
  CustomReportResult,
  DatabaseViewDefinition,
  UpsertCustomReportDefinitionInput,
} from "../types/customReport";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const customReportService = {
  /** Report definitions for management (Admin, Developer) — GET /api/settings/reports */
  async getAll(token: string): Promise<CustomReportDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/settings/reports`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load custom reports");
    return response.json();
  },

  /** Runnable report list for Business+ — GET /api/reports/custom */
  async listRunnable(token: string): Promise<CustomReportDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/reports/custom`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load custom reports");
    return response.json();
  },

  async create(
    definition: UpsertCustomReportDefinitionInput,
    token: string,
  ): Promise<CustomReportDefinition> {
    const response = await fetch(`${API_BASE_URL}/settings/reports`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to create custom report");
    return response.json();
  },

  async update(
    id: number,
    definition: UpsertCustomReportDefinitionInput,
    token: string,
  ): Promise<CustomReportDefinition> {
    const response = await fetch(`${API_BASE_URL}/settings/reports/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to update custom report");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/settings/reports/${id}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to delete custom report");
  },

  async run(id: number, token: string): Promise<CustomReportResult> {
    const response = await fetch(`${API_BASE_URL}/reports/custom/${id}`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to run custom report");
    return response.json();
  },

  async getAvailableViews(token: string): Promise<DatabaseViewDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/settings/reports/database-views`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load database views");
    return response.json();
  },
};
