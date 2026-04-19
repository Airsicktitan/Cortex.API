import { useEffect, useMemo, type Dispatch, type SetStateAction } from "react";
import TicketCard from "./TicketCard";
import { TicketGridSkeleton } from "./LoadingSkeletons";
import {
  type FilterOption,
  type MyTicketApprovalFilter,
  type PageSizeOption,
  type RequesterLifecycleSummary,
  type TicketListSortOption,
  isTicketListSortOption,
} from "../hooks/useTickets";
import type { SavedTicketFilter } from "../hooks/useSavedFilters";
import type { ThemeMode } from "../theme";
import type { Ticket } from "../types/ticket";
import type { TicketBoardDefinition } from "../types/ticketBoard";

const SLA_FILTER_OPTIONS = ["Breached", "At Risk", "Met"] as const;
const PAGE_SIZE_OPTIONS: ReadonlyArray<PageSizeOption> = [10, 25, 50, "all"];
const TICKET_AUTO_REFRESH_INTERVAL_MS = 15000;

export type TicketsContainerProps = {
  theme: ThemeMode;
  isAuthenticated: boolean;
  bootstrapComplete: boolean;
  needsConsent: boolean;
  canViewTicketSections: boolean;
  boardTabs: TicketBoardDefinition[];
  boardCountsById: Record<number, number>;
  loading: boolean;
  apiUnavailable: boolean;
  error: string | null;
  savedFilters: SavedTicketFilter[];
  selectedSavedFilterId: string;
  setSelectedSavedFilterId: Dispatch<SetStateAction<string>>;
  openSaveFilterModal: () => void;
  deleteSavedFilter: () => void;
  clearTicketFilters: () => void;
  applySavedFilter: (savedFilterId: string) => void;
  handleFilterChange: (value: string) => void;
  handleFilterValueChange: (value: string) => void;
  handleSearchChange: (value: string) => void;
  handlePageSizeChange: (value: string) => void;
  filter: FilterOption;
  filterValue: string;
  searchQuery: string;
  pageSize: PageSizeOption;
  selectedBoardId: number | "all";
  setSelectedBoardId: Dispatch<SetStateAction<number | "all">>;
  myTicketsOnly: boolean;
  setMyTicketsOnly: Dispatch<SetStateAction<boolean>>;
  myTicketApprovalFilter: MyTicketApprovalFilter;
  setMyTicketApprovalFilter: Dispatch<SetStateAction<MyTicketApprovalFilter>>;
  ticketListSort: TicketListSortOption;
  setTicketListSort: Dispatch<SetStateAction<TicketListSortOption>>;
  tickets: Ticket[];
  pagedTickets: Ticket[];
  totalTickets: number;
  totalPages: number;
  currentPage: number;
  setCurrentPage: Dispatch<SetStateAction<number>>;
  showingStart: number;
  showingEnd: number;
  requesterLifecycleSummary: RequesterLifecycleSummary | null;
  isModalOpen: boolean;
  syncTicketChangesSilently: (providedToken?: string) => Promise<void>;
  openTicket: (ticket: Ticket) => void;
};

export default function TicketsContainer({
  theme,
  isAuthenticated,
  bootstrapComplete,
  needsConsent,
  canViewTicketSections,
  boardTabs,
  boardCountsById,
  loading,
  apiUnavailable,
  error,
  savedFilters,
  selectedSavedFilterId,
  setSelectedSavedFilterId,
  openSaveFilterModal,
  deleteSavedFilter,
  clearTicketFilters,
  applySavedFilter,
  handleFilterChange,
  handleFilterValueChange,
  handleSearchChange,
  handlePageSizeChange,
  filter,
  filterValue,
  searchQuery,
  pageSize,
  selectedBoardId,
  setSelectedBoardId,
  myTicketsOnly,
  setMyTicketsOnly,
  myTicketApprovalFilter,
  setMyTicketApprovalFilter,
  ticketListSort,
  setTicketListSort,
  tickets,
  pagedTickets,
  totalTickets,
  totalPages,
  currentPage,
  setCurrentPage,
  showingStart,
  showingEnd,
  requesterLifecycleSummary,
  isModalOpen,
  syncTicketChangesSilently,
  openTicket,
}: TicketsContainerProps) {
  const allBoardsCount = useMemo(
    () => boardTabs.reduce((sum, board) => sum + (boardCountsById[board.id] ?? 0), 0),
    [boardCountsById, boardTabs],
  );
  const approvalFilterCounts = useMemo(
    () => ({
      all: requesterLifecycleSummary?.total ?? 0,
      NeedsMoreInfo: requesterLifecycleSummary?.needsAttention ?? 0,
      PendingApproval: requesterLifecycleSummary?.pendingApproval ?? 0,
      Approved: requesterLifecycleSummary?.activeWork ?? 0,
      Rejected: requesterLifecycleSummary?.rejected ?? 0,
    }),
    [requesterLifecycleSummary],
  );
  const searchPlaceholder = myTicketsOnly
    ? "Search my requests..."
    : "Search tickets...";

  useEffect(() => {
    if (
      !isAuthenticated ||
      !bootstrapComplete ||
      needsConsent ||
      !canViewTicketSections
    ) {
      return;
    }

    const isUserInteractingWithForm = () => {
      if (typeof document === "undefined") {
        return false;
      }

      const activeElement = document.activeElement as HTMLElement | null;
      if (!activeElement) {
        return false;
      }

      if (activeElement.isContentEditable) {
        return true;
      }

      return ["INPUT", "TEXTAREA", "SELECT"].includes(activeElement.tagName);
    };

    const intervalId = window.setInterval(() => {
      if (loading || isModalOpen || isUserInteractingWithForm()) {
        return;
      }

      void syncTicketChangesSilently();
    }, TICKET_AUTO_REFRESH_INTERVAL_MS);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [
    isAuthenticated,
    isModalOpen,
    loading,
    needsConsent,
    bootstrapComplete,
    canViewTicketSections,
    syncTicketChangesSilently,
  ]);

  return (
    <>
      <div className="mb-6 rounded-lg border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900">
        <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
          <div>
            <h3 className="text-sm font-semibold text-gray-900 dark:text-slate-100">
              {myTicketsOnly ? "My Tickets" : "Ticket Filters"}
            </h3>
            <p className="text-sm text-gray-500 dark:text-slate-400">
              {myTicketsOnly
                ? "Track what you submitted, what needs your attention, and what is already active work."
                : "Search, narrow, and save ticket views without crowding the header."}
            </p>
          </div>

          <div className="flex flex-wrap gap-2">
            <button
              onClick={openSaveFilterModal}
              className="inline-flex items-center rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Save Filter
            </button>

            {selectedSavedFilterId && (
              <button
                onClick={deleteSavedFilter}
                className="inline-flex items-center rounded-md border border-red-200 px-3 py-2 text-sm font-medium text-red-600 shadow-sm transition-colors hover:bg-red-50 dark:border-red-900/50 dark:text-red-300 dark:hover:bg-red-950/30"
              >
                Delete Saved
              </button>
            )}

            <button
              onClick={clearTicketFilters}
              className="inline-flex items-center rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 shadow-sm transition-colors hover:bg-gray-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Clear
            </button>
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button
            onClick={() => setSelectedBoardId("all")}
            className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
              selectedBoardId === "all"
                ? "bg-cortex-blue text-white"
                : "border border-gray-200 bg-gray-50 text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
            }`}
          >
            All Boards
            <span className="ml-2 text-xs opacity-80">{allBoardsCount}</span>
          </button>
          {boardTabs.map((board) => {
            const boardCount = boardCountsById[board.id] ?? 0;
            const isActive = selectedBoardId === board.id;

            return (
              <button
                key={board.id}
                onClick={() => setSelectedBoardId(board.id)}
                className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? "bg-cortex-blue text-white"
                    : "border border-gray-200 bg-gray-50 text-gray-700 hover:bg-gray-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                {board.name}
                <span className="ml-2 text-xs opacity-80">{boardCount}</span>
              </button>
            );
          })}
        </div>

        {myTicketsOnly ? (
          <p className="mt-3 text-xs text-gray-500 dark:text-slate-400">
            Boards matter most once a request becomes active work. Use board filters
            here when you want to narrow approved requests further.
          </p>
        ) : null}

        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <select
            value={filter}
            onChange={(event) => handleFilterChange(event.target.value)}
            className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="all">All Tickets</option>
            <option value="status">By Status</option>
            <option value="priority">By Priority</option>
            <option value="sla">By SLA</option>
          </select>

          <label className="flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-600 dark:border-slate-700 dark:text-slate-400">
            <span>Show</span>
            <select
              value={pageSize}
              onChange={(event) => handlePageSizeChange(event.target.value)}
              style={{
                colorScheme: theme === "dark" ? "dark" : "light",
              }}
              className="min-w-0 flex-1 rounded-md border border-gray-300 bg-white px-2 py-1 text-gray-900 shadow-none focus:border-cortex-blue focus:ring-0 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
            >
              {PAGE_SIZE_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option === "all" ? "All" : option}
                </option>
              ))}
            </select>
          </label>

          {filter === "sla" ? (
            <select
              value={filterValue}
              onChange={(event) =>
                handleFilterValueChange(event.target.value)
              }
              className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
            >
              <option value="">Select SLA state</option>
              {SLA_FILTER_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          ) : filter !== "all" ? (
            <input
              type="text"
              placeholder={`Enter ${filter}...`}
              value={filterValue}
              onChange={(event) =>
                handleFilterValueChange(event.target.value)
              }
              className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
            />
          ) : (
            <div className="hidden lg:block" />
          )}

          <select
            value={selectedSavedFilterId}
            onChange={(event) => applySavedFilter(event.target.value)}
            className="rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
          >
            <option value="">Saved Filters</option>
            {savedFilters.map((savedFilter) => (
              <option key={savedFilter.id} value={savedFilter.id}>
                {savedFilter.name}
              </option>
            ))}
          </select>
        </div>

        <div className="mt-3 flex flex-col gap-3">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div
              className="flex shrink-0 gap-1 rounded-full border border-gray-200 bg-gray-50 p-1 dark:border-slate-700 dark:bg-slate-800"
              role="group"
              aria-label="Ticket scope"
            >
              <button
                type="button"
                onClick={() => {
                  setSelectedSavedFilterId("");
                  setMyTicketsOnly(false);
                  setMyTicketApprovalFilter("all");
                }}
                className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                  !myTicketsOnly
                    ? "bg-cortex-blue text-white"
                    : "text-gray-700 hover:bg-gray-100 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                All Tickets
              </button>
              <button
                type="button"
                onClick={() => {
                  setSelectedSavedFilterId("");
                  setMyTicketsOnly(true);
                }}
                className={`rounded-full px-3 py-2 text-sm font-medium transition-colors ${
                  myTicketsOnly
                    ? "bg-cortex-blue text-white"
                    : "text-gray-700 hover:bg-gray-100 dark:text-slate-200 dark:hover:bg-slate-700"
                }`}
              >
                My Tickets
              </button>
            </div>
            <input
              type="text"
              placeholder={searchPlaceholder}
              value={searchQuery}
              onChange={(event) => handleSearchChange(event.target.value)}
              className="min-w-0 flex-1 rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:placeholder:text-slate-500"
            />
            <select
              aria-label="Sort tickets"
              value={ticketListSort}
              onChange={(event) => {
                const value = event.target.value;
                if (isTicketListSortOption(value)) {
                  setSelectedSavedFilterId("");
                  setTicketListSort(value);
                }
              }}
              className="w-full shrink-0 rounded-md border-gray-300 bg-white text-gray-900 shadow-sm sm:w-52 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
            >
              <option value="newest-first">
                {myTicketsOnly ? "Most relevant first" : "Newest first"}
              </option>
              <option value="oldest-first">Oldest first</option>
              <option value="priority-high-low">Priority (high → low)</option>
              <option value="priority-low-high">Priority (low → high)</option>
              <option value="due-soonest">Due soonest</option>
              <option value="most-overdue">Most overdue</option>
            </select>
          </div>
          {myTicketsOnly ? (
            <div>
              <p className="text-sm text-gray-500 dark:text-slate-400">
                Request lifecycle
              </p>
              <p className="mt-0.5 text-xs text-gray-400 dark:text-slate-500">
                Action-needed and waiting-review requests surface first in the
                default sort so you can see what matters fastest.
              </p>
              <div
                className="mt-2 flex flex-wrap gap-2"
                role="group"
                aria-label="Filter by approval status"
              >
                {(
                  [
                    { value: "all" as const, label: "All states" },
                    {
                      value: "NeedsMoreInfo" as const,
                      label: "Needs More Info",
                    },
                    {
                      value: "PendingApproval" as const,
                      label: "Pending Approval",
                    },
                    { value: "Approved" as const, label: "Approved" },
                    { value: "Rejected" as const, label: "Rejected" },
                  ] as const
                ).map((option) => {
                  const active = myTicketApprovalFilter === option.value;
                  const count = approvalFilterCounts[option.value];
                  return (
                    <button
                      key={option.value}
                      type="button"
                      onClick={() => setMyTicketApprovalFilter(option.value)}
                      className={`rounded-full px-3 py-1.5 text-xs font-medium transition-colors ${
                        active
                          ? "bg-cortex-blue text-white"
                          : "border border-gray-200 bg-white text-gray-700 hover:bg-gray-50 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                      }`}
                    >
                      {option.label}
                      <span className="ml-2 text-[11px] opacity-75">{count}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          ) : null}
        </div>
      </div>

      {(loading || apiUnavailable) && <TicketGridSkeleton />}

      {error && !apiUnavailable && (
        <div className="bg-red-50 dark:bg-red-950/40 border-l-4 border-red-500 p-4 rounded">
          <p className="text-red-700 dark:text-red-300">{error}</p>
        </div>
      )}

      {!loading && !apiUnavailable && !error && tickets.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-200 bg-gray-50/80 px-6 py-16 text-center dark:border-slate-800 dark:bg-slate-900/40">
          <p className="text-lg font-semibold text-gray-800 dark:text-slate-100">
            {myTicketsOnly ? "No requests match this view" : "No tickets found"}
          </p>
          <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
            {myTicketsOnly
              ? "Try another lifecycle filter, board, or search term."
              : "Adjust your filters or try another board."}
          </p>
        </div>
      )}

      {!loading && !apiUnavailable && !error && tickets.length > 0 && (
        <>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
            {pagedTickets.map((ticket) => (
              <TicketCard
                key={ticket.id}
                ticket={ticket}
                onClick={() => openTicket(ticket)}
                approvalDisplayContext={myTicketsOnly ? "requester" : "active"}
              />
            ))}
          </div>

          <div className="mt-6 flex flex-col gap-3 rounded-lg border border-gray-200 bg-white/80 px-4 py-3 shadow-sm dark:border-slate-800 dark:bg-slate-900/80 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-gray-600 dark:text-slate-400">
              Showing {showingStart}-{showingEnd} of {totalTickets} tickets
            </p>
            {pageSize !== "all" && totalPages > 1 && (
              <div className="flex items-center gap-3 sm:ml-auto">
                <p className="text-sm text-gray-600 dark:text-slate-400">
                  Page {currentPage} of {totalPages}
                </p>
                <button
                  onClick={() =>
                    setCurrentPage((page) => Math.max(1, page - 1))
                  }
                  disabled={currentPage === 1}
                  className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  Previous
                </button>
                <button
                  onClick={() =>
                    setCurrentPage((page) => Math.min(totalPages, page + 1))
                  }
                  disabled={currentPage === totalPages}
                  className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                >
                  Next
                </button>
              </div>
            )}
          </div>
        </>
      )}
    </>
  );
}
