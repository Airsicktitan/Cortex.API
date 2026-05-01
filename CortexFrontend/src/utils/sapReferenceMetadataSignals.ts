import type { SapTicketReferenceMatch } from "../types/sapTicketReference";

export function normTable(name: string | null | undefined): string {
  return (name ?? "").trim().toUpperCase();
}

/** YY/ZZ-style SAP customer extension fields plus catalog isCustom. */
export function isCustomFieldSignal(m: SapTicketReferenceMatch): boolean {
  if (m.isCustom) {
    return true;
  }
  const raw = m.fieldName?.trim() ?? "";
  return /^(YY|ZZ)/i.test(raw);
}

export type SapReferenceMetadataSignals = {
  tables: Set<string>;
  sortedTables: string[];
  fieldNames: string[];
  customFieldNames: string[];
  /** Uppercase table key → best description seen */
  tableToDescription: Map<string, string>;
  /** Uppercase field name → best field description seen (stored metadata only) */
  fieldToDescription: Map<string, string>;
  modules: string[];
  businessObjects: string[];
  dataDomains: string[];
  hasCustomField: boolean;
  hasMaterialMasterBo: boolean;
  /**
   * Heuristic when fieldDescription suggests key/required/mandatory (no DTO key flag today).
   */
  hasKeyOrRequiredFieldHint: boolean;
  isPurchasingInfoRecordContext: boolean;
};

/**
 * Collects deterministic signals from stored SAP reference matches only.
 */
export function collectSapReferenceMetadataSignals(
  matches: SapTicketReferenceMatch[],
): SapReferenceMetadataSignals {
  const tables = new Set<string>();
  const fieldNameSet = new Set<string>();
  const customFieldNames: string[] = [];
  const customFieldSeen = new Set<string>();
  const tableToDescription = new Map<string, string>();
  const modules = new Set<string>();
  const businessObjects = new Set<string>();
  const dataDomains = new Set<string>();
  const fieldToDescription = new Map<string, string>();
  let hasCustomField = false;
  let hasMaterialMasterBo = false;
  let hasKeyOrRequiredFieldHint = false;
  let isPurchasingInfoRecordContext = false;

  for (const m of matches) {
    const t = normTable(m.tableName);
    if (t) {
      tables.add(t);
    }

    const f = m.fieldName?.trim();
    if (f) {
      fieldNameSet.add(f);
      const fk = f.toUpperCase();
      if (m.fieldDescription?.trim()) {
        const fd = m.fieldDescription.trim();
        const prev = fieldToDescription.get(fk);
        if (!prev || fd.length > prev.length) {
          fieldToDescription.set(fk, fd);
        }
        if (/\b(key|primary key|required|mandatory)\b/i.test(fd)) {
          hasKeyOrRequiredFieldHint = true;
        }
      }
    }

    if (isCustomFieldSignal(m)) {
      hasCustomField = true;
      if (f) {
        const key = f.toUpperCase();
        if (!customFieldSeen.has(key)) {
          customFieldSeen.add(key);
          customFieldNames.push(f);
        }
      }
    }

    if (t && m.tableDescription?.trim()) {
      const d = m.tableDescription.trim();
      const prev = tableToDescription.get(t);
      if (!prev || d.length > prev.length) {
        tableToDescription.set(t, d);
      }
    }

    if (t === "EINA" || t === "EINE") {
      isPurchasingInfoRecordContext = true;
    }

    if (m.module?.trim()) {
      modules.add(m.module.trim());
    }

    const bo = m.businessObject?.trim();
    if (bo) {
      businessObjects.add(bo);
      if (/material\s*master/i.test(bo)) {
        hasMaterialMasterBo = true;
      }
      if (/purchasing info record/i.test(bo)) {
        isPurchasingInfoRecordContext = true;
      }
    }

    if (m.dataDomain?.trim()) {
      dataDomains.add(m.dataDomain.trim());
    }
  }

  return {
    tables,
    sortedTables: [...tables].sort(),
    fieldNames: [...fieldNameSet].sort(),
    customFieldNames,
    tableToDescription,
    fieldToDescription,
    modules: [...modules].sort(),
    businessObjects: [...businessObjects].sort(),
    dataDomains: [...dataDomains].sort(),
    hasCustomField,
    hasMaterialMasterBo,
    hasKeyOrRequiredFieldHint,
    isPurchasingInfoRecordContext,
  };
}
