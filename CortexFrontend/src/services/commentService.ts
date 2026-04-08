import type { Comment } from "../types/comment";

const API_BASE_URL = import.meta.env.VITE_API_URL;

export const commentService = {
  async getByTicket(ticketId: string, token: string): Promise<Comment[]> {
    const res = await fetch(`${API_BASE_URL}/tickets/${ticketId}/comments`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!res.ok) throw new Error("Failed to load comments");
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

    if (!res.ok) throw new Error("Failed to create comment");
    return res.json();
  },
};
