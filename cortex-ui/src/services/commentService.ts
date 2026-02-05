import type { Comment } from "../types/comment";

const BASE = "http://localhost:5214/api";

export const commentService = {
  async getByTicket(ticketId: string): Promise<Comment[]> {
    const res = await fetch(`${BASE}/tickets/${ticketId}/comments`);
    if (!res.ok) throw new Error("Failed to load comments");
    return res.json();
  },

  async create(ticketId: string, body: string): Promise<Comment> {
    const res = await fetch(`${BASE}/tickets/${ticketId}/comments`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ body }),
    });

    if (!res.ok) throw new Error("Failed to create comment");
    return res.json();
  },
};
