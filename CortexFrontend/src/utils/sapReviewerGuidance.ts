import type { SapTicketReferenceMatch } from "../types/sapTicketReference";

export type SapReviewerGuidance = {
  summaryLines: string[];
  questions: string[];
  investigationPaths: string[];
  ownershipHints: string[];
};

function normTable(name: string | null | undefined): string {
  return (name ?? "").trim().toUpperCase();
}

/** YY/ZZ-style SAP customer extension fields plus catalog isCustom. */
function isCustomFieldSignal(m: SapTicketReferenceMatch): boolean {
  if (m.isCustom) {
    return true;
  }
  const raw = m.fieldName?.trim() ?? "";
  return /^(YY|ZZ)/i.test(raw);
}

function collectSignals(matches: SapTicketReferenceMatch[]) {
  const tables = new Set<string>();
  let hasCustomField = false;
  let hasMaterialMaster = false;
  const businessObjects = new Set<string>();

  for (const m of matches) {
    const t = normTable(m.tableName);
    if (t) {
      tables.add(t);
    }
    if (isCustomFieldSignal(m)) {
      hasCustomField = true;
    }
    const bo = m.businessObject?.trim();
    if (bo) {
      businessObjects.add(bo);
      if (/material\s*master/i.test(bo)) {
        hasMaterialMaster = true;
      }
    }
  }

  return {
    tables,
    hasCustomField,
    hasMaterialMaster,
    businessObjects,
  };
}

function dedupePush(arr: string[], seen: Set<string>, ...candidates: string[]) {
  for (const c of candidates) {
    const t = c.trim();
    if (!t || seen.has(t)) {
      continue;
    }
    seen.add(t);
    arr.push(t);
  }
}

/**
 * Deterministic, read-only guidance from stored SAP reference matches only.
 */
export function buildSapReviewerGuidance(
  matches: SapTicketReferenceMatch[],
): SapReviewerGuidance | null {
  if (!matches.length) {
    return null;
  }

  const {
    tables,
    hasCustomField,
    hasMaterialMaster,
    businessObjects,
  } = collectSignals(matches);

  const summaryLines: string[] = [];
  const questions: string[] = [];
  const investigationPaths: string[] = [];
  const ownershipHints: string[] = [];
  const seenQ = new Set<string>();
  const seenP = new Set<string>();
  const seenO = new Set<string>();
  const seenS = new Set<string>();

  const sortedTables = [...tables].sort();
  const tableHint = sortedTables[0];

  if (tables.has("MARC")) {
    dedupePush(
      summaryLines,
      seenS,
      "This ticket references MARC — plant-level material master data.",
    );
    dedupePush(
      questions,
      seenQ,
      "Which material numbers are affected?",
      "Which plant(s) are affected?",
      "Is the issue limited to specific material–plant combinations?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check plant-level material mapping and validation rules.",
      "Confirm material/plant extension scope in source and target.",
    );
  }

  if (tables.has("MARA")) {
    dedupePush(
      summaryLines,
      seenS,
      "This ticket references MARA — general material master data.",
    );
    dedupePush(
      questions,
      seenQ,
      "Which material numbers are affected?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check general material master mapping and cross-plant consistency.",
    );
  }

  if (tables.has("LFA1")) {
    dedupePush(
      summaryLines,
      seenS,
      "This ticket references LFA1 — vendor master general data.",
    );
    dedupePush(
      questions,
      seenQ,
      "Which vendor or account numbers are affected?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check vendor master general data mapping and IDoc/field coverage.",
    );
    dedupePush(ownershipHints, seenO, "Vendor master / procurement data owner");
  }

  if (tables.has("KNA1")) {
    dedupePush(
      summaryLines,
      seenS,
      "This ticket references KNA1 — customer master general data.",
    );
    dedupePush(
      questions,
      seenQ,
      "Which customer numbers are affected?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check customer master general data mapping and distribution channel scope.",
    );
    dedupePush(ownershipHints, seenO, "Customer master / sales data owner");
  }

  if (hasMaterialMaster && !tables.has("MARC") && !tables.has("MARA")) {
    dedupePush(
      summaryLines,
      seenS,
      "Detected references tie to Material Master (business object metadata).",
    );
    dedupePush(
      questions,
      seenQ,
      "Which material numbers are affected?",
      "What is the plant, sales org, or distribution scope?",
      "Is this general material data or plant-specific material data?",
    );
  }

  if (hasCustomField) {
    let customDisplay: string | null = null;
    for (const m of matches) {
      if (isCustomFieldSignal(m) && m.fieldName?.trim()) {
        customDisplay = m.fieldName.trim();
        break;
      }
    }

    if (customDisplay) {
      dedupePush(
        summaryLines,
        seenS,
        `${customDisplay} appears to be a custom or extension SAP field.`,
      );
    } else {
      dedupePush(
        summaryLines,
        seenS,
        "At least one custom or extension SAP field is referenced.",
      );
    }
    dedupePush(
      questions,
      seenQ,
      "Is the field maintained in SAP, provided by the source, or derived in transformation?",
      "Should this field exist in the project mapping or specification?",
      "Do validation or load rules require this field?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Confirm the field exists in the project mapping/specification.",
      "Check whether the source extract includes this field.",
      "Check transformation/mapping for this field.",
      "Check validation and load rules that reference this field.",
    );
    dedupePush(
      ownershipHints,
      seenO,
      "Data migration / field mapping owner",
      "Validation or load rule owner",
    );
  }

  if (sortedTables.length > 0 && summaryLines.length === 0 && tableHint) {
    const tail = sortedTables.length > 1 ? ` (${sortedTables.length} tables)` : "";
    dedupePush(
      summaryLines,
      seenS,
      `This ticket references SAP table ${tableHint}${tail} metadata.`,
    );
  }

  if (businessObjects.size > 0 && summaryLines.length === 0) {
    const [firstBo] = [...businessObjects].sort();
    dedupePush(
      summaryLines,
      seenS,
      `Cortex matched SAP reference metadata (business object: ${firstBo}).`,
    );
  }

  // Generic fallbacks when little table signal
  dedupePush(
    questions,
    seenQ,
    "Is the issue missing data, missing mapping, or failed validation?",
  );
  dedupePush(
    investigationPaths,
    seenP,
    "Check source extract field coverage.",
    "Check end-to-end mapping and transformation logic.",
  );

  if (hasMaterialMaster || tables.has("MARC") || tables.has("MARA")) {
    dedupePush(ownershipHints, seenO, "Material Master / supply chain");
  }

  // Caps — keep panel concise
  const cap = (a: string[], n: number) => a.slice(0, n);

  return {
    summaryLines: cap(summaryLines, 5),
    questions: cap(questions, 8),
    investigationPaths: cap(investigationPaths, 6),
    ownershipHints: cap(ownershipHints, 4),
  };
}
