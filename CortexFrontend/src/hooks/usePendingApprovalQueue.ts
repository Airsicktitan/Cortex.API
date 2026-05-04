import { useCallback, useEffect, useRef, useState } from "react";
import type { Ticket } from "../types/ticket";
import { getUserFacingErrorMessage, ticketService } from "../services/api";

const APPROVAL_QUEUE_POLL_MS = 25000;
const MIN_SILENT_REFRESH_INDICATOR_MS = 350;

interface UsePendingApprovalQueueParams {
  isAuthenticated: boolean;
  bootstrapComplete: boolean;
  needsConsent: boolean;
  /** When false, the hook does not auto-load or poll (used to defer fetches until the queue view is visited). */
  enabled: boolean;
  getApiToken: (providedToken?: string) => Promise<string>;
}

/**
 * Owns the pending-approval ticket list so both the Approval Queue page and
 * the Ticket Modal can mutate it through the same state. Approval/return/
 * reject actions update via {@link applyReviewedTicketToQueue} so the queue
 * card disappears immediately instead of waiting for the next poll.
 */
export function usePendingApprovalQueue({
  isAuthenticated,
  bootstrapComplete,
  needsConsent,
  enabled,
  getApiToken,
}: UsePendingApprovalQueueParams) {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [silentRefreshing, setSilentRefreshing] = useState(false);
  const [lastSuccessfulRefreshAt, setLastSuccessfulRefreshAt] = useState<
    number | null
  >(null);
  const silentRefreshDepthRef = useRef(0);
  const initialLoadCompletedRef = useRef(false);

  const ready = isAuthenticated && bootstrapComplete && !needsConsent;
  const active = ready && enabled;

  const refreshSilent = useCallback(async () => {
    if (!ready) {
      return;
    }

    silentRefreshDepthRef.current += 1;
    if (silentRefreshDepthRef.current === 1) {
      setSilentRefreshing(true);
    }
    const startedAt = Date.now();

    try {
      const token = await getApiToken();
      const data = await ticketService.getPendingApproval(token);
      setTickets(data.items ?? []);
      setError(null);
      setLastSuccessfulRefreshAt(Date.now());
    } catch {
      /* keep existing list; avoid noisy empty states on background refresh */
    } finally {
      silentRefreshDepthRef.current -= 1;
      if (silentRefreshDepthRef.current === 0) {
        const elapsed = Date.now() - startedAt;
        const delay = Math.max(0, MIN_SILENT_REFRESH_INDICATOR_MS - elapsed);
        window.setTimeout(() => {
          setSilentRefreshing(false);
        }, delay);
      }
    }
  }, [getApiToken, ready]);

  const loadInitial = useCallback(async () => {
    if (!ready) {
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const token = await getApiToken();
      const data = await ticketService.getPendingApproval(token);
      setTickets(data.items ?? []);
      setLastSuccessfulRefreshAt(Date.now());
      initialLoadCompletedRef.current = true;
    } catch (err) {
      setError(getUserFacingErrorMessage(err, "Unable to load approval queue."));
    } finally {
      setLoading(false);
    }
  }, [getApiToken, ready]);

  useEffect(() => {
    if (!active) {
      return;
    }
    if (initialLoadCompletedRef.current) {
      void refreshSilent();
      return;
    }
    void loadInitial();
  }, [active, loadInitial, refreshSilent]);

  useEffect(() => {
    if (!active) {
      return;
    }

    const onResume = () => {
      if (document.visibilityState === "visible") {
        void refreshSilent();
      }
    };

    document.addEventListener("visibilitychange", onResume);
    window.addEventListener("focus", onResume);
    const intervalId = window.setInterval(() => {
      void refreshSilent();
    }, APPROVAL_QUEUE_POLL_MS);

    return () => {
      document.removeEventListener("visibilitychange", onResume);
      window.removeEventListener("focus", onResume);
      window.clearInterval(intervalId);
    };
  }, [active, refreshSilent]);

  /**
   * Locally apply an approve/return/reject result without a re-fetch.
   * Tickets that are no longer PendingApproval are removed; PendingApproval
   * tickets (e.g. resubmissions arriving via realtime) are upserted so the
   * card reflects the latest server state.
   */
  const applyReviewedTicketToQueue = useCallback((updated: Ticket) => {
    setTickets((current) => {
      const exists = current.some((t) => t.id === updated.id);
      const isPending = updated.approvalStatus === "PendingApproval";
      if (!exists) {
        return isPending ? [updated, ...current] : current;
      }
      if (!isPending) {
        return current.filter((t) => t.id !== updated.id);
      }
      return current.map((t) => (t.id === updated.id ? updated : t));
    });
  }, []);

  return {
    pendingApprovalTickets: tickets,
    pendingApprovalLoading: loading,
    pendingApprovalError: error,
    pendingApprovalSilentRefreshing: silentRefreshing,
    pendingApprovalLastSuccessfulRefreshAt: lastSuccessfulRefreshAt,
    refreshPendingApprovalQueueSilent: refreshSilent,
    applyReviewedTicketToQueue,
  };
}
