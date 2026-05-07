import toast from "react-hot-toast";
import type { AdminUpdateUserResult, Auth0ProfileSyncStatus } from "../types/user";

const durationMs = 6500;

export type AdminUserUpdateToastKind = "success" | "info" | "warning";

/** Enterprise copy: one combined message per admin save + Auth0 sync outcome. */
export function getAdminUserUpdateToast(status: Auth0ProfileSyncStatus): {
  message: string;
  kind: AdminUserUpdateToastKind;
} {
  switch (status) {
    case "Synced":
      return {
        kind: "success",
        message: "User updated in Cortex and mirrored to Auth0.",
      };
    case "Skipped":
      return {
        kind: "success",
        message:
          "User updated in Cortex. Auth0 sync skipped because display name and nickname were unchanged.",
      };
    case "NotConfigured":
      return {
        kind: "info",
        message: "User updated in Cortex. Auth0 profile sync is not enabled.",
      };
    case "Failed":
      return {
        kind: "warning",
        message:
          "User updated in Cortex, but Auth0 profile sync failed. Reimport may restore the previous Auth0 profile values.",
      };
  }
}

/** Single toast owner for a successful admin user update (HTTP 200). */
export function showAdminUserUpdateToast(result: AdminUpdateUserResult): void {
  const { message, kind } = getAdminUserUpdateToast(result.auth0ProfileSyncStatus);
  switch (kind) {
    case "success":
      toast.success(message, { duration: durationMs });
      break;
    case "info":
      toast(message, {
        duration: durationMs,
        style: {
          background: "#e0f2fe",
          color: "#0c4a6e",
        },
      });
      break;
    case "warning":
      toast(message, {
        duration: durationMs,
        style: {
          background: "#fef3c7",
          color: "#92400e",
          border: "1px solid #f59e0b",
        },
      });
      break;
  }
}
