import type { UserNotification } from "../types/notification";

export type NotificationPriority = "high" | "medium" | "low";

const PRIORITY_RANK: Record<NotificationPriority, number> = {
  high: 0,
  medium: 1,
  low: 2,
};

/**
 * Derives a display priority tier from available notification metadata.
 * Uses server severity when present; falls back to type + eventType heuristics.
 */
export function getNotificationPriority(n: UserNotification): NotificationPriority {
  const sev = n.severity?.toLowerCase() ?? "";
  if (sev === "critical" || sev === "high") return "high";
  if (sev === "low") return "low";

  if (n.type === "assignment") return "high";
  if (n.type === "comment") return "low";

  const et = (n.eventType ?? "").toLowerCase();
  if (
    et.includes("assignment") ||
    et.includes("assigned") ||
    et.includes("sla") ||
    et.includes("breach") ||
    et.includes("overdue") ||
    et.includes("risk") ||
    et.includes("approval") ||
    et.includes("review") ||
    et.includes("blocked") ||
    et.includes("needs_more_info") ||
    et.includes("needsmoreinfo")
  ) {
    return "high";
  }

  if (n.type === "status" || et.includes("status") || et.includes("update")) {
    return "medium";
  }

  return "low";
}

/**
 * Stable sort: unread before read, then high → medium → low within each group.
 * Does not mutate the input array.
 */
export function sortNotificationsByPriority(
  notifications: UserNotification[],
): UserNotification[] {
  return [...notifications].sort((a, b) => {
    if (!a.isRead && b.isRead) return -1;
    if (a.isRead && !b.isRead) return 1;
    return PRIORITY_RANK[getNotificationPriority(a)] - PRIORITY_RANK[getNotificationPriority(b)];
  });
}

export function hasHighPriorityUnread(notifications: UserNotification[]): boolean {
  return notifications.some((n) => !n.isRead && getNotificationPriority(n) === "high");
}
