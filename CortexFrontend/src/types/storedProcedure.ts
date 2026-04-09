export interface StoredProcedureDefinition {
  id: number;
  name: string;
  procedureName: string;
  definitionSql: string;
  description?: string;
  isEnabled: boolean;
  createdDateUtc: string;
  lastModifiedDateUtc?: string;
}

export interface UpsertStoredProcedureDefinitionInput {
  name: string;
  procedureName: string;
  definitionSql: string;
  description?: string;
  isEnabled: boolean;
}

export interface DatabaseStoredProcedureDefinition {
  procedureName: string;
  definitionSql: string;
}
