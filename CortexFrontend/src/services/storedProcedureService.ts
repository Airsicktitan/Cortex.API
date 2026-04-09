import type {
  DatabaseStoredProcedureDefinition,
  StoredProcedureDefinition,
  UpsertStoredProcedureDefinitionInput,
} from "../types/storedProcedure";
import { ApiError } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

async function readErrorMessage(response: Response, fallbackMessage: string) {
  try {
    const data = (await response.json()) as unknown;

    if (typeof data === "string" && data.trim()) {
      return data;
    }

    if (typeof data === "object" && data !== null) {
      const message = "message" in data ? data.message : undefined;
      if (typeof message === "string" && message.trim()) {
        return message;
      }
    }
  } catch {
    // Ignore unreadable response bodies and use the fallback.
  }

  return fallbackMessage;
}

async function ensureSuccess(response: Response, fallbackMessage: string) {
  if (response.ok) {
    return;
  }

  const message = await readErrorMessage(response, fallbackMessage);
  throw new ApiError(message, response.status);
}

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
