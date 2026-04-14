import type { NotificationFeed, UserNotification } from "../types/notification";
import { ensureSuccess } from "./api";

const API_BASE_URL = import.meta.env.VITE_API_URL;

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

export const notificationService = {
  async getFeed(token: string, take = 20): Promise<NotificationFeed> {
    const response = await fetch(`${API_BASE_URL}/notifications?take=${take}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to load notifications");
    return response.json();
  },

  async markRead(id: number, token: string): Promise<UserNotification> {
    const response = await fetch(`${API_BASE_URL}/notifications/${id}/read`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to mark notification as read");
    return response.json();
  },

  async markAllRead(token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/notifications/read-all`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Failed to mark notifications as read");
  },
};
