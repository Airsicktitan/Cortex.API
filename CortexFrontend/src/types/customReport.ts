export interface CustomReportDefinition {
  id: number;
  name: string;
  viewName: string;
  description?: string;
  sqlQuery: string;
  isEnabled: boolean;
  sourceKey?: string;
  selectedColumns?: string;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertCustomReportDefinitionInput {
  name: string;
  viewName: string;
  description?: string;
  sqlQuery?: string;
  isEnabled: boolean;
  sourceKey?: string;
  selectedColumns?: string;
}

export interface DatabaseViewDefinition {
  viewName: string;
  definitionSql?: string | null;
}

export interface ReportSourceColumn {
  key: string;
  label: string;
}

export interface ReportSource {
  key: string;
  label: string;
  description: string;
  columns: ReportSourceColumn[];
}

export interface CustomReportResult {
  reportName: string;
  columns: string[];
  rows: Array<Record<string, unknown>>;
  generatedDateUtc: string;
  isTruncated: boolean;
}
