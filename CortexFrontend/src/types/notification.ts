export interface UserNotification {
  id: number;
  category: string;
  eventType: string;
  severity: string;
  title: string;
  message: string;
  ticketId?: string;
  ticketIsArchived: boolean;
  isRead: boolean;
  createdDateUtc: string;
  readDateUtc?: string;
}

export interface NotificationFeed {
  unreadCount: number;
  items: UserNotification[];
}
