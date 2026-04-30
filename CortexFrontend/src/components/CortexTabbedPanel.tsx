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

type CortexTab =
  | "decision"
  | "risk"
  | "intake"
  | "evidence"
  | "history";

const TAB_LABELS: Record<CortexTab, string> = {
  decision: "Decision",
  risk: "Risk",
  intake: "Intake",
  evidence: "Evidence",
  history: "History",
};

const ALL_TABS: CortexTab[] = [
  "decision",
  "risk",
  "intake",
  "evidence",
  "history",
];

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
  intakeSlot?: ReactNode;
  evidenceSlot?: ReactNode;
  /** External work-item provenance (read-only); shown under Cortex intro, above tabs. */
  sourceContextSlot?: ReactNode;
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
  intakeSlot,
  evidenceSlot,
  sourceContextSlot,
}: CortexTabbedPanelProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [activeTab, setActiveTab] = useState<CortexTab>("decision");
  const [visited, setVisited] = useState<ReadonlySet<CortexTab>>(
    new Set<CortexTab>(["decision"]),
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
          Decision controls routing and reviewer actions. Risk, Intake, Evidence, and History
          provide advisory context.
        </p>
        {sourceContextSlot ? (
          <div className="mb-3">{sourceContextSlot}</div>
        ) : null}
        <div
          className="-mb-px flex flex-wrap gap-y-1"
          role="tablist"
          aria-label="Cortex tabs"
        >
          {ALL_TABS.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={activeTab === tab}
              onClick={() => switchTab(tab)}
              className={`mr-1 mb-1 border-b-2 px-2.5 py-2 text-sm font-medium transition-colors last:mr-0 focus-visible:outline-none sm:px-3 ${
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
        <div className={activeTab === "decision" ? undefined : "hidden"}>
          <div className="border-b border-slate-100 px-4 pb-4 pt-2 dark:border-slate-800/80">
            <p className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
              Reviewer apply
            </p>
            <div className="mt-3">
              {reviewSlot ?? (
                <div className="rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-slate-700 dark:bg-slate-900/50">
                  <p className="text-xs font-semibold text-gray-800 dark:text-slate-200">
                    No triage apply actions yet
                  </p>
                  <p className="mt-1.5 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
                    For example when analysis is still loading or unavailable.
                  </p>
                </div>
              )}
            </div>
          </div>
          {ticket.id && visited.has("decision") && (
            <div>
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
        </div>

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

        {visited.has("intake") && (
          <div className={activeTab === "intake" ? undefined : "hidden"}>
            <div className="px-4 py-3">
              {intakeSlot ?? (
                <div className="rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-slate-700 dark:bg-slate-900/50">
                  <p className="text-xs font-semibold text-gray-800 dark:text-slate-200">
                    No intake analysis available
                  </p>
                  <p className="mt-1.5 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
                    Run reviewer analysis from Decision to evaluate completeness and
                    missing details.
                  </p>
                </div>
              )}
            </div>
          </div>
        )}

        {visited.has("evidence") && (
          <div className={activeTab === "evidence" ? undefined : "hidden"}>
            <div className="px-4 py-3">
              {evidenceSlot ?? (
                <div className="rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-slate-700 dark:bg-slate-900/50">
                  <p className="text-xs font-semibold text-gray-800 dark:text-slate-200">
                    No screenshot evidence analyzed yet
                  </p>
                  <p className="mt-1.5 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
                    Use Analyze screenshots in Attachments to add visual evidence here.
                  </p>
                </div>
              )}
            </div>
          </div>
        )}

        {ticket.id && visited.has("history") && (
          <div className={activeTab === "history" ? undefined : "hidden"}>
            <CortexInsightPanel
              ticketId={ticket.id}
              isOpen={isModalOpen}
              onOpenSourceTicket={onOpenSourceTicket}
              onInsightReady={handleInsightReady}
            />
          </div>
        )}

        {!ticket.id && activeTab !== "decision" && (
          <p className="p-4 text-sm text-slate-500 dark:text-slate-400">
            Save the ticket to use the Cortex tabs.
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
