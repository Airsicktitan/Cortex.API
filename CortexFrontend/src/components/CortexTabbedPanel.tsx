import { useAuth0 } from "@auth0/auth0-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { RoutingLivePreviewInput } from "./TicketRoutingInsight";
import type { CortexInsight } from "../types/cortexInsight";
import type { CortexSlaRisk } from "../types/cortexRisk";
import type { SapTicketReferenceMatch } from "../types/sapTicketReference";
import { ticketService } from "../services/api";
import { deriveHistoricalContextFromInsight } from "../utils/cortexHistoricalContext";
import { buildSapDecisionAssist, buildSapIntentOnlyDecisionAssist } from "../utils/sapDecisionAssist";
import TicketRoutingInsight from "./TicketRoutingInsight";
import CortexRiskPanel from "./CortexRiskPanel";
import CortexInsightPanel from "./CortexInsightPanel";
import { SapDecisionAssistCard } from "./SapDecisionAssistCard";
import { ScrollToBottomButton } from "./ui/ScrollToBottomButton";

const API_AUDIENCE = "https://cortex-api";

type CortexTab = "decision" | "source" | "sap" | "intake" | "evidence" | "history";

const TAB_LABELS: Record<CortexTab, string> = {
  decision: "Decision",
  source: "Source",
  sap: "SAP",
  intake: "Intake",
  evidence: "Evidence",
  history: "History",
};

const TAIL_TABS: CortexTab[] = ["intake", "evidence", "history"];

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
  /**
   * External source / provenance (reviewer rail). When set, adds Source tab after Decision.
   */
  sourceContextSlot?: ReactNode;
  /**
   * SAP reference context (reviewer rail). When set, adds SAP tab after Source (or after Decision if no Source).
   */
  sapContextSlot?: ReactNode;
  /**
   * SAP reference matches for advisory Decision assist (successful load only; parent omits while loading/error).
   */
  sapDecisionAssistMatches?: SapTicketReferenceMatch[];
  /** True when API returned SAP intent without catalog matches — intake-only Decision assist. */
  sapIntentOnly?: boolean;
  /** Title + description for ticket-body key/required hints in Decision assist. */
  sapDecisionAssistTicketText?: string | null;
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
  sapContextSlot,
  sapDecisionAssistMatches,
  sapIntentOnly = false,
  sapDecisionAssistTicketText,
}: CortexTabbedPanelProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [activeTab, setActiveTab] = useState<CortexTab>("decision");
  const riskPanelAnchorRef = useRef<HTMLDivElement | null>(null);

  const tabList = useMemo((): CortexTab[] => {
    const tabs: CortexTab[] = ["decision"];
    if (sourceContextSlot) {
      tabs.push("source");
    }
    if (sapContextSlot) {
      tabs.push("sap");
    }
    tabs.push(...TAIL_TABS);
    return tabs;
  }, [sourceContextSlot, sapContextSlot]);

  useEffect(() => {
    if (activeTab === "source" && !sourceContextSlot) {
      setActiveTab("decision");
      return;
    }
    if (activeTab === "sap" && !sapContextSlot) {
      setActiveTab("decision");
    }
  }, [activeTab, sourceContextSlot, sapContextSlot]);

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

  const sapDecisionAssist = useMemo(() => {
    if (sapIntentOnly) {
      return buildSapIntentOnlyDecisionAssist(sapDecisionAssistTicketText);
    }
    if (sapDecisionAssistMatches?.length) {
      return buildSapDecisionAssist(
        sapDecisionAssistMatches,
        sapDecisionAssistTicketText,
      );
    }
    return null;
  }, [
    sapIntentOnly,
    sapDecisionAssistMatches,
    sapDecisionAssistTicketText,
  ]);

  const scrollRiskIntoView = useCallback(() => {
    riskPanelAnchorRef.current?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }, []);

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
    <section className="relative flex h-full min-h-0 min-w-0 flex-col overflow-hidden rounded-md border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900/50">
      <div className="shrink-0 border-b border-gray-200 px-3 pb-0 pt-2 dark:border-slate-800 sm:px-4 sm:pt-3">
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Cortex
        </p>
        <p className="mb-2 text-[11px] leading-snug text-slate-500 dark:text-slate-400 lg:mb-3">
          Decision controls routing and reviewer actions.
          {sourceContextSlot || sapContextSlot ? (
            <span className="hidden lg:inline">
              {sourceContextSlot && sapContextSlot ? (
                <>
                  {" "}
                  <span className="font-medium text-slate-600 dark:text-slate-300">
                    Source
                  </span>{" "}
                  is provenance;{" "}
                  <span className="font-medium text-slate-600 dark:text-slate-300">SAP</span> is
                  data reference context.
                </>
              ) : sourceContextSlot ? (
                <>
                  {" "}
                  <span className="font-medium text-slate-600 dark:text-slate-300">Source</span>{" "}
                  shows external provenance.
                </>
              ) : (
                <>
                  {" "}
                  <span className="font-medium text-slate-600 dark:text-slate-300">SAP</span> shows
                  data reference context.
                </>
              )}
            </span>
          ) : null}{" "}
          <span className="hidden lg:inline">
            Intake, Evidence, and History provide advisory context; workload and SLAs live under
            Decision.
          </span>
          <span className="lg:hidden">Use the tabs below for details.</span>
        </p>
        <div
          className="-mb-px flex flex-nowrap items-end justify-start gap-x-0.5 pb-px"
          role="tablist"
          aria-label="Cortex tabs"
        >
          {tabList.map((tab) => (
            <button
              key={tab}
              type="button"
              role="tab"
              aria-selected={activeTab === tab}
              onClick={() => switchTab(tab)}
              className={`mb-1 shrink-0 whitespace-nowrap border-b-2 px-1 py-1 text-xs font-medium leading-none transition-colors focus-visible:outline-none sm:px-1.5 sm:py-1.5 xl:px-2 xl:text-[13px] ${
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
        className="scroll-surface min-h-0 flex-1 overflow-y-auto overscroll-y-contain pb-12 touch-pan-y"
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
          {ticket.id &&
          visited.has("decision") &&
          sapDecisionAssist ? (
            <div className="mt-5 border-b border-slate-100 px-4 pb-4 pt-1 dark:border-slate-800/80">
              <SapDecisionAssistCard assist={sapDecisionAssist} />
            </div>
          ) : null}
          {ticket.id && visited.has("decision") && (
            <div>
              <TicketRoutingInsight
                ticket={ticket}
                isModalOpen={isModalOpen}
                ticketBoards={ticketBoards}
                livePreview={livePreview}
                riskLevel={riskLevel}
                historicalContext={historicalContext}
                onRecommendedOwnerClick={scrollRiskIntoView}
                onReassignmentApplied={onReassignmentApplied}
              />
            </div>
          )}
          {ticket.id && visited.has("decision") && (
            <div
              ref={riskPanelAnchorRef}
              className="scroll-mt-3 border-t border-slate-100 dark:border-slate-800/80"
            >
              <CortexRiskPanel
                ticketId={ticket.id}
                isOpen={isModalOpen}
                insight={loadedInsight}
                onRiskReady={onRiskReady}
                onRecommendedActionClick={() => switchTab("decision")}
              />
            </div>
          )}
        </div>

        {sourceContextSlot && visited.has("source") && (
          <div className={activeTab === "source" ? undefined : "hidden"}>
            <div className="px-4 py-3">{sourceContextSlot}</div>
          </div>
        )}

        {sapContextSlot && visited.has("sap") && (
          <div className={activeTab === "sap" ? undefined : "hidden"}>
            <div className="px-4 py-3">{sapContextSlot}</div>
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
                    Run reviewer analysis from Decision to evaluate completeness and missing
                    details.
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
