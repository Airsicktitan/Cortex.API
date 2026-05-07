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
    "If the ticket references required or key values, confirm what is needed to identify the affected records before approval.";
  const base: SapReviewerGuidance = {
    summaryLines: [
      "SAP-related wording is present, but reference context is not yet tied to a specific catalog object.",
      "Before approval, this request likely needs clearer scope from the requester.",
    ],
    questions: [
      "Which SAP table and field apply (if known)?",
      "Which records are affected (keys or examples)?",
      "What is the current value, requested value, and business reason for the change?",
      "Could this impact reporting, integrations, compliance, or downstream processing?",
    ],
    investigationPaths: [
      "Collect SAP identifiers and values before approval.",
      "After scope is clear, validate extract coverage, mapping, and load or validation rules.",
    ],
    ownershipHints: [
      "Intake / data governance reviewer",
      "SAP functional owner (once scope is documented)",
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
      return "plant-level material master";
    case "MARA":
      return "general material master";
    case "LFA1":
      return "vendor master";
    case "KNA1":
      return "customer master";
    case "EINA":
    case "EINE":
      return "purchasing info records";
    case "QMAT":
      return "quality inspection setup for materials";
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

/** Highest-priority domain when several SAP tables appear (single coherent checklist). */
function pickPrimaryGuidanceDomain(sig: SapReferenceMetadataSignals): string | null {
  if (sig.tables.has("MARC")) {
    return "MARC";
  }
  if (
    sig.tables.has("EINA") ||
    sig.tables.has("EINE") ||
    sig.isPurchasingInfoRecordContext
  ) {
    return "PURCHASE";
  }
  if (sig.tables.has("QMAT")) {
    return "QMAT";
  }
  if (sig.tables.has("MARA")) {
    return "MARA";
  }
  if (sig.tables.has("LFA1")) {
    return "LFA1";
  }
  if (sig.tables.has("KNA1")) {
    return "KNA1";
  }
  return null;
}

function primarySapContextSentence(sig: SapReferenceMetadataSignals): string | null {
  const { tables, hasCustomField, isPurchasingInfoRecordContext, businessObjects } = sig;

  if (tables.has("MARC")) {
    if (hasCustomField) {
      return "This appears to involve plant-level material master data, including a likely custom SAP field.";
    }
    return "This appears to involve plant-level material master data.";
  }

  if (tables.has("EINA") || tables.has("EINE") || isPurchasingInfoRecordContext) {
    return "This appears to involve a purchasing info record, which connects vendor and material purchasing details to purchasing organization or plant-specific context.";
  }

  if (tables.has("QMAT")) {
    return "This appears to involve quality inspection setup for a material and plant.";
  }

  if (tables.has("MARA")) {
    return "This appears to involve general material master data.";
  }

  if (tables.has("LFA1")) {
    return "This appears to involve vendor master data.";
  }

  if (tables.has("KNA1")) {
    return "This appears to involve customer master data.";
  }

  if (businessObjects.length === 1) {
    return `This appears to involve ${businessObjects[0]}, based on reference context.`;
  }

  if (sig.sortedTables.length > 0) {
    const t0 = sig.sortedTables[0];
    const phrase = tableGovernancePhrase(t0);
    const desc = sig.tableToDescription.get(t0);
    if (phrase) {
      return `This appears to relate to ${phrase}.`;
    }
    if (desc) {
      const short = desc.length > 160 ? `${desc.slice(0, 157)}…` : desc;
      return `This appears to involve SAP data described in the reference catalog: ${short}`;
    }
  }

  return null;
}

/**
 * Curated reviewer checks for the dominant SAP domain (keeps lists short and actionable).
 */
function buildIntrinsicSapReviewerChecks(
  sig: SapReferenceMetadataSignals,
): string[] {
  const domain = pickPrimaryGuidanceDomain(sig);
  const { tables, hasCustomField, customFieldNames } = sig;
  const cf = customFieldNames[0]?.trim();
  const out: string[] = [];

  switch (domain) {
    case "MARC":
      out.push("Confirm material and plant are identified.");
      if (hasCustomField) {
        if (cf) {
          out.push(
            `Confirm the meaning and owner of likely custom field ${cf}, including allowed values and downstream usage.`,
          );
        } else {
          out.push(
            "Confirm the meaning and owner of the custom SAP field, including allowed values and downstream usage.",
          );
        }
      } else {
        out.push("Confirm the affected field(s), current vs requested values, and the business reason.");
      }
      out.push("Check whether the change may affect planning, procurement, quality, or reporting.");
      out.push(
        "Confirm whether this is a governance change or a support correction, and who should approve.",
      );
      if (hasCustomField) {
        out.push("Confirm source-of-truth and approval ownership for the field.");
      }
      break;
    case "MARA":
      out.push(
        "Confirm the material number(s), affected field, data domain, and (when relevant) current vs requested values.",
      );
      out.push(
        "Confirm source of truth and whether impact is general master data or specific to certain downstream usage.",
      );
      out.push("Confirm the business reason if approval depends on scope or ownership.");
      break;
    case "PURCHASE":
      out.push("Confirm vendor and material are identified.");
      out.push(
        "Confirm purchasing organization and plant if org- or plant-level purchasing data (for example, EINE) is involved.",
      );
      out.push("Include the purchasing info record number if known.");
      out.push(
        "Confirm whether this is a data correction, mapping issue, or governance change.",
      );
      break;
    case "QMAT":
      out.push("Confirm material and plant are identified.");
      out.push("Confirm the inspection setup or inspection type being requested.");
      out.push("Check whether the issue may affect release, blocking, or inspection processing.");
      out.push("Confirm quality ownership before approval.");
      break;
    case "LFA1":
      out.push("Confirm the vendor or account affected.");
      out.push("Confirm the field, requested value, and business reason.");
      out.push("Check whether the field may have compliance, purchasing, or payment impact.");
      out.push("Confirm vendor data ownership before approval.");
      break;
    case "KNA1":
      out.push("Confirm the customer or account affected.");
      out.push("Confirm the field, requested value, and business reason.");
      out.push("Check whether the field may affect sales, billing, reporting, or compliance.");
      out.push("Confirm customer data ownership before approval.");
      break;
    default:
      break;
  }

  if (out.length === 0 && tables.size > 0) {
    out.push("Confirm the SAP object, affected records, and business reason before approval.");
  }

  const seen = new Set<string>();
  const deduped: string[] = [];
  for (const line of out) {
    const k = line.toLowerCase();
    if (!seen.has(k)) {
      seen.add(k);
      deduped.push(line);
    }
  }
  return deduped;
}

export function getSapGovernanceCardPrimaryContext(
  matches: SapTicketReferenceMatch[],
  ticketBodyText?: string | null,
): string | null {
  if (!matches.length) {
    return null;
  }
  const sig = collectSapReferenceMetadataSignals(matches, ticketBodyText);
  return primarySapContextSentence(sig);
}

export function getSapGovernanceCardReviewerChecks(
  matches: SapTicketReferenceMatch[],
  ticketBodyText?: string | null,
): string[] {
  if (!matches.length) {
    return [];
  }
  const sig = collectSapReferenceMetadataSignals(matches, ticketBodyText);
  return buildIntrinsicSapReviewerChecks(sig).slice(0, 4);
}

function hasTableDrivenScope(sig: SapReferenceMetadataSignals): boolean {
  return pickPrimaryGuidanceDomain(sig) !== null;
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
    hasMinimalSapReferenceDetails(sig, matchCount) ||
    buildIntrinsicSapReviewerChecks(sig).length > 0
  );
}

function buildMetadataSummaries(
  sig: SapReferenceMetadataSignals,
  seenS: Set<string>,
  summaryLines: string[],
) {
  const primary = primarySapContextSentence(sig);
  if (primary) {
    dedupePush(summaryLines, seenS, primary);
  }

  if (summaryLines.length > 0) {
    const t0 = sig.sortedTables[0];
    const desc = t0 ? sig.tableToDescription.get(t0) : null;
    if (desc) {
      const compact = desc.length > 140 ? `${desc.slice(0, 137)}…` : desc;
      const primaryLower = summaryLines.join(" ").toLowerCase();
      if (!primaryLower.includes(compact.slice(0, 24).toLowerCase())) {
        dedupePush(
          summaryLines,
          seenS,
          `Catalog note for ${t0}: ${compact}`,
        );
      }
    }
  }

  if (summaryLines.length === 0 && sig.sortedTables.length > 0) {
    const t0 = sig.sortedTables[0];
    const phrase = tableGovernancePhrase(t0);
    const tail =
      sig.sortedTables.length > 1
        ? ` Additional context may apply (${sig.sortedTables.length} tables referenced).`
        : "";
    if (phrase) {
      dedupePush(
        summaryLines,
        seenS,
        `This appears to relate to ${phrase}.${tail}`,
      );
    } else {
      dedupePush(
        summaryLines,
        seenS,
        `SAP reference context may apply for ${t0}; reviewers should confirm scope with an SME.${tail}`,
      );
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
    "Provide SAP table and field names if known.",
    "Provide affected record keys or representative examples.",
    "Describe current value, requested value, and business reason.",
    "Confirm whether this may affect reporting, integrations, compliance, or downstream processing.",
  );
}

function pushKeyAndRequiredGuidance(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  seenQ: Set<string>,
) {
  if (!sig.hasKeyOrRequiredFieldHint) {
    return;
  }

  const domain = pickPrimaryGuidanceDomain(sig);
  let example = "records";
  if (domain === "MARC" || (sig.tables.has("MARC"))) {
    example = "material and plant for plant-level data";
  } else if (domain === "PURCHASE") {
    example = "vendor, material, and purchasing organization for info records";
  } else if (domain === "QMAT") {
    example = "material, plant, and inspection setup";
  } else if (domain === "LFA1") {
    example = "vendor or account number";
  } else if (domain === "KNA1") {
    example = "customer or account number";
  }

  if (sig.keyOrRequiredHintFromTicketBodyOnly) {
    dedupePush(
      questions,
      seenQ,
      `The ticket suggests identifying keys may matter — confirm the values needed to scope the change (for example, ${example}).`,
    );
  } else {
    dedupePush(
      questions,
      seenQ,
      `The reference catalog suggests some fields may be required to identify records — confirm those identifiers before approval (for example, ${example}).`,
    );
  }

  if (questions.length < 6) {
    dedupePush(
      questions,
      seenQ,
      "Provide example records or source extracts when that would speed review.",
    );
  }
}

function pushPurchasingRecordHint(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  seenQ: Set<string>,
) {
  if (!sig.isPurchasingInfoRecordContext && !sig.tables.has("EINA") && !sig.tables.has("EINE")) {
    return;
  }
  if (questions.some((s) => /purchasing info record number/i.test(s))) {
    return;
  }
  dedupePush(
    questions,
    seenQ,
    "Include the purchasing info record number if known.",
  );
}

function pushCustomFieldExtras(
  sig: SapReferenceMetadataSignals,
  questions: string[],
  seenQ: Set<string>,
) {
  if (!sig.hasCustomField) {
    return;
  }
  const domain = pickPrimaryGuidanceDomain(sig);
  if (domain === "MARC") {
    return;
  }
  const cf = sig.customFieldNames[0]?.trim();
  dedupePush(
    questions,
    seenQ,
    cf
      ? `Treat ${cf} as a likely custom SAP field — confirm owner, meaning, allowed values, and downstream usage.`
      : "Treat the referenced field as a likely custom SAP field — confirm owner, meaning, allowed values, and downstream usage.",
  );
}

function buildMetadataQuestionsOrdered(
  sig: SapReferenceMetadataSignals,
  matchCount: number,
  seenQ: Set<string>,
  questions: string[],
) {
  pushWeakMatchClarifiers(sig, matchCount, questions, seenQ);

  for (const line of buildIntrinsicSapReviewerChecks(sig)) {
    dedupePush(questions, seenQ, line);
  }

  pushCustomFieldExtras(sig, questions, seenQ);
  pushPurchasingRecordHint(sig, questions, seenQ);
  pushKeyAndRequiredGuidance(sig, questions, seenQ);

  if (sig.businessObjects.length === 1 && !shouldOmitGenericWhichRecords(sig, matchCount)) {
    dedupePush(
      questions,
      seenQ,
      `Which ${sig.businessObjects[0]} records are affected?`,
    );
  } else if (sig.businessObjects.length > 1 && !shouldOmitGenericWhichRecords(sig, matchCount)) {
    dedupePush(
      questions,
      seenQ,
      `Which records are affected for: ${sig.businessObjects.slice(0, 3).join(", ")}?`,
    );
  } else if (!shouldOmitGenericWhichRecords(sig, matchCount)) {
    dedupePush(questions, seenQ, "Which records are affected?");
  }

  if (sig.dataDomains.length === 1 && questions.length < 7) {
    dedupePush(questions, seenQ, `What ${sig.dataDomains[0]} scope is affected?`);
  }

  if (questions.length < 6) {
    for (const mod of sig.modules.slice(0, 1)) {
      dedupePush(
        questions,
        seenQ,
        `Does this fall under the ${mod} process or data owner?`,
      );
    }
  }

  if (questions.length < 8) {
    dedupePush(
      questions,
      seenQ,
      "Is this likely missing data, mapping, or validation?",
    );
  }
}

function buildMetadataInvestigation(
  sig: SapReferenceMetadataSignals,
  seenP: Set<string>,
  investigationPaths: string[],
) {
  const domain = pickPrimaryGuidanceDomain(sig);

  switch (domain) {
    case "MARC":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate plant-level material mapping and extension scope in source and target.",
      );
      break;
    case "MARA":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate general material mapping and cross-plant consistency.",
      );
      break;
    case "PURCHASE":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate purchasing info record mapping and org/plant consistency.",
      );
      break;
    case "QMAT":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate quality material inspection setup and related rules.",
      );
      break;
    case "LFA1":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate vendor master mapping and dependent purchasing flows.",
      );
      break;
    case "KNA1":
      dedupePush(
        investigationPaths,
        seenP,
        "Validate customer master mapping and downstream distribution scope.",
      );
      break;
    default:
      dedupePush(
        investigationPaths,
        seenP,
        "Confirm extract coverage, mapping, and validation or load rules once scope is clear.",
      );
  }

  if (sig.hasCustomField && domain !== "MARC") {
    dedupePush(
      investigationPaths,
      seenP,
      "Confirm the custom field is documented in mapping or specification.",
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

  for (const bo of businessObjects.slice(0, 3)) {
    dedupePush(ownershipHints, seenO, `${bo} data owner`);
  }

  for (const mod of modules.slice(0, 2)) {
    dedupePush(ownershipHints, seenO, `${mod} process or data owner`);
  }

  for (const d of dataDomains.slice(0, 2)) {
    dedupePush(ownershipHints, seenO, `${d} governance owner`);
  }

  if (hasCustomField) {
    dedupePush(
      ownershipHints,
      seenO,
      "Field mapping / specification owner",
      "Validation or load rule owner",
    );
  }

  if (hasMaterialMasterBo || sig.tables.has("MARC") || sig.tables.has("MARA")) {
    dedupePush(ownershipHints, seenO, "Material master / supply chain");
  }

  if (sig.tables.has("LFA1")) {
    dedupePush(ownershipHints, seenO, "Vendor master / procurement");
  }

  if (sig.tables.has("KNA1")) {
    dedupePush(ownershipHints, seenO, "Customer master / sales");
  }
}

/**
 * Deterministic, read-only guidance from stored SAP reference matches only.
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
    summaryLines: summaryLines.slice(0, 3),
    questions: questions.slice(0, 12),
    investigationPaths,
    ownershipHints,
  };
}
