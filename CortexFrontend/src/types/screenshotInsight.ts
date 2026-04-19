/** Approver screenshot insight (vision). Advisory only. */
export interface ScreenshotInsightResult {
  summary: string;
  visibleDetails: string[];
  possibleIssues: string[];
  recommendedFollowUp: string[];
  unavailable?: boolean;
  unavailableReason?: string | null;
  /** Present when loaded from persisted ticket data (GET ticket). */
  analyzedAtUtc?: string;
  analyzedImageCount?: number;
  analyzedFileNames?: string[];
}

/** Persisted on the ticket (`screenshotInsight` on GET); mirrors API `ScreenshotInsightPersistedDto`. */
export interface ScreenshotInsightPersisted {
  source?: string;
  analyzedAtUtc?: string;
  analyzedImageCount?: number;
  analyzedFileNames?: string[];
  summary: string;
  visibleDetails: string[];
  possibleIssues: string[];
  recommendedFollowUp: string[];
}

export function screenshotInsightPersistedHasContent(
  insight: ScreenshotInsightPersisted | null | undefined,
): boolean {
  if (!insight) {
    return false;
  }
  return Boolean(
    insight.summary?.trim() ||
      (insight.visibleDetails?.length ?? 0) > 0 ||
      (insight.possibleIssues?.length ?? 0) > 0 ||
      (insight.recommendedFollowUp?.length ?? 0) > 0,
  );
}

export function persistedScreenshotInsightToResult(
  p: ScreenshotInsightPersisted | null | undefined,
): ScreenshotInsightResult | null {
  if (!p) {
    return null;
  }
  const hasBody =
    Boolean(p.summary?.trim()) ||
    (p.visibleDetails?.length ?? 0) > 0 ||
    (p.possibleIssues?.length ?? 0) > 0 ||
    (p.recommendedFollowUp?.length ?? 0) > 0;
  if (!hasBody) {
    return null;
  }
  return {
    summary: p.summary ?? "",
    visibleDetails: p.visibleDetails ?? [],
    possibleIssues: p.possibleIssues ?? [],
    recommendedFollowUp: p.recommendedFollowUp ?? [],
    unavailable: false,
    analyzedAtUtc: p.analyzedAtUtc,
    analyzedImageCount: p.analyzedImageCount,
    analyzedFileNames: p.analyzedFileNames,
  };
}
