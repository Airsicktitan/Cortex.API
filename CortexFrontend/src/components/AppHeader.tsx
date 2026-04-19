import { memo, type RefObject } from "react";
import type { UserNotification } from "../types/notification";
import type { UserProfile } from "../types/user";
import NotificationPanel from "./NotificationPanel";
import TicketHeaderActions from "./TicketHeaderActions";

type AppHeaderView =
  | "dashboard"
  | "tickets"
  | "approval"
  | "archived"
  | "reports"
  | "sla"
  | "jobs"
  | "users";

type NavigationGroup = "workspace" | "admin";

type NavigationItem = {
  view: AppHeaderView;
  group: NavigationGroup;
  label: string;
  description: string;
};

type AppHeaderProps = {
  activeView: AppHeaderView;
  activeViewLabel: string;
  activeNavigationDescription: string | null;
  navigationItems: NavigationItem[];
  realtimeStatus: "connected" | "reconnecting" | "offline";
  isStatusTooltipVisible: boolean;
  onStatusMouseEnter: () => void;
  onStatusMouseLeave: () => void;
  appMenuRef: RefObject<HTMLDivElement | null>;
  isAppMenuOpen: boolean;
  onToggleAppMenu: () => void;
  onViewChange: (view: AppHeaderView) => void;
  showTicketActions: boolean;
  canCreateTickets: boolean;
  onRefreshTickets: () => void;
  onCreateTicket: () => void;
  bootstrapComplete: boolean;
  needsConsent: boolean;
  onGrantConsent: () => void;
  sessionRefreshNotice: string | null;
  sessionRefreshInProgress: boolean;
  onRefreshSession: () => void;
  userMenuRef: RefObject<HTMLDivElement | null>;
  notificationPanelRef: RefObject<HTMLDivElement | null>;
  isNotificationPanelOpen: boolean;
  notificationUnreadCount: number;
  notifications: UserNotification[];
  notificationsLoading: boolean;
  notificationsError: string | null;
  markingAllNotificationsRead: boolean;
  markingNotificationId: number | null;
  onToggleNotificationPanel: () => void;
  onRefreshNotifications: () => void;
  onMarkAllNotificationsRead: () => Promise<void>;
  onOpenNotification: (notification: UserNotification) => Promise<void>;
  canManageJobs: boolean;
  failedJobsCount: number;
  onOpenFailedJobsQueue: () => void;
  isUserMenuOpen: boolean;
  onToggleUserMenu: () => void;
  currentUser: UserProfile | null;
  authDisplayName?: string;
  authEmail?: string;
  profileLoading: boolean;
  onOpenProfileModal: () => void;
  isDarkMode: boolean;
  onToggleThemeFromMenu: () => void;
  onLogout: () => void;
};

function AppHeader({
  activeView,
  activeViewLabel,
  activeNavigationDescription,
  navigationItems,
  realtimeStatus,
  isStatusTooltipVisible,
  onStatusMouseEnter,
  onStatusMouseLeave,
  appMenuRef,
  isAppMenuOpen,
  onToggleAppMenu,
  onViewChange,
  showTicketActions,
  canCreateTickets,
  onRefreshTickets,
  onCreateTicket,
  bootstrapComplete,
  needsConsent,
  onGrantConsent,
  sessionRefreshNotice,
  sessionRefreshInProgress,
  onRefreshSession,
  userMenuRef,
  notificationPanelRef,
  isNotificationPanelOpen,
  notificationUnreadCount,
  notifications,
  notificationsLoading,
  notificationsError,
  markingAllNotificationsRead,
  markingNotificationId,
  onToggleNotificationPanel,
  onRefreshNotifications,
  onMarkAllNotificationsRead,
  onOpenNotification,
  canManageJobs,
  failedJobsCount,
  onOpenFailedJobsQueue,
  isUserMenuOpen,
  onToggleUserMenu,
  currentUser,
  authDisplayName,
  authEmail,
  profileLoading,
  onOpenProfileModal,
  isDarkMode,
  onToggleThemeFromMenu,
  onLogout,
}: AppHeaderProps) {
  return (
    <header className="relative z-40 border-b border-gray-200 bg-white/92 shadow-sm backdrop-blur dark:border-slate-800 dark:bg-cortex-ink-dark/92">
      <div className="mx-auto flex w-full max-w-[2200px] flex-col gap-6 px-4 py-5 sm:px-6 sm:py-6 2xl:px-8 xl:flex-row xl:items-center xl:justify-between">
        <div className="space-y-4">
          <div className="flex flex-col gap-2 md:flex-row md:items-baseline md:gap-4">
            <h1 className="text-3xl font-bold">🧠 CORTEX</h1>
            <h2 className="text-lg text-gray-600 dark:text-slate-400">
              Central Operations & Routing Technology Expert
            </h2>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <div
              className={`relative inline-flex items-center gap-2 rounded-full transition-colors ${
                realtimeStatus === "connected"
                  ? "px-1 py-1"
                  : realtimeStatus === "reconnecting"
                    ? "bg-amber-100 px-2.5 py-1 text-xs font-medium text-amber-700 dark:bg-amber-950/30 dark:text-amber-300"
                    : "bg-red-100 px-2.5 py-1 text-xs font-medium text-red-700 dark:bg-red-950/30 dark:text-red-300"
              }`}
              onMouseEnter={onStatusMouseEnter}
              onMouseLeave={onStatusMouseLeave}
            >
              <span
                className={`inline-block h-2.5 w-2.5 rounded-full ${
                  realtimeStatus === "connected"
                    ? "bg-emerald-500"
                    : realtimeStatus === "reconnecting"
                      ? "bg-amber-500"
                      : "bg-red-500"
                }`}
              />
              {realtimeStatus === "reconnecting" ? "Reconnecting..." : null}
              {realtimeStatus === "offline" ? "Offline" : null}
              {isStatusTooltipVisible ? (
                <span className="pointer-events-none absolute -bottom-9 left-1/2 z-20 -translate-x-1/2 whitespace-nowrap rounded bg-slate-900 px-2 py-1 text-[11px] font-medium text-white shadow-sm dark:bg-slate-100 dark:text-slate-900">
                  {realtimeStatus === "connected"
                    ? "Online"
                    : realtimeStatus === "reconnecting"
                      ? "Trying to reconnect to live updates"
                      : "Live updates are paused. Changes may be out of date."}
                </span>
              ) : null}
            </div>

            <div ref={appMenuRef} className="relative lg:hidden">
              <button
                onClick={onToggleAppMenu}
                className="inline-flex items-center gap-3 rounded-md border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 20 20"
                  className="h-4 w-4"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                >
                  <path d="M3 5h14" />
                  <path d="M3 10h14" />
                  <path d="M3 15h14" />
                </svg>
                <span>Menu</span>
                <span className="text-xs text-gray-400 dark:text-slate-500">
                  ▾
                </span>
              </button>

              {isAppMenuOpen && (
                <div className="absolute left-0 top-full z-20 mt-2 w-80 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
                  <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
                    <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                      Navigate
                    </p>
                    <p className="mt-1 text-sm text-gray-600 dark:text-slate-300">
                      Current view:{" "}
                      <span className="font-medium text-gray-900 dark:text-slate-100">
                        {activeViewLabel}
                      </span>
                    </p>
                  </div>

                  {(["workspace", "admin"] as const).map((group) => {
                    const items = navigationItems.filter(
                      (item) => item.group === group,
                    );
                    if (items.length === 0) return null;

                    return (
                      <section
                        key={group}
                        className="border-t border-gray-100 dark:border-slate-800"
                      >
                        <p className="px-4 pt-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-gray-500 dark:text-slate-400">
                          {group === "workspace" ? "Workspace" : "Admin"}
                        </p>
                        {items.map((item) => {
                          const isActive = item.view === activeView;

                          return (
                            <button
                              key={item.view}
                              onClick={() => onViewChange(item.view)}
                              className={`w-full px-4 py-3 text-left transition-colors ${
                                isActive
                                  ? "bg-cortex-blue-soft text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100"
                                  : "text-gray-700 hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800"
                              }`}
                            >
                              <div className="flex items-center justify-between gap-4">
                                <span className="font-medium">{item.label}</span>
                                {isActive && (
                                  <span className="text-xs font-semibold uppercase tracking-wide text-cortex-blue dark:text-cortex-cyan">
                                    Active
                                  </span>
                                )}
                              </div>
                              <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                                {item.description}
                              </p>
                            </button>
                          );
                        })}
                      </section>
                    );
                  })}
                </div>
              )}
            </div>

            <div className="inline-flex items-center rounded-full bg-gray-100 px-3 py-2 text-sm text-gray-600 dark:bg-slate-800 dark:text-slate-300">
              {activeViewLabel}
            </div>

            {activeNavigationDescription && (
              <p className="hidden text-sm text-gray-500 dark:text-slate-400 lg:block">
                {activeNavigationDescription}
              </p>
            )}
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-end gap-4">
          {showTicketActions && (
            <TicketHeaderActions
              canCreateTickets={canCreateTickets}
              onRefresh={onRefreshTickets}
              onCreateTicket={onCreateTicket}
            />
          )}

          {bootstrapComplete && needsConsent && (
            <div className="flex items-center gap-2">
              <span className="text-sm text-yellow-700 dark:text-amber-300">
                CORTEX API consent is required before the app can load.
              </span>
              <button
                onClick={onGrantConsent}
                className="rounded-md bg-cortex-blue px-3 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
              >
                Grant Access
              </button>
            </div>
          )}

          {sessionRefreshNotice && (
            <div className="flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 dark:border-amber-900/40 dark:bg-amber-950/30">
              <span className="text-sm text-amber-800 dark:text-amber-200">
                {sessionRefreshNotice}
              </span>
              <button
                onClick={onRefreshSession}
                disabled={sessionRefreshInProgress}
                className="rounded-md bg-amber-600 px-3 py-1.5 text-sm text-white transition-colors hover:bg-amber-700 disabled:cursor-not-allowed disabled:opacity-70"
              >
                {sessionRefreshInProgress ? "Refreshing..." : "Refresh Session"}
              </button>
            </div>
          )}

          <div
            ref={userMenuRef}
            className="relative flex items-center gap-3 border-l border-gray-300 pl-4 dark:border-slate-700"
          >
            <div ref={notificationPanelRef} className="relative">
              <button
                onClick={onToggleNotificationPanel}
                className="relative inline-flex h-10 w-10 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-cortex-blue-soft hover:text-cortex-blue dark:text-slate-300 dark:hover:bg-cortex-blue/20 dark:hover:text-cortex-cyan"
                title={
                  notificationUnreadCount === 0
                    ? "Notifications"
                    : `${notificationUnreadCount} unread notification${notificationUnreadCount === 1 ? "" : "s"}`
                }
                aria-label={
                  notificationUnreadCount === 0
                    ? "Open notifications"
                    : `Open notifications with ${notificationUnreadCount} unread`
                }
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 20 20"
                  className="h-5 w-5"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M10 3.5a5 5 0 0 0-5 5v2.1c0 .7-.2 1.3-.6 1.9l-.8 1.2c-.4.6 0 1.3.7 1.3h11.4c.7 0 1.1-.7.7-1.3l-.8-1.2c-.4-.6-.6-1.2-.6-1.9V8.5a5 5 0 0 0-5-5Z" />
                  <path d="M8.5 16.5a1.5 1.5 0 0 0 3 0" />
                </svg>
                {notificationUnreadCount > 0 && (
                  <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-cortex-cyan px-1 text-[11px] font-semibold leading-none text-cortex-ink">
                    {notificationUnreadCount > 9 ? "9+" : notificationUnreadCount}
                  </span>
                )}
              </button>

              {isNotificationPanelOpen && (
                <NotificationPanel
                  notifications={notifications}
                  unreadCount={notificationUnreadCount}
                  loading={notificationsLoading}
                  error={notificationsError}
                  markingAllRead={markingAllNotificationsRead}
                  markingNotificationId={markingNotificationId}
                  onRefresh={onRefreshNotifications}
                  onMarkAllRead={onMarkAllNotificationsRead}
                  onOpenNotification={onOpenNotification}
                />
              )}
            </div>

            {canManageJobs && failedJobsCount > 0 && (
              <button
                onClick={onOpenFailedJobsQueue}
                className="relative inline-flex h-10 w-10 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-red-50 hover:text-red-700 dark:text-slate-300 dark:hover:bg-red-950/30 dark:hover:text-red-200"
                title={
                  failedJobsCount === 1
                    ? "1 failed job needs attention"
                    : `${failedJobsCount} failed jobs need attention`
                }
                aria-label={
                  failedJobsCount === 1
                    ? "Open failed jobs queue"
                    : `Open failed jobs queue with ${failedJobsCount} failed jobs`
                }
              >
                <svg
                  aria-hidden="true"
                  viewBox="0 0 20 20"
                  className="h-5 w-5"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M10 3a4 4 0 0 0-4 4v2.6c0 .7-.2 1.4-.6 2l-.9 1.3A1 1 0 0 0 5.3 14h9.4a1 1 0 0 0 .8-1.6l-.9-1.3a3.6 3.6 0 0 1-.6-2V7a4 4 0 0 0-4-4Z" />
                  <path d="M8.5 16a1.5 1.5 0 0 0 3 0" />
                </svg>
                <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-600 px-1 text-[11px] font-semibold leading-none text-white">
                  {failedJobsCount > 9 ? "9+" : failedJobsCount}
                </span>
              </button>
            )}

            <button
              onClick={onToggleUserMenu}
              className="inline-flex items-center gap-2 rounded-md px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 dark:text-slate-300 dark:hover:bg-slate-800"
            >
              <span>
                {currentUser?.nickName ?? currentUser?.displayName ?? authDisplayName}
              </span>
              <span className="text-xs text-gray-500 dark:text-slate-400">
                ▾
              </span>
            </button>

            {isUserMenuOpen && (
              <div className="absolute right-0 top-full z-20 mt-2 w-72 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
                <div className="border-b border-gray-100 px-4 py-3 dark:border-slate-800">
                  <p className="font-medium text-gray-900 dark:text-slate-100">
                    {currentUser?.displayName ?? authDisplayName}
                  </p>
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    {currentUser?.email ?? authEmail}
                  </p>
                  {currentUser?.nickName && (
                    <p className="mt-1 text-xs text-gray-500 dark:text-slate-400">
                      Nick name: {currentUser.nickName}
                    </p>
                  )}
                </div>

                <button
                  onClick={onOpenProfileModal}
                  disabled={profileLoading}
                  className="w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  {profileLoading ? "Loading Profile..." : "Edit Profile"}
                </button>
                <button
                  onClick={onToggleThemeFromMenu}
                  className="w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  {isDarkMode ? "Light Mode" : "Dark Mode"}
                </button>
                <button
                  onClick={onLogout}
                  className="w-full px-4 py-3 text-left text-sm text-red-600 transition-colors hover:bg-red-50 dark:text-red-300 dark:hover:bg-red-950/30"
                >
                  Log Out
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}

function areNavigationItemsEqual(
  previousItems: NavigationItem[],
  nextItems: NavigationItem[],
) {
  if (previousItems === nextItems) {
    return true;
  }

  if (previousItems.length !== nextItems.length) {
    return false;
  }

  return previousItems.every((item, index) => {
    const nextItem = nextItems[index];
    return (
      item.view === nextItem.view &&
      item.group === nextItem.group &&
      item.label === nextItem.label &&
      item.description === nextItem.description
    );
  });
}

function areAppHeaderPropsEqual(
  previousProps: AppHeaderProps,
  nextProps: AppHeaderProps,
) {
  return (
    previousProps.activeView === nextProps.activeView &&
    previousProps.activeViewLabel === nextProps.activeViewLabel &&
    previousProps.activeNavigationDescription ===
      nextProps.activeNavigationDescription &&
    areNavigationItemsEqual(
      previousProps.navigationItems,
      nextProps.navigationItems,
    ) &&
    previousProps.realtimeStatus === nextProps.realtimeStatus &&
    previousProps.isStatusTooltipVisible === nextProps.isStatusTooltipVisible &&
    previousProps.onStatusMouseEnter === nextProps.onStatusMouseEnter &&
    previousProps.onStatusMouseLeave === nextProps.onStatusMouseLeave &&
    previousProps.appMenuRef === nextProps.appMenuRef &&
    previousProps.isAppMenuOpen === nextProps.isAppMenuOpen &&
    previousProps.onToggleAppMenu === nextProps.onToggleAppMenu &&
    previousProps.onViewChange === nextProps.onViewChange &&
    previousProps.showTicketActions === nextProps.showTicketActions &&
    previousProps.canCreateTickets === nextProps.canCreateTickets &&
    previousProps.onRefreshTickets === nextProps.onRefreshTickets &&
    previousProps.onCreateTicket === nextProps.onCreateTicket &&
    previousProps.bootstrapComplete === nextProps.bootstrapComplete &&
    previousProps.needsConsent === nextProps.needsConsent &&
    previousProps.onGrantConsent === nextProps.onGrantConsent &&
    previousProps.sessionRefreshNotice === nextProps.sessionRefreshNotice &&
    previousProps.sessionRefreshInProgress ===
      nextProps.sessionRefreshInProgress &&
    previousProps.onRefreshSession === nextProps.onRefreshSession &&
    previousProps.userMenuRef === nextProps.userMenuRef &&
    previousProps.notificationPanelRef === nextProps.notificationPanelRef &&
    previousProps.isNotificationPanelOpen ===
      nextProps.isNotificationPanelOpen &&
    previousProps.notificationUnreadCount === nextProps.notificationUnreadCount &&
    previousProps.notifications === nextProps.notifications &&
    previousProps.notificationsLoading === nextProps.notificationsLoading &&
    previousProps.notificationsError === nextProps.notificationsError &&
    previousProps.markingAllNotificationsRead ===
      nextProps.markingAllNotificationsRead &&
    previousProps.markingNotificationId === nextProps.markingNotificationId &&
    previousProps.onToggleNotificationPanel ===
      nextProps.onToggleNotificationPanel &&
    previousProps.onRefreshNotifications === nextProps.onRefreshNotifications &&
    previousProps.onMarkAllNotificationsRead ===
      nextProps.onMarkAllNotificationsRead &&
    previousProps.onOpenNotification === nextProps.onOpenNotification &&
    previousProps.canManageJobs === nextProps.canManageJobs &&
    previousProps.failedJobsCount === nextProps.failedJobsCount &&
    previousProps.onOpenFailedJobsQueue === nextProps.onOpenFailedJobsQueue &&
    previousProps.isUserMenuOpen === nextProps.isUserMenuOpen &&
    previousProps.onToggleUserMenu === nextProps.onToggleUserMenu &&
    previousProps.currentUser === nextProps.currentUser &&
    previousProps.authDisplayName === nextProps.authDisplayName &&
    previousProps.authEmail === nextProps.authEmail &&
    previousProps.profileLoading === nextProps.profileLoading &&
    previousProps.onOpenProfileModal === nextProps.onOpenProfileModal &&
    previousProps.isDarkMode === nextProps.isDarkMode &&
    previousProps.onToggleThemeFromMenu === nextProps.onToggleThemeFromMenu &&
    previousProps.onLogout === nextProps.onLogout
  );
}

export default memo(AppHeader, areAppHeaderPropsEqual);
