import type { Comment } from "../types/comment";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

export const commentService = {
  async getByTicket(ticketId: string, token: string): Promise<Comment[]> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(res, "Unable to load comments");
    return res.json();
  },

  async getByArchivedTicket(ticketId: string, token: string): Promise<Comment[]> {
    const res = await fetch(`${API_BASE_URL}/tickets/archived/${ticketId}/comments`, {
      cache: "no-store",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(res, "Unable to load archived comments");
    return res.json();
  },

  async create(
    ticketId: string,
    body: string,
    token: string,
  ): Promise<Comment> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ body }),
    });

    await ensureSuccess(res, "Unable to add comment");
    return res.json();
  },

  async sendTyping(ticketId: string, token: string): Promise<void> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments/typing`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    await ensureSuccess(res, "Unable to send typing status");
  },
};
