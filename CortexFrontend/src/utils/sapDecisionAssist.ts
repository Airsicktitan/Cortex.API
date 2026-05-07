import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import {
  collectSapReferenceMetadataSignals,
  hasMinimalSapReferenceDetails,
  isCustomFieldSignal,
  ticketBodySuggestsKeyOrRequired,
  type SapReferenceMetadataSignals,
} from "./sapReferenceMetadataSignals";

/** Advisory SAP / data governance assist for the Decision tab. */
export type SapDecisionAssist = {
/** How ready this appears for approval — not model confidence. */
  reviewReadinessLines: string[];
  /** Business-facing SAP scope (not raw “detection” wording). */
  dataContextLines: string[];
  /** Concrete checks before approval. */
  beforeApprovalLines: string[];
  /** Downstream / governance risk framing. */
  governanceConcernLines: string[];
  reviewerFocusLines: string[];
};

function pushDedupe(arr: string[], seen: Set<string>, ...items: string[]) {
  for (const item of items) {
    const t = item.trim();
    if (!t || seen.has(t)) {
      continue;
    }
    seen.add(t);
    arr.push(t);
  }
}

function dedupeMerge(segments: string[][]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const seg of segments) {
    for (const item of seg) {
      const t = item.trim();
      if (!t || seen.has(t)) {
        continue;
      }
      seen.add(t);
      out.push(t);
    }
  }
  return out;
}

const CAP_REVIEW = 2;
const CAP_CONTEXT = 2;
const CAP_BEFORE = 4;
const CAP_GOV = 2;
const CAP_FOCUS = 3;

/** Decision assist when ticket suggests SAP but no catalog match exists. */
export function buildSapIntentOnlyDecisionAssist(
  ticketBodyText?: string | null,
): SapDecisionAssist {
  const beforeApprovalLines = [
    "Provide SAP table, field, record keys, current and requested values, and business reason.",
    "Confirm downstream impact (reporting, integrations, compliance) once scope is known.",
  ];
  if (ticketBodySuggestsKeyOrRequired(ticketBodyText)) {
    beforeApprovalLines.push(
      "If required or key values are mentioned, confirm what is needed to identify the affected records.",
    );
  }
  return {
    reviewReadinessLines: [
      "SAP-related context is present, but catalog-linked table or field detail is missing — scope may need clarification before approval.",
    ],
    dataContextLines: [
      "SAP-related intake is present, but no catalog-linked table or field is available for reviewers yet.",
    ],
    beforeApprovalLines,
    governanceConcernLines: [
      "Missing record keys or unclear business meaning can delay approval, update the wrong records, or affect downstream reporting.",
    ],
    reviewerFocusLines: [
      "Intake / data governance reviewer",
      "SAP functional owner (after scope is documented)",
    ],
  };
}

function buildDataContextLines(sig: SapReferenceMetadataSignals): string[] {
  const lines: string[] = [];
  const seen = new Set<string>();
  const { tables, hasCustomField, isPurchasingInfoRecordContext, sortedTables } =
    sig;

  if (tables.has("MARC") && hasCustomField) {
    pushDedupe(
      lines,
      seen,
      "Plant-level material data with a custom SAP field.",
    );
  } else if (tables.has("MARC")) {
    pushDedupe(lines, seen, "Plant-level material data.");
  }

  if (tables.has("EINA") || tables.has("EINE") || isPurchasingInfoRecordContext) {
    pushDedupe(
      lines,
      seen,
      "Purchasing Info Record data for vendor, material, and purchasing-organization context.",
    );
  }

  if (tables.has("LFA1")) {
    pushDedupe(lines, seen, "Vendor master (general) data.");
  }

  if (tables.has("KNA1")) {
    pushDedupe(lines, seen, "Customer master (general) data.");
  }

  if (tables.has("MARA") && !tables.has("MARC")) {
    pushDedupe(lines, seen, "General material data.");
  }

  if (lines.length === 0 && sortedTables.length > 0) {
    const t = sortedTables[0];
    const desc = sig.tableToDescription.get(t);
    if (desc) {
      pushDedupe(
        lines,
        seen,
        `This request may involve data described in catalog context as: ${desc}`,
      );
    } else {
      pushDedupe(
        lines,
        seen,
        "SAP master or configuration data may be in scope based on the Cortex reference catalog.",
      );
    }
  } else if (lines.length === 0) {
    pushDedupe(
      lines,
      seen,
      "Catalog-linked SAP identifiers are limited; reviewers should confirm the exact object with the requester.",
    );
  }

  const firstCustom = sig.customFieldNames[0] ?? null;
  if (hasCustomField && firstCustom && !lines.some((l) => l.includes(firstCustom))) {
    pushDedupe(
      lines,
      seen,
      `Catalog context includes field ${firstCustom}; confirm business meaning before approval.`,
    );
  }

  return lines.slice(0, CAP_CONTEXT);
}

function appendBeforeApprovalTableRules(
  sig: SapReferenceMetadataSignals,
  segments: string[][],
) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC") && hasCustomField) {
    segments.push([
      "Confirm affected material numbers, plants, current value, requested value, and business meaning of the custom field.",
    ]);
  } else if (tables.has("MARC")) {
    segments.push([
      "Confirm affected material numbers and plants.",
      "Confirm material/plant extension scope.",
    ]);
  }

  if (tables.has("MARA")) {
    segments.push([
      "Confirm affected material numbers.",
      "Confirm general material master scope.",
    ]);
  }

  if (tables.has("LFA1")) {
    segments.push([
      "Confirm affected vendor account number, exact field, and current versus requested value.",
      "Confirm downstream purchasing, reporting, compliance, or integration impact.",
    ]);
  }

  if (tables.has("KNA1")) {
    segments.push([
      "Confirm affected customer account number, exact field, and current versus requested value.",
      "Confirm downstream sales, billing, reporting, compliance, or integration impact.",
    ]);
  }

  if (tables.has("EINA") || tables.has("EINE")) {
    segments.push([
      "Provide the purchasing info record number when available, or explain how the record is identified.",
      "Confirm vendor, material, purchasing organization, and plant (when relevant).",
      "Confirm whether purchasing conditions, source determination, or reporting could be affected.",
    ]);
  }

  if (tables.has("QMAT")) {
    segments.push([
      "Confirm affected material, plant, and inspection type (or relevant QM setup).",
    ]);
  }
}

function appendReviewerFocus(sig: SapReferenceMetadataSignals, segments: string[][]) {
  const { tables, hasCustomField } = sig;

  if (tables.has("MARC")) {
    segments.push([
      "Material Master / Supply Chain",
      "Plant-level material data owner",
    ]);
  }

  if (tables.has("MARA")) {
    segments.push(["Material Master owner"]);
  }

  if (tables.has("LFA1")) {
    segments.push(["Vendor Master owner"]);
  }

  if (tables.has("KNA1")) {
    segments.push(["Customer Master owner"]);
  }

  if (tables.has("MARC") && hasCustomField) {
    segments.push([
      "Mapping / transformation owner",
      "Validation or load rule owner",
    ]);
  }
}

/**
 * Metadata-driven decision support; table rules add secondary readiness/focus only.
 */
export function buildSapDecisionAssist(
  matches: SapTicketReferenceMatch[],
  ticketBodyText?: string | null,
): SapDecisionAssist | null {
  if (!matches.length) {
    return null;
  }

  const sig = collectSapReferenceMetadataSignals(matches, ticketBodyText);

  const reviewReadinessLines: string[] = [];
  const seenR = new Set<string>();
  const beforeSegments: string[][] = [];
  const focusSegments: string[][] = [];
  const governanceSegments: string[][] = [];

  if (hasMinimalSapReferenceDetails(sig, matches.length)) {
    pushDedupe(
      reviewReadinessLines,
      seenR,
      "Appears thin on SAP detail — table, field, or record scope may need clarification before approval.",
    );
    beforeSegments.push([
      "Provide SAP table and field names, affected records, and the requested change.",
    ]);
    governanceSegments.push([
      "Thin detail increases the risk of wrong scope or incorrect master data updates.",
    ]);
  } else {
    pushDedupe(
      reviewReadinessLines,
      seenR,
      "Appears reviewable once the checklist items below are confirmed.",
    );
  }

  let firstCustom = sig.customFieldNames[0] ?? null;
  if (!firstCustom) {
    for (const m of matches) {
      if (isCustomFieldSignal(m) && m.fieldName?.trim()) {
        firstCustom = m.fieldName.trim();
        break;
      }
    }
  }

  if (firstCustom) {
    beforeSegments.push([
      "Confirm the field exists in the project mapping or specification.",
      "Confirm whether the field is source-provided, transformed, defaulted, or SAP-maintained.",
    ]);
    focusSegments.push([
      "Mapping / transformation owner",
      "Validation or load rule owner",
    ]);
    governanceSegments.push([
      "Custom field meaning should be confirmed before approval because downstream reporting or integrations may depend on it.",
    ]);
  }

  if (sig.isPurchasingInfoRecordContext || sig.tables.has("EINA") || sig.tables.has("EINE")) {
    governanceSegments.push([
      "Purchasing Info Record changes may affect source determination, purchasing behavior, or reporting.",
    ]);
  }

  if (sig.hasKeyOrRequiredFieldHint) {
    if (sig.keyOrRequiredHintFromTicketBodyOnly) {
      governanceSegments.push([
        "Unclear identifying values may delay approval or lead to updating the wrong records.",
      ]);
      beforeSegments.push([
        "Ticket text suggests key or required values may matter for identifying records; confirm before approval.",
      ]);
    } else {
      governanceSegments.push([
        "Missing record keys may delay approval or cause the wrong records to be updated.",
      ]);
      beforeSegments.push([
        "Available metadata suggests key or required values may matter; confirm identifying values before approval.",
      ]);
    }
  }

  beforeSegments.push([
    "Confirm affected records and organizational scope.",
    "Confirm mapping and validation/load rule coverage.",
  ]);

  if (!sig.hasCustomField) {
    beforeSegments.push([
      "Confirm whether the issue may reflect missing source data, mapping, or failed validation.",
    ]);
  }

  for (const bo of sig.businessObjects.slice(0, 2)) {
    focusSegments.push([`${bo} data owner`]);
  }

  for (const mod of sig.modules.slice(0, 2)) {
    focusSegments.push([`${mod} process/data owner`]);
  }

  focusSegments.push([
    "Mapping / transformation owner",
    "Validation or load rule owner",
  ]);

  if (sig.hasMaterialMasterBo) {
    focusSegments.push(["Material Master / Supply Chain"]);
  }

  for (const d of sig.dataDomains.slice(0, 1)) {
    focusSegments.push([`${d} governance owner`]);
  }

  appendBeforeApprovalTableRules(sig, beforeSegments);
  appendReviewerFocus(sig, focusSegments);

  const dataContextLines = buildDataContextLines(sig);

  const beforeApprovalLines = dedupeMerge(beforeSegments).slice(0, CAP_BEFORE);
  const reviewerFocusLines = dedupeMerge(focusSegments).slice(0, CAP_FOCUS);
  const governanceConcernLines = dedupeMerge(governanceSegments).slice(
    0,
    CAP_GOV,
  );

  return {
    reviewReadinessLines: reviewReadinessLines.slice(0, CAP_REVIEW),
    dataContextLines,
    beforeApprovalLines,
    governanceConcernLines,
    reviewerFocusLines,
  };
}
