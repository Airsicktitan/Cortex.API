/** Align with backend enums (JSON string values as emitted by System.Text.Json + JsonStringEnumConverter). */

export type IntegrationProvider = "SharePoint" | "Jira" | "ServiceNow";

export type IntegrationAuthMode = "Manual" | "OAuth" | "AppRegistration";

export type IntegrationSyncMode = "ReadOnly" | "ImportToCortex" | "TwoWay";

export type ExternalSourceType = "SharePointList" | "JiraProject" | "ServiceNowTable";

export type CortexField =
  | "Title"
  | "Description"
  | "Status"
  | "Priority"
  | "Requester"
  | "Department"
  | "BusinessOwner"
  | "SynitiOwner"
  | "Category"
  | "DueDate"
  | "EvidenceUrl"
  | "Unknown";

export type ExternalBoardMappingMode = "Mirror" | "Import" | "ReferenceOnly";

export interface IntegrationConnectionResponse {
  id: number;
  provider: IntegrationProvider;
  displayName: string;
  tenantId?: string | null;
  organizationId?: string | null;
  authMode: IntegrationAuthMode;
  syncMode: IntegrationSyncMode;
  isEnabled: boolean;
  lastSyncUtc?: string | null;
  lastSyncStatus?: string | null;
  lastSyncMessage?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  externalWorkSourceCount: number;
}

export interface CreateIntegrationConnectionInput {
  provider: IntegrationProvider;
  displayName: string;
  tenantId?: string | null;
  organizationId?: string | null;
  authMode?: IntegrationAuthMode | null;
  syncMode?: IntegrationSyncMode | null;
  isEnabled?: boolean | null;
}

export interface UpdateIntegrationConnectionInput {
  displayName: string;
  tenantId?: string | null;
  organizationId?: string | null;
  authMode?: IntegrationAuthMode | null;
  syncMode?: IntegrationSyncMode | null;
  isEnabled?: boolean | null;
  lastSyncUtc?: string | null;
  lastSyncStatus?: string | null;
  lastSyncMessage?: string | null;
}

export interface ExternalWorkSourceResponse {
  id: number;
  integrationConnectionId: number;
  provider: IntegrationProvider;
  sourceType: ExternalSourceType;
  externalSourceId: string;
  name: string;
  externalUrl?: string | null;
  isEnabled: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  fieldMappingCount: number;
  boardMappingCount: number;
}

export interface CreateExternalWorkSourceInput {
  provider: IntegrationProvider;
  sourceType: ExternalSourceType;
  externalSourceId: string;
  name: string;
  externalUrl?: string | null;
  isEnabled?: boolean | null;
}

export interface UpdateExternalWorkSourceInput {
  name: string;
  externalUrl?: string | null;
  provider?: IntegrationProvider | null;
  sourceType?: ExternalSourceType | null;
  externalSourceId?: string | null;
  isEnabled?: boolean | null;
}

export interface ExternalFieldMappingResponse {
  id: number;
  externalFieldName: string;
  externalFieldKey?: string | null;
  cortexField: CortexField;
  isRequired: boolean;
  transformHint?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface ExternalFieldMappingItemInput {
  externalFieldName: string;
  externalFieldKey?: string | null;
  cortexField: CortexField;
  isRequired: boolean;
  transformHint?: string | null;
}

export interface ExternalBoardMappingResponse {
  id: number;
  boardId: number;
  boardName: string;
  mappingMode: ExternalBoardMappingMode;
  isDefault: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface ExternalBoardMappingItemInput {
  boardId: number;
  mappingMode: ExternalBoardMappingMode;
  isDefault: boolean;
}

export interface ExternalWorkItemResponse {
  id: number;
  provider: IntegrationProvider;
  sourceName: string;
  externalItemId: string;
  externalUrl?: string | null;
  title: string;
  description?: string | null;
  status?: string | null;
  priority?: string | null;
  requester?: string | null;
  assignedTo?: string | null;
  department?: string | null;
  category?: string | null;
  dueDateUtc?: string | null;
  lastModifiedUtc?: string | null;
  lastSeenUtc: string;
  isDeleted: boolean;
  cortexTicketId?: string | null;
}

export interface ManualUpsertExternalWorkItemInput {
  externalItemId: string;
  title: string;
  externalUrl?: string | null;
  description?: string | null;
  status?: string | null;
  priority?: string | null;
  requester?: string | null;
  assignedTo?: string | null;
  department?: string | null;
  category?: string | null;
  dueDateUtc?: string | null;
  lastModifiedUtc?: string | null;
  rawJson?: string | null;
  syncHash?: string | null;
  cortexTicketId?: string | null;
}

export const INTEGRATION_PROVIDERS: IntegrationProvider[] = [
  "SharePoint",
  "Jira",
  "ServiceNow",
];

export const AUTH_MODES: IntegrationAuthMode[] = ["Manual", "OAuth", "AppRegistration"];

export const SYNC_MODES: IntegrationSyncMode[] = ["ReadOnly", "ImportToCortex", "TwoWay"];

export const SOURCE_TYPES: ExternalSourceType[] = [
  "SharePointList",
  "JiraProject",
  "ServiceNowTable",
];

export const CORTEX_FIELDS: CortexField[] = [
  "Title",
  "Description",
  "Status",
  "Priority",
  "Requester",
  "Department",
  "BusinessOwner",
  "SynitiOwner",
  "Category",
  "DueDate",
  "EvidenceUrl",
  "Unknown",
];

export const BOARD_MAPPING_MODES: ExternalBoardMappingMode[] = [
  "ReferenceOnly",
  "Import",
  "Mirror",
];
