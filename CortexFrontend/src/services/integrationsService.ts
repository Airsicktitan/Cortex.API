import type {
  CreateExternalWorkSourceInput,
  CreateIntegrationConnectionInput,
  ExternalBoardMappingItemInput,
  ExternalBoardMappingResponse,
  ExternalFieldMappingItemInput,
  ExternalFieldMappingResponse,
  ExternalWorkItemResponse,
  ExternalWorkSourceResponse,
  IntegrationConnectionResponse,
  ManualUpsertExternalWorkItemInput,
  UpdateExternalWorkSourceInput,
  UpdateIntegrationConnectionInput,
} from "../types/integrations";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, json = false): HeadersInit => ({
  ...(json ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

const integrationsBase = `${API_BASE_URL}/integrations`;

export const integrationsService = {
  async listConnections(token: string): Promise<IntegrationConnectionResponse[]> {
    const response = await fetch(`${integrationsBase}/connections`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load integration connections");
    return response.json();
  },

  async getConnection(
    token: string,
    id: number,
  ): Promise<IntegrationConnectionResponse> {
    const response = await fetch(`${integrationsBase}/connections/${id}`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load integration connection");
    return response.json();
  },

  async createConnection(
    token: string,
    body: CreateIntegrationConnectionInput,
  ): Promise<IntegrationConnectionResponse> {
    const response = await fetch(`${integrationsBase}/connections`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(response, "Unable to create integration connection");
    return response.json();
  },

  async updateConnection(
    token: string,
    id: number,
    body: UpdateIntegrationConnectionInput,
  ): Promise<IntegrationConnectionResponse> {
    const response = await fetch(`${integrationsBase}/connections/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(response, "Unable to update integration connection");
    return response.json();
  },

  async setConnectionEnabled(
    token: string,
    id: number,
    isEnabled: boolean,
  ): Promise<IntegrationConnectionResponse> {
    const response = await fetch(`${integrationsBase}/connections/${id}/enabled`, {
      method: "PATCH",
      headers: authHeaders(token, true),
      body: JSON.stringify({ isEnabled }),
    });
    await ensureSuccess(response, "Unable to update connection status");
    return response.json();
  },

  async listSources(
    token: string,
    connectionId: number,
  ): Promise<ExternalWorkSourceResponse[]> {
    const response = await fetch(
      `${integrationsBase}/connections/${connectionId}/sources`,
      { headers: authHeaders(token) },
    );
    await ensureSuccess(response, "Unable to load external work sources");
    return response.json();
  },

  async createSource(
    token: string,
    connectionId: number,
    body: CreateExternalWorkSourceInput,
  ): Promise<ExternalWorkSourceResponse> {
    const response = await fetch(
      `${integrationsBase}/connections/${connectionId}/sources`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: JSON.stringify(body),
      },
    );
    await ensureSuccess(response, "Unable to create external work source");
    return response.json();
  },

  async updateSource(
    token: string,
    sourceId: number,
    body: UpdateExternalWorkSourceInput,
  ): Promise<ExternalWorkSourceResponse> {
    const response = await fetch(`${integrationsBase}/sources/${sourceId}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(body),
    });
    await ensureSuccess(response, "Unable to update external work source");
    return response.json();
  },

  async setSourceEnabled(
    token: string,
    sourceId: number,
    isEnabled: boolean,
  ): Promise<ExternalWorkSourceResponse> {
    const response = await fetch(`${integrationsBase}/sources/${sourceId}/enabled`, {
      method: "PATCH",
      headers: authHeaders(token, true),
      body: JSON.stringify({ isEnabled }),
    });
    await ensureSuccess(response, "Unable to update source status");
    return response.json();
  },

  async getFieldMappings(
    token: string,
    sourceId: number,
  ): Promise<ExternalFieldMappingResponse[]> {
    const response = await fetch(
      `${integrationsBase}/sources/${sourceId}/field-mappings`,
      { headers: authHeaders(token) },
    );
    await ensureSuccess(response, "Unable to load field mappings");
    return response.json();
  },

  async replaceFieldMappings(
    token: string,
    sourceId: number,
    mappings: ExternalFieldMappingItemInput[],
  ): Promise<ExternalFieldMappingResponse[]> {
    const response = await fetch(
      `${integrationsBase}/sources/${sourceId}/field-mappings`,
      {
        method: "PUT",
        headers: authHeaders(token, true),
        body: JSON.stringify(mappings),
      },
    );
    await ensureSuccess(response, "Unable to save field mappings");
    return response.json();
  },

  async getBoardMappings(
    token: string,
    sourceId: number,
  ): Promise<ExternalBoardMappingResponse[]> {
    const response = await fetch(
      `${integrationsBase}/sources/${sourceId}/board-mappings`,
      { headers: authHeaders(token) },
    );
    await ensureSuccess(response, "Unable to load board mappings");
    return response.json();
  },

  async replaceBoardMappings(
    token: string,
    sourceId: number,
    mappings: ExternalBoardMappingItemInput[],
  ): Promise<ExternalBoardMappingResponse[]> {
    const response = await fetch(
      `${integrationsBase}/sources/${sourceId}/board-mappings`,
      {
        method: "PUT",
        headers: authHeaders(token, true),
        body: JSON.stringify(mappings),
      },
    );
    await ensureSuccess(response, "Unable to save board mappings");
    return response.json();
  },

  async listWorkItems(
    token: string,
    sourceId: number,
  ): Promise<ExternalWorkItemResponse[]> {
    const response = await fetch(`${integrationsBase}/sources/${sourceId}/items`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load external work items");
    return response.json();
  },

  async manualUpsertWorkItem(
    token: string,
    sourceId: number,
    body: ManualUpsertExternalWorkItemInput,
  ): Promise<ExternalWorkItemResponse> {
    const response = await fetch(
      `${integrationsBase}/sources/${sourceId}/items/manual-upsert`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: JSON.stringify(body),
      },
    );
    await ensureSuccess(response, "Unable to save external work item");
    return response.json();
  },
};
