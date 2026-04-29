import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { RoutingLivePreviewInput } from "./TicketRoutingInsight";
import type { CortexInsight } from "../types/cortexInsight";
import type { CortexSlaRisk } from "../types/cortexRisk";
import { ticketService } from "../services/api";
import { deriveHistoricalContextFromInsight } from "../utils/cortexHistoricalContext";
import TicketRoutingInsight from "./TicketRoutingInsight";
import CortexRiskPanel from "./CortexRiskPanel";
import CortexInsightPanel from "./CortexInsightPanel";
import { ScrollToBottomButton } from "./ui/ScrollToBottomButton";

const API_AUDIENCE = "https://cortex-api";

type CortexTab = "review" | "decision" | "risk" | "insight";

const TAB_LABELS: Record<CortexTab, string> = {
  review: "Review",
  decision: "Decision",
  risk: "Risk",
  insight: "Insight",
};

const ALL_TABS: CortexTab[] = ["review", "decision", "risk", "insight"];

export interface CortexTabbedPanelProps {
  ticket: Ticket;
  isModalOpen: boolean;
  ticketBoards?: TicketBoardDefinition[];
  livePreview?: RoutingLivePreviewInput | null;
  riskLevel?: "Low" | "Medium" | "High" | null;
  onRiskReady?: (risk: CortexSlaRisk | null) => void;
  onOpenSourceTicket?: (ticketId: string) => void | Promise<void>;
  onReassignmentApplied?: (updatedTicket: Ticket) => void;
  reviewSlot?: ReactNode;
}

export function CortexTabbedPanel({
  ticket,
  isModalOpen,
  ticketBoards,
  livePreview,
  riskLevel,
  onRiskReady,
  onOpenSourceTicket,
  onReassignmentApplied,
  reviewSlot,
}: CortexTabbedPanelProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [activeTab, setActiveTab] = useState<CortexTab>("review");
  const [visited, setVisited] = useState<ReadonlySet<CortexTab>>(
    new Set<CortexTab>(["review"]),
  );
  const [loadedInsightState, setLoadedInsightState] = useState<{
    ticketId: string;
    insight: CortexInsight | null;
  } | null>(null);
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
  const cacheHydratedTicketIdRef = useRef<string | null>(null);
  const cacheHydrationAbortRef = useRef<AbortController | null>(null);

  const loadedInsight =
    loadedInsightState?.ticketId === ticket.id
      ? loadedInsightState.insight
      : null;
  const historicalContext = useMemo(
    () => deriveHistoricalContextFromInsight(loadedInsight),
    [loadedInsight],
  );

  useEffect(() => {
    if (!isModalOpen || !ticket.id) {
      cacheHydrationAbortRef.current?.abort();
      cacheHydrationAbortRef.current = null;
      cacheHydratedTicketIdRef.current = null;
      return;
    }

    if (cacheHydratedTicketIdRef.current === ticket.id) {
      return;
    }

    const ticketId = ticket.id;
    const controller = new AbortController();
    cacheHydrationAbortRef.current?.abort();
    cacheHydrationAbortRef.current = controller;

    void (async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        const cachedInsight = await ticketService.getCachedInsight(
          ticketId,
          token,
          controller.signal,
        );

        if (!controller.signal.aborted) {
          setLoadedInsightState((current) => {
            if (current?.ticketId === ticketId && current.insight) {
              return current;
            }
            return { ticketId, insight: cachedInsight };
          });
          cacheHydratedTicketIdRef.current = ticketId;
        }
      } catch (err) {
        if (!controller.signal.aborted) {
          console.warn("Cached Cortex Insight hydration failed", err);
          cacheHydratedTicketIdRef.current = ticketId;
        }
      } finally {
        if (cacheHydrationAbortRef.current === controller) {
          cacheHydrationAbortRef.current = null;
        }
      }
    })();

    return () => {
      controller.abort();
      if (cacheHydrationAbortRef.current === controller) {
        cacheHydrationAbortRef.current = null;
      }
    };
  }, [getAccessTokenSilently, isModalOpen, ticket.id]);

  const handleInsightReady = useCallback((insight: CortexInsight | null) => {
    setLoadedInsightState({
      ticketId: ticket.id ?? "",
      insight,
    });
  }, [ticket.id]);

  function switchTab(tab: CortexTab) {
    setActiveTab(tab);
    setVisited((prev) => (prev.has(tab) ? prev : new Set([...prev, tab])));
  }

  return (
    <section className="relative flex min-h-0 flex-col overflow-visible rounded-md border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900/50">
      <div className="shrink-0 border-b border-gray-200 px-4 pb-0 pt-3 dark:border-slate-800">
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Cortex
        </p>
        <p className="mb-3 text-[11px] leading-snug text-slate-500 dark:text-slate-400">
          Routing recommendation, SLA risk, advisory insight
        </p>
        <div className="-mb-px flex" role="tablist" aria-label="Cortex tabs">
          {ALL_TABS.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={activeTab === tab}
              onClick={() => switchTab(tab)}
              className={`mr-1 border-b-2 px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none ${
                activeTab === tab
                  ? "border-slate-900 text-slate-900 dark:border-slate-100 dark:text-slate-100"
                  : "border-transparent text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-slate-200"
              }`}
            >
              {TAB_LABELS[tab]}
            </button>
          ))}
        </div>
      </div>

      <div
        ref={scrollContainerRef}
        className="scroll-surface min-h-0 flex-1 overflow-y-auto pb-12"
      >
        <div className={activeTab === "review" ? undefined : "hidden"}>
          {reviewSlot ?? (
            <p className="p-4 text-sm text-slate-500 dark:text-slate-400">
              No review content available.
            </p>
          )}
        </div>

        {ticket.id && visited.has("decision") && (
          <div className={activeTab === "decision" ? undefined : "hidden"}>
            <TicketRoutingInsight
              ticket={ticket}
              isModalOpen={isModalOpen}
              ticketBoards={ticketBoards}
              livePreview={livePreview}
              riskLevel={riskLevel}
              historicalContext={historicalContext}
              onRecommendedOwnerClick={() => switchTab("risk")}
              onReassignmentApplied={onReassignmentApplied}
            />
          </div>
        )}

        {ticket.id && visited.has("risk") && (
          <div className={activeTab === "risk" ? undefined : "hidden"}>
            <CortexRiskPanel
              ticketId={ticket.id}
              isOpen={isModalOpen}
              insight={loadedInsight}
              onRiskReady={onRiskReady}
              onRecommendedActionClick={() => switchTab("decision")}
            />
          </div>
        )}

        {ticket.id && visited.has("insight") && (
          <div className={activeTab === "insight" ? undefined : "hidden"}>
            <CortexInsightPanel
              ticketId={ticket.id}
              isOpen={isModalOpen}
              onOpenSourceTicket={onOpenSourceTicket}
              onInsightReady={handleInsightReady}
            />
          </div>
        )}

        {!ticket.id && activeTab !== "review" && (
          <p className="p-4 text-sm text-slate-500 dark:text-slate-400">
            Save the ticket to unlock Cortex insights.
          </p>
        )}
      </div>

      <ScrollToBottomButton
        containerRef={scrollContainerRef}
        aria-label="Scroll Cortex panel content to bottom"
      />
    </section>
  );
}

export default CortexTabbedPanel;
