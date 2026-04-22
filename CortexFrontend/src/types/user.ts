import type { NotificationChannelMode } from "./notificationChannelConfiguration";

export interface UserProfile {
  id: number;
  displayName: string;
  nickName?: string;
  email: string;
  phoneNumber?: string;
  department?: string;
  assignmentNotificationChannel?: NotificationChannelMode | null;
  slaRiskNotificationChannel?: NotificationChannelMode | null;
  /** Highest-privilege role from local DB (display); prefer `roles` from API for Auth0. */
  role: string;
  /** Roles from the access token (Auth0); authoritative for UI authorization. */
  roles?: string[];
  createdDate: string;
  isActive: boolean;
  isSynitiOwnerEligible: boolean;
  isBusinessOwnerEligible: boolean;
  lastLoginDate?: string;
  lastSeenDateUtc?: string;
  expiryDate?: string;
  lastModifiedDate?: string;
}

export interface UserRecord extends UserProfile {
  auth0Id?: string;
}

/** Role definition from Auth0 Management API (admin screens). */
export interface Auth0RoleOption {
  id: string;
  name: string;
}

export interface UserAuth0RolesResponse {
  roles: Auth0RoleOption[];
}

export interface UserRoleMutationRequest {
  action: "add" | "remove";
  roleName: string;
}

export interface OnlineUser {
  id: number;
  displayName: string;
  nickName?: string;
  email: string;
  department?: string;
  role: string;
  lastSeenDateUtc?: string;
  lastLoginDate?: string;
}

export interface UserDirectoryEntry {
  id: number;
  displayName: string;
  email: string;
  department?: string;
  /** Local role display (e.g. User, Business Manager); optional for older API responses. */
  role?: string;
  isActive: boolean;
  isSynitiOwnerEligible: boolean;
  isBusinessOwnerEligible: boolean;
}

/** Result of POST /api/users/sync-from-auth0 (local directory projection from Auth0). */
export interface SyncUsersFromAuth0Result {
  totalFromAuth0: number;
  created: number;
  linkedByEmail: number;
  updated: number;
  unchanged: number;
  skippedNoEmail: number;
  skippedEmailConflict: number;
}

export interface AdminUpdateUserInput {
  nickName?: string;
  phoneNumber?: string;
  department?: string;
  assignmentNotificationChannel?: NotificationChannelMode | "";
  slaRiskNotificationChannel?: NotificationChannelMode | "";
  role?: string;
  isActive?: boolean;
  isSynitiOwnerEligible?: boolean;
  isBusinessOwnerEligible?: boolean;
  expiryDate?: string | null;
}

export interface CreateUserInput {
  displayName: string;
  nickName?: string;
  email: string;
  password: string;
  phoneNumber?: string;
  department?: string;
  role: string;
  isActive: boolean;
  isSynitiOwnerEligible: boolean;
  isBusinessOwnerEligible: boolean;
  expiryDate?: string | null;
}

export interface UpdateUserProfileInput {
  displayName?: string;
  nickName?: string;
  phoneNumber?: string;
  department?: string;
  assignmentNotificationChannel?: NotificationChannelMode | "";
  slaRiskNotificationChannel?: NotificationChannelMode | "";
}
