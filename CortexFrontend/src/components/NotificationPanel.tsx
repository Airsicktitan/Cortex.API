import type { UserNotification } from "../types/notification";

interface NotificationPanelProps {
  notifications: UserNotification[];
  unreadCount: number;
  loading: boolean;
  error: string | null;
  markingAllRead: boolean;
  markingNotificationId: number | null;
  onRefresh: () => void;
  onMarkAllRead: () => Promise<void>;
  onOpenNotification: (notification: UserNotification) => Promise<void>;
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function getSeverityClass(severity: string) {
  switch (severity.toLowerCase()) {
    case "critical":
      return "bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-200";
    case "warning":
      return "bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-200";
    default:
      return "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-200";
  }
}

export default function NotificationPanel({
  notifications,
  unreadCount,
  loading,
  error,
  markingAllRead,
  markingNotificationId,
  onRefresh,
  onMarkAllRead,
  onOpenNotification,
}: NotificationPanelProps) {
  return (
    <div className="absolute right-0 top-full z-[90] mt-2 w-[24rem] overflow-hidden rounded-lg border border-gray-200 bg-white shadow-2xl dark:border-slate-800 dark:bg-slate-900">
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

        <div className="flex items-center gap-2">
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
      ) : notifications.length === 0 ? (
        <div className="px-4 py-10 text-center text-sm text-gray-500 dark:text-slate-400">
          No notifications yet.
        </div>
      ) : (
        <div className="max-h-[26rem] overflow-y-auto bg-white dark:bg-slate-900">
          <div className="divide-y divide-gray-100 dark:divide-slate-800">
            {notifications.map((notification) => (
              <button
                key={notification.id}
                onClick={() => void onOpenNotification(notification)}
                className={`w-full px-4 py-3 text-left transition-colors hover:bg-gray-50 dark:hover:bg-slate-800/80 ${
                  notification.isRead ? "" : "bg-cortex-blue-soft/40 dark:bg-cortex-blue/10"
                }`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <span
                        className={`rounded-full px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide ${getSeverityClass(notification.severity)}`}
                      >
                        {notification.severity}
                      </span>
                      {!notification.isRead && (
                        <span className="inline-flex h-2.5 w-2.5 rounded-full bg-cortex-cyan" />
                      )}
                    </div>
                    <p className="mt-2 font-medium text-gray-900 dark:text-slate-100">
                      {notification.title}
                    </p>
                    <p className="mt-1 text-sm text-gray-600 dark:text-slate-400">
                      {notification.message}
                    </p>
                    <p className="mt-2 text-xs text-gray-500 dark:text-slate-500">
                      {formatDateTime(notification.createdDateUtc)}
                    </p>
                  </div>
                  {markingNotificationId === notification.id && (
                    <span className="text-xs text-gray-500 dark:text-slate-400">
                      Opening...
                    </span>
                  )}
                </div>
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
