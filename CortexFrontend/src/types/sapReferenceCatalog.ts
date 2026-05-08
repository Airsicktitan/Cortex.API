/** GET /api/reference-catalogs/sap-reference — read-only SAP reference catalog visibility. */

export interface SapReferenceCatalogEntry {
  rowKind: "Table" | "Field" | string;
  tableName: string;
  fieldName: string | null;
  tableDescription: string | null;
  fieldDescription: string | null;
  businessObject: string | null;
  module: string | null;
  domain: string | null;
  isKey: boolean | null;
  isRequired: boolean | null;
  isCustomField: boolean | null;
  likelyCustomSapField: boolean;
  sourceName: string;
  sourceType: string;
  sourceIsEnabled: boolean;
  fieldCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SapReferenceCatalogListResponse {
  entries: SapReferenceCatalogEntry[];
}
