/** Align with backend enums (reference knowledge only — not a live SAP connector). */

export type SapReferenceSourceType =
  | "Manual"
  | "CsvImport"
  | "MetadataExport"
  | "SynitiExport"
  | "FutureLiveSap";

export interface SapReferenceSourceResponse {
  id: number;
  name: string;
  description?: string | null;
  sourceType: SapReferenceSourceType;
  systemLabel?: string | null;
  client?: string | null;
  environment?: string | null;
  isEnabled: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface SapTableMetadataResponse {
  id: number;
  sapReferenceSourceId: number;
  tableName: string;
  description?: string | null;
  module?: string | null;
  businessObject?: string | null;
  dataDomain?: string | null;
  isCustom: boolean;
  notes?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  fieldCount: number;
}

export interface SapFieldMetadataResponse {
  id: number;
  sapTableMetadataId: number;
  fieldName: string;
  description?: string | null;
  dataElement?: string | null;
  domainName?: string | null;
  dataType?: string | null;
  length?: number | null;
  isKey: boolean;
  isRequired?: boolean | null;
  isCustom: boolean;
  businessMeaning?: string | null;
  exampleValue?: string | null;
  notes?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface SapReferenceSearchResultDto {
  resultType: string;
  sourceId: number;
  sourceName: string;
  tableId?: number | null;
  tableName?: string | null;
  fieldId?: number | null;
  fieldName?: string | null;
  title: string;
  subtitle?: string | null;
  description?: string | null;
  isCustom?: boolean | null;
  module?: string | null;
  businessObject?: string | null;
  relevanceReason: string;
  domainValueId?: number | null;
}

export interface CreateSapReferenceSourceInput {
  name: string;
  description?: string | null;
  sourceType?: SapReferenceSourceType | null;
  systemLabel?: string | null;
  client?: string | null;
  environment?: string | null;
  isEnabled?: boolean | null;
}

export interface CreateSapTableInput {
  tableName: string;
  description?: string | null;
  module?: string | null;
  businessObject?: string | null;
  dataDomain?: string | null;
  isCustom?: boolean | null;
  notes?: string | null;
}

export interface CreateSapFieldInput {
  fieldName: string;
  description?: string | null;
  dataElement?: string | null;
  domainName?: string | null;
  dataType?: string | null;
  length?: number | null;
  isKey?: boolean | null;
  isRequired?: boolean | null;
  isCustom?: boolean | null;
  businessMeaning?: string | null;
  exampleValue?: string | null;
  notes?: string | null;
}
