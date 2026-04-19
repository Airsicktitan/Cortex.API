/** GET /api/metrics/snapshot — aggregated WorkflowMetricEvents (read-only). */
export interface WorkflowMetricsSnapshot {
  intakeAssistUsageCount: number;
  intakeAssistSavedCount: number;
  avgMissingDetailCount: number;
  reviewerSignalCounts: {
    ready: number;
    gaps: number;
    needs_detail: number;
  };
  screenshotInsightUsageCount: number;
  avgCommentCountBySignal: {
    ready: number;
    gaps: number;
    needs_detail: number;
  };
}
