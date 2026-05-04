import type {
  CreateSapFieldInput,
  CreateSapReferenceSourceInput,
  CreateSapTableInput,
  SapFieldMetadataResponse,
  SapReferenceSearchResultDto,
  SapReferenceSourceResponse,
  SapTableMetadataResponse,
} from "../types/sapReference";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const headers = (token: string, json = false): HeadersInit => ({
  ...(json ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

const base = `${API_BASE_URL}/sap-reference`;

export const sapReferenceService = {
  async listSources(token: string): Promise<SapReferenceSourceResponse[]> {
    const res = await fetch(`${base}/sources`, { headers: headers(token) });
    await ensureSuccess(res, "Unable to load SAP reference sources.");
    return res.json();
  },

  async createSource(token: string, body: CreateSapReferenceSourceInput): Promise<SapReferenceSourceResponse> {
    const res = await fetch(`${base}/sources`, {
      method: "POST",
      headers: headers(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(res, "Unable to create SAP reference source.");
    return res.json();
  },

  async setSourceEnabled(token: string, id: number, isEnabled: boolean): Promise<SapReferenceSourceResponse> {
    const res = await fetch(`${base}/sources/${id}/enabled`, {
      method: "PATCH",
      headers: headers(token, true),
      body: JSON.stringify({ isEnabled }),
    });
    await ensureSuccess(res, "Unable to update source.");
    return res.json();
  },

  async listTables(token: string, sourceId: number): Promise<SapTableMetadataResponse[]> {
    const res = await fetch(`${base}/sources/${sourceId}/tables`, { headers: headers(token) });
    await ensureSuccess(res, "Unable to load tables.");
    return res.json();
  },

  async createTable(token: string, sourceId: number, body: CreateSapTableInput): Promise<SapTableMetadataResponse> {
    const res = await fetch(`${base}/sources/${sourceId}/tables`, {
      method: "POST",
      headers: headers(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(res, "Unable to create table.");
    return res.json();
  },

  async listFields(token: string, tableId: number): Promise<SapFieldMetadataResponse[]> {
    const res = await fetch(`${base}/tables/${tableId}/fields`, { headers: headers(token) });
    await ensureSuccess(res, "Unable to load fields.");
    return res.json();
  },

  async createField(token: string, tableId: number, body: CreateSapFieldInput): Promise<SapFieldMetadataResponse> {
    const res = await fetch(`${base}/tables/${tableId}/fields`, {
      method: "POST",
      headers: headers(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(res, "Unable to create field.");
    return res.json();
  },

  async deleteSource(token: string, sourceId: number): Promise<void> {
    const res = await fetch(`${base}/sources/${sourceId}`, {
      method: "DELETE",
      headers: headers(token),
    });
    await ensureSuccess(res, "Unable to delete source.");
  },

  async deleteTable(token: string, tableId: number): Promise<void> {
    const res = await fetch(`${base}/tables/${tableId}`, {
      method: "DELETE",
      headers: headers(token),
    });
    await ensureSuccess(res, "Unable to delete table.");
  },

  async deleteField(token: string, fieldId: number): Promise<void> {
    const res = await fetch(`${base}/fields/${fieldId}`, {
      method: "DELETE",
      headers: headers(token),
    });
    await ensureSuccess(res, "Unable to delete field.");
  },

  async search(token: string, query: string): Promise<SapReferenceSearchResultDto[]> {
    const params = new URLSearchParams({ query: query.trim() });
    const res = await fetch(`${base}/search?${params}`, { headers: headers(token) });
    await ensureSuccess(res, "Unable to search SAP reference.");
    return res.json();
  },
};
