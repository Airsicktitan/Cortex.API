import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import {
  collectSapReferenceMetadataSignals,
  hasMinimalSapReferenceDetails,
  ticketBodySuggestsKeyOrRequired,
  type SapReferenceMetadataSignals,
} from "./sapReferenceMetadataSignals";

export type SapReviewerGuidance = {
  summaryLines: string[];
  questions: string[];
  investigationPaths: string[];
  ownershipHints: string[];
};

/** Intake-only guidance when ticket text suggests SAP but no catalog match exists. */
export function buildSapIntentOnlyReviewerGuidance(
  ticketBodyText?: string | null,
): SapReviewerGuidance {
  const keyHintFromTicketBody =
    "The available information suggests required or key values may be needed to identify the affected records. Confirm those values before approval.";
  const base: SapReviewerGuidance = {
    summaryLines: [
      "This request is not ready for approval because the SAP table, field, affected records, current value, requested value, and business reason are missing.",
    ],
    questions: [
      "Provide the SAP table and field name.",
      "Provide the affected record keys or example records.",
      "Explain the current value, requested value, and business reason for the change.",
      "Confirm whether this impacts reporting, integrations, compliance, or downstream processing.",
    ],
    investigationPaths: [
      "Collect SAP identifiers (table, field, and keys) and values before approval.",
      "Once details are confirmed, validate source extract coverage and mapping or validation rules.",
    ],
    ownershipHints: [
      "Intake / data governance reviewer",
      "SAP functional owner (after scope is documented)",
    ],
  };
  if (ticketBodySuggestsKeyOrRequired(ticketBodyText)) {
    return {
      ...base,
      questions: [...base.questions, keyHintFromTicketBody],
    };
  }
  return base;
}

function tableGovernancePhrase(table: string): string | null {
  switch (table) {
    case "MARC":
      return "Plant-level material master data may be in scope.";
    case "MARA":
      return "General material master data may be in scope.";
    case "LFA1":
      return "Vendor master data may be in scope.";
    case "KNA1":
      return "Customer master data may be in scope.";
    case "EINA":
    case "EINE":
      return "Purchasing Info Record data may be in scope.";
    case "QMAT":
      return "Quality management material data may be in scope.";
    default:
      return null;
  }
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

function hasTableDrivenScope(sig: SapReferenceMetadataSignals): boolean {
  const { tables } = sig;
  for (const t of [
    "MARC",
    "MARA",
    "LFA1",
    "KNA1",
    "EINA",
    "EINE",
    "QMAT",
  ] as const) {
    if (tables.has(t)) {
      return true;
    }
  }
  return false;
}

function shouldOmitGenericWhichRecords(
  sig: SapReferenceMetadataSignals,
  matchCount: number,
): boolean {
  return (
    sig.businessObjects.length > 0 ||
    hasTableDrivenScope(sig) ||
    sig.isPurchasingInfoRecordContext ||
    sig.hasCustomField ||
    hasMinimalSapReferenceDetails(sig, matchCount)
  );
}

function appendTableRefinementQuestions(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  seenQ: Set<string>,
) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC")) {
    dedupePush(
      questions,
      seenQ,
      "Confirm the affected material numbers and plants.",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm the current and requested values when known.",
    );
    if (!hasCustomField) {
      dedupePush(
        questions,
        seenQ,
        "Is scope limited to specific material–plant combinations?",
      );
    }
  }

  if (tables.has("MARA")) {
    dedupePush(questions, seenQ, "Which material numbers are affected?");
  }

  if (tables.has("LFA1")) {
    dedupePush(questions, seenQ, "Confirm the affected vendor account number.");
    dedupePush(
      questions,
      seenQ,
      "Confirm the exact field and the current versus requested value.",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm whether this change affects purchasing, reporting, compliance, or integrations.",
    );
  }

  if (tables.has("KNA1")) {
    dedupePush(questions, seenQ, "Confirm the affected customer account number.");
    dedupePush(
      questions,
      seenQ,
      "Confirm the exact field and the current versus requested value.",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm whether this change affects reporting, compliance, sales, billing, or integrations.",
    );
  }

  if (tables.has("EINA") || tables.has("EINE")) {
    dedupePush(
      questions,
      seenQ,
      "Confirm the affected vendor, material, purchasing organization, and plant (when relevant).",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm whether the change affects purchasing conditions, source determination, or reporting.",
    );
  }

  if (tables.has("QMAT")) {
    dedupePush(
      questions,
      seenQ,
      "Confirm the affected material, plant, and inspection type (or relevant QM setup).",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm whether the change affects inspection planning, release, or downstream quality processing.",
    );
  }
}

function appendTableRefinementInvestigation(
  sig: SapReferenceMetadataSignals,
  investigationPaths: string[],
  seenP: Set<string>,
) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC")) {
    if (!hasCustomField) {
      dedupePush(
        investigationPaths,
        seenP,
        "Check plant-level mapping and validation/load rules.",
        "Confirm material/plant extension scope in source and target.",
      );
    }
  }

  if (tables.has("MARA")) {
    dedupePush(
      investigationPaths,
      seenP,
      "Check general material mapping and cross-plant consistency.",
    );
  }

  if (tables.has("LFA1")) {
    dedupePush(
      investigationPaths,
      seenP,
      "Check vendor master mapping and IDoc/field coverage.",
    );
  }

  if (tables.has("KNA1")) {
    dedupePush(
      investigationPaths,
      seenP,
      "Check customer master mapping and distribution scope.",
    );
  }

  if (tables.has("EINA") || tables.has("EINE")) {
    dedupePush(
      investigationPaths,
      seenP,
      "Check purchasing info record mapping and org-level consistency.",
    );
  }

  if (tables.has("QMAT")) {
    dedupePush(
      investigationPaths,
      seenP,
      "Check quality management material data mapping and inspection rules.",
    );
  }
}

function applyMarcCustomFollowup(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  investigationPaths: string[],
  seenQ: Set<string>,
  seenP: Set<string>,
) {
  const { tables, hasCustomField } = sig;
  if (tables.has("MARC") && hasCustomField) {
    dedupePush(
      questions,
      seenQ,
      "Is scope limited to specific material–plant combinations?",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check plant-level mapping and validation/load rules.",
      "Confirm material/plant extension scope in source and target.",
    );
  }
}

function buildMetadataSummaries(
  sig: SapReferenceMetadataSignals,
  seenS: Set<string>,
  summaryLines: string[],
) {
  const {
    sortedTables,
    businessObjects,
    customFieldNames,
    hasCustomField,
    tableToDescription,
    modules,
    dataDomains,
    tables,
  } = sig;

  if (tables.has("MARC") && hasCustomField) {
    dedupePush(
      summaryLines,
      seenS,
      "This request appears to involve plant-level material data and a custom SAP field.",
    );
  } else if (hasCustomField) {
    const name = customFieldNames[0];
    if (name) {
      dedupePush(
        summaryLines,
        seenS,
        `This request appears to reference a custom SAP extension field (for example, ${name}).`,
      );
    } else {
      dedupePush(
        summaryLines,
        seenS,
        "This request appears to reference a custom or extension SAP field.",
      );
    }
  }

  if (
    tables.has("EINA") ||
    tables.has("EINE") ||
    sig.isPurchasingInfoRecordContext
  ) {
    dedupePush(
      summaryLines,
      seenS,
      "This request appears to involve Purchasing Info Record data for vendor, material, and purchasing-organization context.",
    );
  }

  if (
    tables.has("LFA1") &&
    !tables.has("MARC") &&
    !tables.has("MARA") &&
    !tables.has("KNA1")
  ) {
    dedupePush(
      summaryLines,
      seenS,
      "This request appears to involve vendor master data.",
    );
  }

  if (
    tables.has("KNA1") &&
    !tables.has("MARC") &&
    !tables.has("MARA") &&
    !tables.has("LFA1")
  ) {
    dedupePush(
      summaryLines,
      seenS,
      "This request appears to involve customer master data.",
    );
  }

  if (businessObjects.length === 1) {
    dedupePush(
      summaryLines,
      seenS,
      `This request appears to involve ${businessObjects[0]}.`,
    );
  } else if (businessObjects.length > 1) {
    const joined = businessObjects.slice(0, 4).join(", ");
    const more =
      businessObjects.length > 4 ? ` (+${businessObjects.length - 4} more)` : "";
    dedupePush(
      summaryLines,
      seenS,
      `This request appears to involve: ${joined}.${more}`,
    );
  }

  let tableDescLines = 0;
  const maxTableDesc = 2;
  for (const t of sortedTables) {
    if (tableDescLines >= maxTableDesc) {
      break;
    }
    const desc = tableToDescription.get(t);
    if (desc) {
      dedupePush(
        summaryLines,
        seenS,
        `Supporting catalog description for ${t}: ${desc}.`,
      );
      tableDescLines++;
    }
  }

  const modPart =
    modules.length > 0 ? `Process/module context: ${modules.join(", ")}` : "";
  const domPart =
    dataDomains.length > 0 ? `Data domain: ${dataDomains.join(", ")}` : "";
  if (modPart && domPart) {
    dedupePush(summaryLines, seenS, `${modPart}. ${domPart}.`);
  } else if (modPart) {
    dedupePush(summaryLines, seenS, `${modPart}.`);
  } else if (domPart) {
    dedupePush(summaryLines, seenS, `${domPart}.`);
  }

  const hasTableNamed = sortedTables.length > 0;
  if (summaryLines.length === 0 && hasTableNamed) {
    const t0 = sortedTables[0];
    const phrase = tableGovernancePhrase(t0);
    const tail = sortedTables.length > 1 ? ` Additional tables may apply (${sortedTables.length} total).` : "";
    if (phrase) {
      dedupePush(summaryLines, seenS, `This request suggests ${phrase}${tail}`);
    } else {
      const desc = tableToDescription.get(t0);
      if (desc) {
        dedupePush(
          summaryLines,
          seenS,
          `This request may involve data described in catalog context as: ${desc}.${tail}`,
        );
      } else {
        dedupePush(
          summaryLines,
          seenS,
          `SAP reference catalog context may apply; confirm the governing object with an SME.${tail}`,
        );
      }
    }
  } else if (summaryLines.length > 0 && hasTableNamed) {
    let mentionedTable = false;
    const upperSummaries = summaryLines.join(" ").toUpperCase();
    for (const t of sortedTables) {
      if (upperSummaries.includes(t)) {
        mentionedTable = true;
        break;
      }
    }
    if (!mentionedTable) {
      const t0 = sortedTables[0];
      const phrase = tableGovernancePhrase(t0);
      if (phrase) {
        dedupePush(summaryLines, seenS, `This request suggests ${phrase}`);
      } else {
        const desc = tableToDescription.get(t0);
        if (desc) {
          dedupePush(
            summaryLines,
            seenS,
            `Supporting catalog description for ${t0}: ${desc}.`,
          );
        }
      }
    }
  }
}

function pushWeakMatchClarifiers(
  sig: SapReferenceMetadataSignals,
  matchCount: number,
  questions: string[],
  seenQ: Set<string>,
) {
  if (!hasMinimalSapReferenceDetails(sig, matchCount)) {
    return;
  }
  dedupePush(
    questions,
    seenQ,
    "Provide the SAP table and field name if they are known.",
  );
  dedupePush(
    questions,
    seenQ,
    "Provide affected record keys or representative examples.",
  );
  dedupePush(
    questions,
    seenQ,
    "Describe the current value, requested value, and business reason for the change.",
  );
  dedupePush(
    questions,
    seenQ,
    "Confirm whether this affects reporting, integrations, compliance, or downstream processing.",
  );
}

function buildMetadataQuestionsOrdered(
  sig: SapReferenceMetadataSignals,
  matchCount: number,
  seenQ: Set<string>,
  questions: string[],
) {
  const {
    businessObjects,
    dataDomains,
    modules,
    hasCustomField,
    isPurchasingInfoRecordContext,
    hasKeyOrRequiredFieldHint,
    keyOrRequiredHintFromTicketBodyOnly,
  } = sig;

  pushWeakMatchClarifiers(sig, matchCount, questions, seenQ);

  if (hasCustomField) {
    const cf = sig.customFieldNames[0];
    dedupePush(
      questions,
      seenQ,
      "Is the field SAP-maintained, source-provided, or derived?",
    );
    dedupePush(
      questions,
      seenQ,
      "Should this field appear in the project mapping or specification?",
    );
    if (cf) {
      dedupePush(
        questions,
        seenQ,
        `Confirm the business meaning of custom field ${cf} with documentation or SMEs; avoid assuming meaning from the name alone.`,
      );
    }
    dedupePush(
      questions,
      seenQ,
      "Confirm whether this field matters for routing, reporting, or downstream processing.",
    );
    dedupePush(
      questions,
      seenQ,
      "Do validation or load rules depend on this field?",
    );
  }

  if (isPurchasingInfoRecordContext) {
    dedupePush(
      questions,
      seenQ,
      "Provide the purchasing info record number if available.",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm affected info record numbers or how records are identified.",
    );
  }

  appendTableRefinementQuestions(sig, questions, seenQ);

  if (hasKeyOrRequiredFieldHint) {
    if (keyOrRequiredHintFromTicketBodyOnly) {
      dedupePush(
        questions,
        seenQ,
        "The available information suggests required or key values may be needed to identify the affected records. Confirm those values before approval.",
      );
    } else {
      dedupePush(
        questions,
        seenQ,
        "The available metadata suggests this field may be required to identify or process the record. Confirm the required or key values before approval.",
      );
    }
    dedupePush(
      questions,
      seenQ,
      "Provide example records or source values so reviewers can confirm scope.",
    );
    dedupePush(
      questions,
      seenQ,
      "Confirm whether missing required values block approval or downstream processing.",
    );
  }

  if (businessObjects.length === 1) {
    dedupePush(
      questions,
      seenQ,
      `Which ${businessObjects[0]} records are affected?`,
    );
  } else if (businessObjects.length > 1) {
    dedupePush(
      questions,
      seenQ,
      `Which records are affected for: ${businessObjects.slice(0, 3).join(", ")}?`,
    );
  } else if (!shouldOmitGenericWhichRecords(sig, matchCount)) {
    dedupePush(questions, seenQ, "Which records are affected?");
  }

  if (dataDomains.length === 1) {
    dedupePush(
      questions,
      seenQ,
      `What ${dataDomains[0]} scope is affected?`,
    );
  } else if (dataDomains.length > 1) {
    dedupePush(
      questions,
      seenQ,
      `What scope is affected across domains: ${dataDomains.slice(0, 3).join(", ")}?`,
    );
  }

  for (const mod of modules.slice(0, 2)) {
    dedupePush(
      questions,
      seenQ,
      `Does this belong to the ${mod} process owner or data owner?`,
    );
  }

  dedupePush(
    questions,
    seenQ,
    "Is this missing data, mapping, or validation?",
  );
}

function buildMetadataInvestigation(
  sig: SapReferenceMetadataSignals,
  seenP: Set<string>,
  investigationPaths: string[],
) {
  const { businessObjects, dataDomains, modules, hasCustomField } = sig;

  dedupePush(
    investigationPaths,
    seenP,
    "Confirm source extract field coverage.",
    "Check mapping/transformation logic.",
    "Check validation/load rules.",
  );

  if (hasCustomField) {
    dedupePush(
      investigationPaths,
      seenP,
      "Confirm the custom field exists in the project mapping or specification.",
    );
  }

  for (const bo of businessObjects.slice(0, 2)) {
    dedupePush(
      investigationPaths,
      seenP,
      `Review ownership and rules for ${bo} data.`,
    );
  }

  for (const d of dataDomains.slice(0, 2)) {
    dedupePush(
      investigationPaths,
      seenP,
      `Confirm scope and rules for ${d}.`,
    );
  }

  for (const mod of modules.slice(0, 2)) {
    dedupePush(
      investigationPaths,
      seenP,
      `Confirm process/data owner for ${mod}.`,
    );
  }
}

function buildMetadataOwnership(
  sig: SapReferenceMetadataSignals,
  seenO: Set<string>,
  ownershipHints: string[],
) {
  const {
    businessObjects,
    modules,
    dataDomains,
    hasCustomField,
    hasMaterialMasterBo,
  } = sig;

  for (const bo of businessObjects) {
    dedupePush(ownershipHints, seenO, `${bo} data owner`);
  }

  for (const mod of modules) {
    dedupePush(ownershipHints, seenO, `${mod} process/data owner`);
  }

  for (const d of dataDomains) {
    dedupePush(ownershipHints, seenO, `${d} governance owner`);
  }

  if (hasCustomField) {
    dedupePush(
      ownershipHints,
      seenO,
      "Data migration / field mapping owner",
      "Validation or load rule owner",
    );
  }

  if (
    hasMaterialMasterBo ||
    sig.tables.has("MARC") ||
    sig.tables.has("MARA")
  ) {
    dedupePush(ownershipHints, seenO, "Material Master / Supply Chain");
  }

  if (sig.tables.has("LFA1")) {
    dedupePush(ownershipHints, seenO, "Vendor master / procurement data owner");
  }

  if (sig.tables.has("KNA1")) {
    dedupePush(ownershipHints, seenO, "Customer master / sales data owner");
  }
}

/**
 * Deterministic, read-only guidance from stored SAP reference matches only.
 * Metadata-driven first; table-specific rules are secondary refinements.
 */
export function buildSapReviewerGuidance(
  matches: SapTicketReferenceMatch[],
  ticketBodyText?: string | null,
): SapReviewerGuidance | null {
  if (!matches.length) {
    return null;
  }

  const sig = collectSapReferenceMetadataSignals(matches, ticketBodyText);

  const summaryLines: string[] = [];
  const questions: string[] = [];
  const investigationPaths: string[] = [];
  const ownershipHints: string[] = [];
  const seenS = new Set<string>();
  const seenQ = new Set<string>();
  const seenP = new Set<string>();
  const seenO = new Set<string>();

  buildMetadataSummaries(sig, seenS, summaryLines);

  buildMetadataQuestionsOrdered(sig, matches.length, seenQ, questions);

  buildMetadataInvestigation(sig, seenP, investigationPaths);

  appendTableRefinementInvestigation(sig, investigationPaths, seenP);

  applyMarcCustomFollowup(sig, questions, investigationPaths, seenQ, seenP);

  const { hasMaterialMasterBo, tables, businessObjects } = sig;

  if (
    hasMaterialMasterBo &&
    !tables.has("MARC") &&
    !tables.has("MARA") &&
    businessObjects.length > 0
  ) {
    dedupePush(
      questions,
      seenQ,
      "Is this general material data or plant-specific data?",
    );
  }

  buildMetadataOwnership(sig, seenO, ownershipHints);

  return {
    summaryLines,
    questions,
    investigationPaths,
    ownershipHints,
  };
}
