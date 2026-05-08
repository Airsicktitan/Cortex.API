/** Align with backend enums (JSON string values as emitted by System.Text.Json + JsonStringEnumConverter). */

import type { ApprovalStatus } from "./ticket";
export type IntegrationProvider = "SharePoint" | "Jira" | "ServiceNow" | "SapReference";

export type IntegrationAuthMode =
  | "Manual"
  | "OAuth"
  | "AppRegistration"
  | "ApiToken"
  | "OAuthClientCredentials"
  | "ReferenceMetadata";

export type IntegrationSyncMode = "ReadOnly" | "ImportToCortex" | "TwoWay" | "Manual";

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
  /** Non-secret provider fields only; never includes tokens or passwords. */
  safeProviderSettings?: Record<string, string>;
  credentialConfigured?: boolean;
  credentialType?: string | null;
  lastValidatedAtUtc?: string | null;
  credentialStatus?: string;
  configuredCredentialFieldLabels?: string[];
  lastCredentialUpdatedAtUtc?: string | null;
  lastCredentialRotatedAtUtc?: string | null;
}

export interface SharePointDiscoveredFieldResponse {
  externalFieldName: string;
  externalFieldKey?: string | null;
  displayName?: string | null;
  type?: string | null;
  isHidden: boolean;
  isReadOnly: boolean;
  suggestedCortexField?: CortexField | null;
}

export interface ExternalSourceSyncResponse {
  sourceId: number;
  sourceName: string;
  provider: IntegrationProvider;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  createdCount: number;
  updatedCount: number;
  unchangedCount: number;
  skippedCount: number;
  errorCount: number;
  itemCount: number;
  message?: string | null;
}

export type IntegrationActivityType =
  | "DiscoverFields"
  | "SyncSource"
  | "ManualUpsert"
  | "CredentialConfigured"
  | "CredentialRotated"
  | "CredentialCleared";

export type IntegrationActivityStatus = "Success" | "Failed" | "Partial";

export interface IntegrationActivityLogEntry {
  id: number;
  /** Present when the activity is tied to an external source; omitted for connection-only events (e.g. credentials). */
  sourceId?: number | null;
  connectionId?: number | null;
  activityType: IntegrationActivityType;
  status: IntegrationActivityStatus;
  triggeredByDisplayName?: string | null;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  durationMs?: number | null;
  createdCount?: number | null;
  updatedCount?: number | null;
  unchangedCount?: number | null;
  skippedCount?: number | null;
  errorCount?: number | null;
  itemCount?: number | null;
  message?: string | null;
  errorMessage?: string | null;
}

export type IntegrationReadinessCheckStatus = "Passed" | "Warning" | "Failed";

export interface IntegrationReadinessCheckDto {
  key: string;
  label: string;
  status: IntegrationReadinessCheckStatus;
  message: string;
}

export interface ExternalSourceReadinessResponse {
  sourceId: number;
  sourceName: string;
  provider: IntegrationProvider;
  sourceType: ExternalSourceType;
  isReady: boolean;
  canDiscoverFields: boolean;
  canSync: boolean;
  checks: IntegrationReadinessCheckDto[];
}

export interface CreateIntegrationConnectionInput {
  provider: IntegrationProvider;
  displayName: string;
  tenantId?: string | null;
  organizationId?: string | null;
  authMode?: IntegrationAuthMode | null;
  syncMode?: IntegrationSyncMode | null;
  isEnabled?: boolean | null;
  /** Non-secret keys only; server rejects secret payloads. */
  providerSettings?: Record<string, string | null> | null;
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
  providerSettings?: Record<string, string | null> | null;
}

export interface IntegrationProviderFieldDefinitionDto {
  key: string;
  label: string;
  helpText: string;
  fieldType: string;
  required: boolean;
  isSecret: boolean;
  allowedValues?: string[] | null;
  placeholder?: string | null;
  validationHint?: string | null;
}

export interface IntegrationProviderDefinitionDto {
  provider: IntegrationProvider;
  displayName: string;
  description: string;
  allowedAuthModes: IntegrationAuthMode[];
  allowedSyncModes: IntegrationSyncMode[];
  fields: IntegrationProviderFieldDefinitionDto[];
  supportsFieldDiscovery: boolean;
  supportsSync: boolean;
  supportsTicketCreationFromExternalItem: boolean;
  referenceMetadataOnly: boolean;
}

export interface IntegrationProviderDefinitionsResponse {
  providers: IntegrationProviderDefinitionDto[];
}

export interface IntegrationCredentialStatusDto {
  connectionId: number;
  provider: IntegrationProvider;
  credentialConfigured: boolean;
  credentialStatus: string;
  configuredSecretFieldLabels: string[];
  authMode: IntegrationAuthMode;
  credentialType?: string | null;
  lastConfiguredAtUtc?: string | null;
  lastRotatedAtUtc?: string | null;
  lastValidatedAtUtc?: string | null;
}

export interface ConfigureIntegrationCredentialResponse {
  status: IntegrationCredentialStatusDto;
}

export interface ClearIntegrationCredentialResponse {
  status: IntegrationCredentialStatusDto;
}

/** PUT /api/integrations/connections/{id}/credentials */
export interface ConfigureIntegrationCredentialRequestBody {
  secrets?: Record<string, string | null>;
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

export const EXTERNAL_TICKET_PRIORITIES = ["Critical", "High", "Medium", "Low"] as const;

export type ExternalTicketPriority = (typeof EXTERNAL_TICKET_PRIORITIES)[number];

export interface CreateTicketFromExternalItemInput {
  boardId?: number | null;
  title?: string | null;
  description?: string | null;
  priority?: string | null;
  category?: string | null;
  department?: string | null;
  dueDateUtc?: string | null;
  requester?: string | null;
  assignedTo?: string | null;
  createAsPendingApproval?: boolean | null;
}

export interface CreateTicketFromExternalItemResponse {
  externalItemId: number;
  cortexTicketId: string;
  ticketTitle: string;
  boardId: number;
  boardName: string;
  approvalStatus: ApprovalStatus;
  message: string;
  externalItem: ExternalWorkItemResponse;
}

/** GET /api/tickets/{id}/external-source-context — safe fields only (no raw payload). */
export interface TicketExternalSourceContextItem {
  ticketId: string;
  externalWorkItemId: number;
  externalItemId: string;
  externalTitle?: string | null;
  externalStatus?: string | null;
  externalPriority?: string | null;
  provider: IntegrationProvider;
  sourceName: string;
  sourceType: ExternalSourceType;
  externalUrl?: string | null;
  requester?: string | null;
  assignedTo?: string | null;
  department?: string | null;
  category?: string | null;
  lastModifiedUtc?: string | null;
  lastSeenUtc: string;
  message?: string | null;
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
  "SapReference",
];

export const AUTH_MODES: IntegrationAuthMode[] = [
  "Manual",
  "OAuth",
  "AppRegistration",
  "ApiToken",
  "OAuthClientCredentials",
  "ReferenceMetadata",
];

export const SYNC_MODES: IntegrationSyncMode[] = ["ReadOnly", "ImportToCortex", "TwoWay", "Manual"];

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
