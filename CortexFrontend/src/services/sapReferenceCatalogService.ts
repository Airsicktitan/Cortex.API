import type { SapReferenceCatalogListResponse } from "../types/sapReferenceCatalog";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

function headers(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}` };
}

export const sapReferenceCatalogService = {
  async list(token: string): Promise<SapReferenceCatalogListResponse> {
    const res = await fetch(`${API_BASE_URL}/reference-catalogs/sap-reference`, {
      headers: headers(token),
    });
    await ensureSuccess(res, "Unable to load SAP reference catalog.");
    return res.json();
  },
};
