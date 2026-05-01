import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import {
  collectSapReferenceMetadataSignals,
  type SapReferenceMetadataSignals,
} from "./sapReferenceMetadataSignals";

export type SapReviewerGuidance = {
  summaryLines: string[];
  questions: string[];
  investigationPaths: string[];
  ownershipHints: string[];
};

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

/** Secondary scope questions and investigation paths for well-known tables. */
function applyTableRefinements(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  investigationPaths: string[],
  seenQ: Set<string>,
  seenP: Set<string>,
) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC")) {
    dedupePush(
      questions,
      seenQ,
      "Which materials and plants are affected?",
    );
    if (!hasCustomField) {
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

  if (tables.has("MARA")) {
    dedupePush(questions, seenQ, "Which material numbers are affected?");
    dedupePush(
      investigationPaths,
      seenP,
      "Check general material mapping and cross-plant consistency.",
    );
  }

  if (tables.has("LFA1")) {
    dedupePush(questions, seenQ, "Which vendors or accounts are affected?");
    dedupePush(
      investigationPaths,
      seenP,
      "Check vendor master mapping and IDoc/field coverage.",
    );
  }

  if (tables.has("KNA1")) {
    dedupePush(questions, seenQ, "Which customer numbers are affected?");
    dedupePush(
      investigationPaths,
      seenP,
      "Check customer master mapping and distribution scope.",
    );
  }

  if (tables.has("EINA") || tables.has("EINE")) {
    dedupePush(
      questions,
      seenQ,
      "Confirm supplier, material, and purchasing organization scope (if applicable).",
    );
    dedupePush(
      investigationPaths,
      seenP,
      "Check purchasing info record mapping and org-level consistency.",
    );
  }

  if (tables.has("QMAT")) {
    dedupePush(
      questions,
      seenQ,
      "Confirm material, plant, and inspection type scope.",
    );
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
  } = sig;

  if (hasCustomField) {
    const name = customFieldNames[0];
    if (name) {
      dedupePush(
        summaryLines,
        seenS,
        `${name} appears to be a custom SAP field.`,
      );
    } else {
      dedupePush(
        summaryLines,
        seenS,
        "A custom or extension SAP field is referenced.",
      );
    }
  }

  if (businessObjects.length === 1) {
    dedupePush(
      summaryLines,
      seenS,
      `This ticket references ${businessObjects[0]} data.`,
    );
  } else if (businessObjects.length > 1) {
    const joined = businessObjects.slice(0, 4).join(", ");
    const more =
      businessObjects.length > 4 ? ` (+${businessObjects.length - 4} more)` : "";
    dedupePush(
      summaryLines,
      seenS,
      `This ticket references ${joined} data.${more}`,
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
      dedupePush(summaryLines, seenS, `${t} — ${desc}.`);
      tableDescLines++;
    }
  }

  const modPart =
    modules.length > 0 ? `Module: ${modules.join(", ")}` : "";
  const domPart =
    dataDomains.length > 0 ? `domain: ${dataDomains.join(", ")}` : "";
  if (modPart && domPart) {
    dedupePush(summaryLines, seenS, `${modPart}; ${domPart}.`);
  } else if (modPart) {
    dedupePush(summaryLines, seenS, `${modPart}.`);
  } else if (domPart) {
    dedupePush(summaryLines, seenS, `Data domain: ${dataDomains.join(", ")}.`);
  }

  const hasTableNamed = sortedTables.length > 0;
  if (summaryLines.length === 0 && hasTableNamed) {
    const t0 = sortedTables[0];
    const tail = sortedTables.length > 1 ? ` (${sortedTables.length} tables)` : "";
    dedupePush(
      summaryLines,
      seenS,
      `SAP table ${t0}${tail} matched stored reference metadata.`,
    );
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
      dedupePush(
        summaryLines,
        seenS,
        `SAP table ${t0} matched stored reference metadata.`,
      );
    }
  }
}

function buildMetadataQuestions(
  sig: SapReferenceMetadataSignals,
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
  } = sig;

  if (hasCustomField) {
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
      "Confirm affected info record numbers.",
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
  } else {
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

  if (hasKeyOrRequiredFieldHint) {
    dedupePush(
      questions,
      seenQ,
      "Is the key or required field populated in source and target?",
    );
    dedupePush(
      questions,
      seenQ,
      "Is validation failing because this field is blank or invalid?",
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
): SapReviewerGuidance | null {
  if (!matches.length) {
    return null;
  }

  const sig = collectSapReferenceMetadataSignals(matches);

  const summaryLines: string[] = [];
  const questions: string[] = [];
  const investigationPaths: string[] = [];
  const ownershipHints: string[] = [];
  const seenS = new Set<string>();
  const seenQ = new Set<string>();
  const seenP = new Set<string>();
  const seenO = new Set<string>();

  buildMetadataSummaries(sig, seenS, summaryLines);

  buildMetadataQuestions(sig, seenQ, questions);

  buildMetadataInvestigation(sig, seenP, investigationPaths);

  applyTableRefinements(sig, questions, investigationPaths, seenQ, seenP);

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
