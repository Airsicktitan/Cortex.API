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

/** Scans free-form ticket title/body for key/required readiness language. */
export function ticketBodySuggestsKeyOrRequired(text: string | null | undefined): boolean {
  if (!text?.trim()) {
    return false;
  }
  const t = text.toLowerCase();
  const patterns: RegExp[] = [
    /\bkey\b/,
    /\bprimary key\b/,
    /\brequired\b/,
    /\bmandatory\b/,
    /\brecord-identifying\b/,
    /\bidentifying the record\b/,
    /\brequired to identify\b/,
    /\brequired for identifying\b/,
  ];
  return patterns.some((p) => p.test(t));
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
   * Heuristic when match metadata or ticket body suggests key/required/mandatory language.
   */
  hasKeyOrRequiredFieldHint: boolean;
  /**
   * True when key/required hint comes only from ticket body, not from DTO metadata text.
   */
  keyOrRequiredHintFromTicketBodyOnly: boolean;
  isPurchasingInfoRecordContext: boolean;
};

/**
 * Collects deterministic signals from stored SAP reference matches plus optional ticket body text.
 */
export function collectSapReferenceMetadataSignals(
  matches: SapTicketReferenceMatch[],
  ticketBodyText?: string | null,
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
  let keyHintFromMetadata = false;
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
      }
    }

    const tracePieces = [m.reason, m.matchedText, m.fieldDescription, m.tableDescription]
      .filter(Boolean)
      .join(" ");
    if (/\b(key|primary key|required|mandatory)\b/i.test(tracePieces)) {
      keyHintFromMetadata = true;
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

  const keyHintFromTicketBody = ticketBodySuggestsKeyOrRequired(ticketBodyText);
  const hasKeyOrRequiredFieldHint = keyHintFromMetadata || keyHintFromTicketBody;
  const keyOrRequiredHintFromTicketBodyOnly =
    keyHintFromTicketBody && !keyHintFromMetadata;

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
    keyOrRequiredHintFromTicketBodyOnly,
    isPurchasingInfoRecordContext,
  };
}

/** True when matches exist but catalog-linked metadata on the DTO is effectively empty. */
export function hasMinimalSapReferenceDetails(
  sig: SapReferenceMetadataSignals,
  matchCount: number,
): boolean {
  if (matchCount === 0) {
    return false;
  }
  return (
    sig.sortedTables.length === 0 &&
    sig.fieldNames.length === 0 &&
    !sig.hasCustomField &&
    sig.businessObjects.length === 0 &&
    sig.modules.length === 0 &&
    sig.dataDomains.length === 0 &&
    sig.tableToDescription.size === 0
  );
}
