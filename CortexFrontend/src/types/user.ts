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
  role: string;
  createdDate: string;
  isActive: boolean;
  lastLoginDate?: string;
  lastSeenDateUtc?: string;
  expiryDate?: string;
  lastModifiedDate?: string;
}

export interface UserRecord extends UserProfile {
  auth0Id?: string;
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

export interface AdminUpdateUserInput {
  nickName?: string;
  phoneNumber?: string;
  department?: string;
  assignmentNotificationChannel?: NotificationChannelMode | "";
  slaRiskNotificationChannel?: NotificationChannelMode | "";
  role?: string;
  isActive?: boolean;
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
