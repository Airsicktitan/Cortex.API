export interface CustomReportDefinition {
  id: number;
  name: string;
  viewName: string;
  description?: string;
  sqlQuery: string;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertCustomReportDefinitionInput {
  name: string;
  viewName: string;
  description?: string;
  sqlQuery: string;
  isEnabled: boolean;
}

export interface DatabaseViewDefinition {
  viewName: string;
  definitionSql: string;
}

export interface CustomReportResult {
  reportName: string;
  columns: string[];
  rows: Array<Record<string, unknown>>;
  generatedDateUtc: string;
  isTruncated: boolean;
}
