import type {
  RoleDefinition,
  UpsertRoleDefinitionInput,
} from "../types/roleDefinition";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string): HeadersInit => ({
  "Content-Type": "application/json",
  Authorization: `Bearer ${token}`,
});

export const roleDefinitionService = {
  async getAll(token: string): Promise<RoleDefinition[]> {
    const response = await fetch(`${API_BASE_URL}/settings/roles`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load role definitions");
    return response.json();
  },

  async getPermissions(token: string): Promise<string[]> {
    const response = await fetch(`${API_BASE_URL}/settings/roles/permissions`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to load role permissions");
    return response.json();
  },

  async create(
    definition: UpsertRoleDefinitionInput,
    token: string,
  ): Promise<RoleDefinition> {
    const response = await fetch(`${API_BASE_URL}/settings/roles`, {
      method: "POST",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to create role definition");
    return response.json();
  },

  async update(
    id: number,
    definition: UpsertRoleDefinitionInput,
    token: string,
  ): Promise<RoleDefinition> {
    const response = await fetch(`${API_BASE_URL}/settings/roles/${id}`, {
      method: "PUT",
      headers: authHeaders(token),
      body: JSON.stringify(definition),
    });

    await ensureSuccess(response, "Failed to update role definition");
    return response.json();
  },

  async delete(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/settings/roles/${id}`, {
      method: "DELETE",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(response, "Failed to delete role definition");
  },
};
