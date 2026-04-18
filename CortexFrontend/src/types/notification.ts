export type NotificationType = "assignment" | "comment" | "status" | "system";

export interface UserNotification {
  id: number;
  type: NotificationType;
  category: string;
  eventType: string;
  severity: string;
  title: string;
  message: string;
  ticketId?: string;
  ticketIsArchived: boolean;
  isRead: boolean;
  createdAt: string;
  createdDateUtc: string;
  readDateUtc?: string;
}

export interface NotificationFeed {
  unreadCount: number;
  items: UserNotification[];
}
