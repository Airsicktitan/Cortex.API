import type { SynitiKnowledgeCatalogListResponse } from "../types/synitiKnowledgeCatalog";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

function headers(token: string): HeadersInit {
  return { Authorization: `Bearer ${token}` };
}

function buildQuery(q?: string, itemCategory?: string): string {
  const p = new URLSearchParams();
  if (q?.trim()) {
    p.set("q", q.trim());
  }

  if (itemCategory?.trim()) {
    p.set("category", itemCategory.trim());
  }

  const s = p.toString();
  return s ? `?${s}` : "";
}

export const synitiKnowledgeCatalogService = {
  async list(
    token: string,
    options?: { q?: string; category?: string },
  ): Promise<SynitiKnowledgeCatalogListResponse> {
    const qs = buildQuery(options?.q, options?.category);
    const res = await fetch(
      `${API_BASE_URL}/reference-catalogs/syniti-knowledge${qs}`,
      { headers: headers(token) },
    );
    await ensureSuccess(res, "Unable to load Syniti knowledge catalog.");
    return res.json();
  },
};
