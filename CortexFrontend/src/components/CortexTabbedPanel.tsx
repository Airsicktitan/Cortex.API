import { useEffect, useState } from "react";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { RoutingLivePreviewInput } from "./TicketRoutingInsight";
import type { CortexSlaRisk } from "../types/cortexRisk";
import TicketRoutingInsight from "./TicketRoutingInsight";
import CortexRiskPanel from "./CortexRiskPanel";
import CortexInsightPanel from "./CortexInsightPanel";
import CortexAutonomyPanel from "./CortexAutonomyPanel";

type CortexTab = "decision" | "risk" | "insight" | "autonomy";

const TAB_LABELS: Record<CortexTab, string> = {
  decision: "Decision",
  risk: "Risk",
  insight: "Insight",
  autonomy: "Autonomy",
};

const ALL_TABS: CortexTab[] = ["decision", "risk", "insight", "autonomy"];
const TABS_NO_AUTONOMY: CortexTab[] = ["decision", "risk", "insight"];

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
  /** Hide Autonomy tab (e.g. in requester context). */
  showAutonomy?: boolean;
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
  showAutonomy = true,
}: CortexTabbedPanelProps) {
  const [activeTab, setActiveTab] = useState<CortexTab>("decision");
  // Tracks which tabs have been visited at least once — panels only mount on first visit.
  const [visited, setVisited] = useState<ReadonlySet<CortexTab>>(
    new Set(["decision"]),
  );

  // Reset state when a different ticket is opened.
  useEffect(() => {
    setActiveTab("decision");
    setVisited(new Set(["decision"]));
  }, [ticket.id]);

  const tabs = showAutonomy ? ALL_TABS : TABS_NO_AUTONOMY;

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
        <div className="flex -mb-px">
          {tabs.map((tab) => (
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
        {visited.has("decision") && (
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

        {ticket.id && showAutonomy && visited.has("autonomy") && (
          <div className={activeTab === "autonomy" ? undefined : "hidden"}>
            <CortexAutonomyPanel ticketId={ticket.id} isOpen={isModalOpen} />
          </div>
        )}

        {!ticket.id && activeTab !== "decision" && (
          <p className="p-4 text-sm text-slate-500 dark:text-slate-400">
            Save the ticket to unlock Cortex insights.
          </p>
        )}
      </div>
    </div>
  );
}
