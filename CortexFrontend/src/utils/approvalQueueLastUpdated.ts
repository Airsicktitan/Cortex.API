/**
 * Human-readable labels for Approval Queue refresh status (no exact timestamps).
 */
export function formatApprovalQueueLastUpdatedLabel(
  lastSuccessMs: number,
  nowMs: number = Date.now(),
): string {
  const elapsedSec = Math.floor((nowMs - lastSuccessMs) / 1000);
  if (elapsedSec < 60) {
    return "Last updated just now";
  }
  const minutes = Math.floor(elapsedSec / 60);
  if (minutes === 1) {
    return "Last updated 1 min ago";
  }
  return `Last updated ${minutes} min ago`;
}
