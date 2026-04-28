import { useEffect, useState, type ReactNode } from "react";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { RoutingLivePreviewInput } from "./TicketRoutingInsight";
import type { CortexSlaRisk } from "../types/cortexRisk";
import TicketRoutingInsight from "./TicketRoutingInsight";
import CortexRiskPanel from "./CortexRiskPanel";
import CortexInsightPanel from "./CortexInsightPanel";

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
  /** Risk level forwarded from onRiskReady for the Decision panel's context signal. */
  riskLevel?: "Low" | "Medium" | "High" | null;
  onRiskReady?: (risk: CortexSlaRisk | null) => void;
  onOpenSourceTicket?: (ticketId: string) => void | Promise<void>;
  onReassignmentApplied?: (updatedTicket: Ticket) => void;
  /** Content to render inside the Review tab (e.g. ApprovalTriageModalColumn). */
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
  const [activeTab, setActiveTab] = useState<CortexTab>("review");
  // Tracks which tabs have been visited at least once — panels only mount on first visit.
  const [visited, setVisited] = useState<ReadonlySet<CortexTab>>(
    new Set<CortexTab>(["review"]),
  );

  // Reset state when a different ticket is opened.
  useEffect(() => {
    setActiveTab("review");
    setVisited(new Set<CortexTab>(["review"]));
  }, [ticket.id]);

  function switchTab(tab: CortexTab) {
    setActiveTab(tab);
    setVisited((prev) => (prev.has(tab) ? prev : new Set([...prev, tab])));
  }

  return (
    <div className="overflow-hidden rounded-md border border-gray-200 bg-white dark:border-slate-800 dark:bg-slate-900/50">
      {/* Header + tab strip */}
      <div className="border-b border-gray-200 px-4 pb-0 pt-3 dark:border-slate-800">
        <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Cortex
        </p>
        <div className="-mb-px flex">
          {ALL_TABS.map((tab) => (
            <button
              key={tab}
              type="button"
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

      {/* Tab panels — each mounts on first visit, then stays mounted (hidden when inactive).
          This prevents eager API calls for unvisited tabs while avoiding remount on every switch. */}
      <div>
        {/* Review tab: always pre-mounted; renders reviewSlot content injected from parent. */}
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
            />
          </div>
        )}

        {!ticket.id && activeTab !== "review" && (
          <p className="p-4 text-sm text-slate-500 dark:text-slate-400">
            Save the ticket to unlock Cortex insights.
          </p>
        )}
      </div>
    </div>
  );
}
