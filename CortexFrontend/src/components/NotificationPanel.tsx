import { useRef } from "react";
import type { UserNotification } from "../types/notification";
import type { UserProfile } from "../types/user";
import { ScrollableViewport } from "./ui/ScrollableViewport";
import {
  getNotificationPriority,
  sortNotificationsByPriority,
  type NotificationPriority,
} from "../utils/notificationPriority";
import { USER_ID_TOKEN_PREFIX } from "../utils/ownerIdentity";
import { formatTicketIdentifier } from "../utils/presentation";

interface NotificationPanelProps {
  notifications: UserNotification[];
  unreadCount: number;
  loading: boolean;
  error: string | null;
  markingAllRead: boolean;
  markingNotificationId: number | null;
  currentUser: UserProfile | null;
  onRefresh: () => void;
  onMarkAllRead: () => Promise<void>;
  onOpenNotification: (notification: UserNotification) => Promise<void>;
  onMarkRead: (notification: UserNotification) => Promise<void>;
  onAssignToMe: (ticketId: string, ownerToken: string) => Promise<void>;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function getTypeClass(type: UserNotification["type"], priority: NotificationPriority) {
  if (priority === "high") {
    switch (type) {
      case "assignment":
        return "bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-100";
      case "status":
        return "bg-red-100 text-red-800 dark:bg-red-950/40 dark:text-red-200";
      default:
        return "bg-amber-100 text-amber-900 dark:bg-amber-950/50 dark:text-amber-100";
    }
  }
  switch (type) {
    case "assignment":
      return "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100";
    case "comment":
      return "bg-emerald-100 text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-200";
    case "status":
      return "bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-200";
    default:
      return "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";
  }
}

function getTypeLabel(type: UserNotification["type"]) {
  switch (type) {
    case "assignment":
      return "Assignment";
    case "comment":
      return "Comment";
    case "status":
      return "Status";
    default:
      return "System";
  }
}

function getRowClass(notification: UserNotification, priority: NotificationPriority): string {
  if (notification.isRead) {
    return "";
  }
  if (priority === "high") {
    return "border-l-2 border-l-amber-400 bg-amber-50/60 dark:border-l-amber-500 dark:bg-amber-950/20";
  }
  return "bg-cortex-blue-soft/40 dark:bg-cortex-blue/10";
}

function getUnreadDotClass(priority: NotificationPriority): string {
  if (priority === "high") {
    return "inline-flex h-2.5 w-2.5 rounded-full bg-amber-500";
  }
  return "inline-flex h-2.5 w-2.5 rounded-full bg-cortex-cyan";
}

function getMessageClass(isRead: boolean, priority: NotificationPriority): string {
  if (!isRead && priority === "high") {
    return "mt-1 text-sm font-medium text-gray-800 dark:text-slate-200";
  }
  return "mt-1 text-sm text-gray-600 dark:text-slate-400";
}

export default function NotificationPanel({
  notifications,
  unreadCount,
  loading,
  error,
  markingAllRead,
  markingNotificationId,
  currentUser,
  onRefresh,
  onMarkAllRead,
  onOpenNotification,
  onMarkRead,
  onAssignToMe,
}: NotificationPanelProps) {
  const notificationListScrollRef = useRef<HTMLDivElement | null>(null);
  const sorted = sortNotificationsByPriority(notifications);

  const currentUserOwnerToken =
    currentUser?.id != null ? `${USER_ID_TOKEN_PREFIX}${currentUser.id}` : null;

  return (
    <div className="absolute right-0 top-full z-[90] mt-2 w-[min(24rem,calc(100vw-2rem))] overflow-hidden rounded-lg border border-gray-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-4 border-b border-gray-100 px-4 py-3 dark:border-slate-800">
        <div>
          <p className="font-medium text-gray-900 dark:text-slate-100">
            Notifications
          </p>
          <p className="text-sm text-gray-500 dark:text-slate-400">
            {unreadCount === 0
              ? "All caught up"
              : `${unreadCount} unread notification${unreadCount === 1 ? "" : "s"}`}
          </p>
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <button
            onClick={onRefresh}
            className="rounded-md px-2 py-1 text-xs font-medium text-gray-600 transition-colors hover:bg-gray-100 hover:text-gray-900 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-slate-100"
          >
            Refresh
          </button>
          <button
            onClick={() => void onMarkAllRead()}
            disabled={markingAllRead || unreadCount === 0}
            className="rounded-md px-2 py-1 text-xs font-medium text-cortex-blue transition-colors hover:bg-cortex-blue-soft disabled:cursor-not-allowed disabled:opacity-50 dark:text-cortex-cyan dark:hover:bg-cortex-blue/20"
          >
            {markingAllRead ? "Marking..." : "Mark all read"}
          </button>
        </div>
      </div>

      {loading ? (
        <div className="space-y-3 px-4 py-4">
          {Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="animate-pulse rounded-lg border border-gray-100 px-3 py-3 dark:border-slate-800"
            >
              <div className="h-3 w-24 rounded bg-gray-200 dark:bg-slate-700" />
              <div className="mt-3 h-4 w-48 rounded bg-gray-200 dark:bg-slate-700" />
              <div className="mt-2 h-3 w-full rounded bg-gray-100 dark:bg-slate-800" />
            </div>
          ))}
        </div>
      ) : error ? (
        <div className="px-4 py-4">
          <div className="rounded border-l-4 border-red-500 bg-red-50 p-3 dark:bg-red-950/30">
            <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
          </div>
        </div>
      ) : sorted.length === 0 ? (
        <div className="px-4 py-10 text-center text-sm text-gray-500 dark:text-slate-400">
          No notifications yet.
        </div>
      ) : (
        <ScrollableViewport
          viewportRef={notificationListScrollRef}
          viewportClassName="max-h-[26rem] overflow-y-auto bg-white dark:bg-slate-900"
          affordanceAriaLabel="Scroll notifications to bottom"
        >
            <div className="divide-y divide-gray-100 dark:divide-slate-800">
              {sorted.map((notification) => {
                const priority = getNotificationPriority(notification);
                const canShowAssignToMe =
                  Boolean(currentUserOwnerToken) &&
                  notification.type === "assignment" &&
                  Boolean(notification.ticketId) &&
                  !notification.ticketIsArchived;

                return (
                  <div
                    key={notification.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => void onOpenNotification(notification)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter" || e.key === " ") {
                        e.preventDefault();
                        void onOpenNotification(notification);
                      }
                    }}
                    className={`w-full cursor-pointer px-4 py-3 text-left transition-colors hover:bg-gray-50 dark:hover:bg-slate-800/80 ${getRowClass(notification, priority)}`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2">
                          <span
                            className={`rounded-full px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${getTypeClass(notification.type, priority)}`}
                          >
                            {getTypeLabel(notification.type)}
                          </span>
                          {notification.ticketId && (
                            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[11px] font-medium text-gray-600 dark:bg-slate-800 dark:text-slate-300">
                              {formatTicketIdentifier(notification.ticketId)}
                            </span>
                          )}
                          {!notification.isRead && (
                            <span className={getUnreadDotClass(priority)} />
                          )}
                        </div>
                        <p className="mt-2 font-medium text-gray-900 dark:text-slate-100">
                          {notification.title}
                        </p>
                        <p className={getMessageClass(notification.isRead, priority)}>
                          {notification.message}
                        </p>
                        <p className="mt-2 text-xs text-gray-500 dark:text-slate-500">
                          {formatDateTime(notification.createdAt ?? notification.createdDateUtc)}
                        </p>
                        {(!notification.isRead || canShowAssignToMe) && (
                          <div
                            className="mt-2 flex items-center gap-2 flex-wrap"
                            onClick={(e) => e.stopPropagation()}
                            onKeyDown={(e) => e.stopPropagation()}
                          >
                            {!notification.isRead && (
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  void onMarkRead(notification);
                                }}
                                className="rounded px-2 py-0.5 text-[11px] font-medium text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700 dark:text-slate-400 dark:hover:bg-slate-700 dark:hover:text-slate-200"
                              >
                                Mark read
                              </button>
                            )}
                            {canShowAssignToMe && currentUserOwnerToken && (
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  void onAssignToMe(notification.ticketId!, currentUserOwnerToken);
                                  if (!notification.isRead) {
                                    void onMarkRead(notification);
                                  }
                                }}
                                className="rounded px-2 py-0.5 text-[11px] font-medium text-cortex-blue transition-colors hover:bg-cortex-blue-soft dark:text-cortex-cyan dark:hover:bg-cortex-blue/20"
                              >
                                Assign to me
                              </button>
                            )}
                          </div>
                        )}
                      </div>
                      {markingNotificationId === notification.id && (
                        <span className="text-xs text-gray-500 dark:text-slate-400">
                          Opening...
                        </span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
        </ScrollableViewport>
      )}
    </div>
  );
}
