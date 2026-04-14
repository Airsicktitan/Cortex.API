import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const storedProcedureService = {
  async getAll(token: string): Promise<StoredProcedureDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/settings/stored-procedures`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load stored procedures");
    return response.json();
  },

  async create(
    definition: UpsertStoredProcedureDefinitionInput,
    token: string,
  ): Promise<StoredProcedureDefinition> {
    const response = await fetch(`${API_BASE_URL}/settings/stored-procedures`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to create stored procedure");
    return response.json();
  },

  async update(
    id: number,
    definition: UpsertStoredProcedureDefinitionInput,
    token: string,
  ): Promise<StoredProcedureDefinition> {
    const response = await fetch(
      `${API_BASE_URL}/settings/stored-procedures/${id}`,
      {
        method: "PUT",
        headers: authHeaders(token),
        body: JSON.stringify(definition),
      },
    );

    await ensureSuccess(response, "Failed to update stored procedure");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(
      `${API_BASE_URL}/settings/stored-procedures/${id}`,
      {
        method: "DELETE",
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    await ensureSuccess(response, "Failed to delete stored procedure");
  },

  async getAvailableDatabaseProcedures(
    token: string,
  ): Promise<DatabaseStoredProcedureDefinition[]> {
    const response = await fetch(
      `${API_BASE_URL}/settings/stored-procedures/database-procedures`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      },
    );

    await ensureSuccess(response, "Failed to load database stored procedures");
    return response.json();
  },
};
