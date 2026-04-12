export type NotificationChannelMode = "Neither" | "Email" | "Teams" | "Both";

export interface NotificationChannelConfiguration {
  assignmentChannel: NotificationChannelMode;
  slaRiskChannel: NotificationChannelMode;
}
