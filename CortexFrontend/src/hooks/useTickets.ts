import {
  createElement,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { Dispatch, SetStateAction } from "react";
import type { ArchivedTicket } from "../types/archivedTicket";
import type {
  ApprovalStatus,
  CreateTicketInput,
  Ticket,
  TicketMutationInput,
  TicketSaveResult,
} from "../types/ticket";
import { isTicketApproved } from "../types/ticket";
import type { UserProfile } from "../types/user";
import type { TicketListQueryOptions } from "../services/api";
import {
  API_USER_MESSAGES,
  ApiError,
  attachmentService,
  getUserFacingErrorMessage,
  ticketService,
} from "../services/api";
import type { PagedTicketList } from "../types/pagedList";
import {
  isAttentionFilterValue,
  ticketMatchesAttentionFilter,
} from "../utils/ticketAttention";
import toast from "react-hot-toast";

/**
 * Submission confirmation for intake (PendingApproval). Future phases may add
 * similar toasts or notifications for Approved / Returned for Detail / Rejected.
 */
function notifyTicketSaveSuccess(
  isCreate: boolean,
  savedTicket: Ticket,
  message: string,
  attachmentCount = 0,
  options?: { resubmittedForReview?: boolean },
) {
  if (options?.resubmittedForReview) {
    toast.success(
      () =>
        createElement(
          "div",
          { className: "text-left max-w-sm" },
          createElement(
            "p",
            { className: "font-semibold" },
            "Ticket resubmitted for review.",
          ),
          createElement(
            "p",
            { className: "mt-1 text-sm opacity-90 leading-snug" },
            "Your updated request has been sent back to the Approval Queue.",
          ),
          attachmentCount > 0
            ? createElement(
                "p",
                { className: "mt-2 text-xs opacity-80" },
                attachmentCount === 1
                  ? "One file was attached."
                  : `${attachmentCount} files were attached.`,
              )
            : null,
        ),
      { id: "ticket-save-success", duration: 8000 },
    );
    return;
  }

  const isAwaitingReview =
    isCreate &&
    (savedTicket.approvalStatus === "PendingApproval" ||
      savedTicket.approvalStatus === undefined);

  if (isAwaitingReview) {
    toast.success(
      () =>
        createElement(
          "div",
          { className: "text-left max-w-sm" },
          createElement(
            "p",
            { className: "font-semibold" },
            "Ticket submitted for review.",
          ),
          createElement(
            "p",
            { className: "mt-1 text-sm opacity-90 leading-snug" },
            "Your request is awaiting approval before it enters active work. It stays in the Approval Queue until a reviewer responds.",
          ),
          attachmentCount > 0
            ? createElement(
                "p",
                { className: "mt-2 text-xs opacity-80" },
                attachmentCount === 1
                  ? "One file was attached."
                  : `${attachmentCount} files were attached.`,
              )
            : null,
        ),
      { id: "ticket-save-success", duration: 8000 },
    );
    return;
  }

  toast.success(message, { id: "ticket-save-success" });
}

export type MyTicketApprovalFilter = "all" | ApprovalStatus;
export type RequesterLifecycleSummary = {
  total: number;
  needsAttention: number;
  pendingApproval: number;
  activeWork: number;
  rejected: number;
};

export type FilterOption = "all" | "status" | "priority" | "sla" | "attention";
export type PageSizeOption = 10 | 25 | 50 | "all";
export type TicketListSortOption =
  | "newest-first"
  | "oldest-first"
  | "priority-high-low"
  | "priority-low-high"
  | "due-soonest"
  | "most-overdue";

function useDebouncedValue<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}

export function normalize(value: string) {
  return value.trim().toLowerCase();
}

export function isTicketListSortOption(value: string): value is TicketListSortOption {
  return (
    value === "newest-first" ||
    value === "oldest-first" ||
    value === "priority-high-low" ||
    value === "priority-low-high" ||
    value === "due-soonest" ||
    value === "most-overdue"
  );
}

function ticketMatchesSearch(ticket: Ticket, searchValue: string) {
  const searchableValues = [
    ticket.id,
    ticket.title,
    ticket.description,
    ticket.boardName,
    ticket.storyPoints,
    ticket.status,
    ticket.priority,
    ticket.synitiOwner,
    ticket.businessOwner,
    ticket.synitiOwnerDisplayName,
    ticket.businessOwnerDisplayName,
    ticket.createdByDisplayName,
  ];

  return searchableValues.some((value) =>
    normalize(String(value ?? "")).includes(searchValue),
  );
}

function getOwnerMatchCandidates(
  profile: UserProfile | null,
  auth0Name: string | undefined,
  auth0Email: string | undefined,
): Set<string> {
  const candidates = new Set<string>();
  for (const value of [
    profile?.displayName,
    profile?.nickName,
    profile?.email,
    auth0Name,
    auth0Email,
    profile?.id != null ? `user:${profile.id}` : undefined,
  ]) {
    const normalized = normalize(String(value ?? ""));
    if (normalized) {
      candidates.add(normalized);
    }
  }
  return candidates;
}

function getCreatorMatchCandidates(
  profile: UserProfile | null,
  auth0Sub: string | undefined,
  auth0Email: string | undefined,
): Set<string> {
  const candidates = new Set<string>();
  for (const value of [profile?.email, auth0Email, auth0Sub]) {
    const normalized = normalize(String(value ?? ""));
    if (normalized) {
      candidates.add(normalized);
    }
  }
  return candidates;
}

function ticketIsCreatedByCurrentUser(
  ticket: Ticket,
  currentUser: UserProfile | null,
  creatorCandidates: Set<string>,
): boolean {
  if (
    currentUser?.id != null &&
    String(ticket.createdBy).trim() === String(currentUser.id)
  ) {
    return true;
  }

  const createdByEmail = normalize(String(ticket.createdByEmail ?? ""));
  return createdByEmail !== "" && creatorCandidates.has(createdByEmail);
}

function ticketIsOwnedByCurrentUser(ticket: Ticket, candidates: Set<string>): boolean {
  const syn = normalize(String(ticket.synitiOwner ?? ""));
  const bus = normalize(String(ticket.businessOwner ?? ""));
  if (!syn && !bus) {
    return false;
  }
  return (syn !== "" && candidates.has(syn)) || (bus !== "" && candidates.has(bus));
}

/** "My Tickets" includes items you created (e.g. intake) and items where you are an owner. */
function ticketMatchesMyTicketsScope(
  ticket: Ticket,
  currentUser: UserProfile | null,
  creatorCandidates: Set<string>,
  ownerCandidates: Set<string>,
): boolean {
  if (ticketIsCreatedByCurrentUser(ticket, currentUser, creatorCandidates)) {
    return true;
  }
  return ticketIsOwnedByCurrentUser(ticket, ownerCandidates);
}

const PRIORITY_RANK: Record<string, number> = {
  Critical: 4,
  High: 3,
  Medium: 2,
  Low: 1,
};

function getPriorityRank(priority: string): number {
  return PRIORITY_RANK[priority] ?? 0;
}

function parseTicketTime(value: string | undefined): number {
  if (!value) {
    return 0;
  }

  const parsed = new Date(value).getTime();
  return Number.isNaN(parsed) ? 0 : parsed;
}

function getTicketApprovalStatus(ticket: Ticket): ApprovalStatus {
  return ticket.approvalStatus ?? "Approved";
}

function getRequesterLifecycleRank(ticket: Ticket): number {
  switch (getTicketApprovalStatus(ticket)) {
    case "NeedsMoreInfo":
      return 0;
    case "PendingApproval":
      return 1;
    case "Approved":
      return 2;
    case "Rejected":
      return 3;
    default:
      return 4;
  }
}

function compareRequesterRelevance(a: Ticket, b: Ticket): number {
  const lifecycleDiff = getRequesterLifecycleRank(a) - getRequesterLifecycleRank(b);
  if (lifecycleDiff !== 0) {
    return lifecycleDiff;
  }

  return parseTicketTime(b.createdDate) - parseTicketTime(a.createdDate);
}

function sortTicketsForList(
  tickets: Ticket[],
  sort: TicketListSortOption,
  options?: { requesterContext?: boolean },
): Ticket[] {
  const copy = [...tickets];
  const requesterContext = options?.requesterContext === true;

  switch (sort) {
    case "newest-first":
      if (requesterContext) {
        return copy.sort(compareRequesterRelevance);
      }
      return copy.sort(
        (a, b) => parseTicketTime(b.createdDate) - parseTicketTime(a.createdDate),
      );
    case "oldest-first":
      return copy.sort(
        (a, b) => parseTicketTime(a.createdDate) - parseTicketTime(b.createdDate),
      );
    case "priority-high-low":
      return copy.sort(
        (a, b) => getPriorityRank(b.priority) - getPriorityRank(a.priority),
      );
    case "priority-low-high":
      return copy.sort(
        (a, b) => getPriorityRank(a.priority) - getPriorityRank(b.priority),
      );
    case "due-soonest":
      return copy.sort(
        (a, b) => parseTicketTime(a.slaTargetDate) - parseTicketTime(b.slaTargetDate),
      );
    case "most-overdue":
      return copy.sort((a, b) => {
        const byRemaining = a.slaRemainingMinutes - b.slaRemainingMinutes;
        if (byRemaining !== 0) {
          return byRemaining;
        }

        return parseTicketTime(a.slaTargetDate) - parseTicketTime(b.slaTargetDate);
      });
    default:
      return copy;
  }
}

function upsertById<T extends { id: string }>(current: T[], incoming: T): T[] {
  const existingIndex = current.findIndex((item) => item.id === incoming.id);
  if (existingIndex < 0) {
    return [incoming, ...current];
  }

  const nextItems = [...current];
  nextItems[existingIndex] = incoming;
  return nextItems;
}

function removeById<T extends { id: string }>(current: T[], id: string): T[] {
  const nextItems = current.filter((item) => item.id !== id);
  return nextItems.length === current.length ? current : nextItems;
}

interface UseTicketsParams {
  getApiToken: (providedToken?: string) => Promise<string>;
  setApiUnavailable: Dispatch<SetStateAction<boolean>>;
  setLoading: Dispatch<SetStateAction<boolean>>;
  setError: Dispatch<SetStateAction<string | null>>;
  setNeedsConsent: Dispatch<SetStateAction<boolean>>;
  currentUser: UserProfile | null;
  auth0Sub?: string;
  auth0Name?: string;
  auth0Email?: string;
  isConsentRequiredError: (error: unknown) => boolean;
  isForbiddenError: (error: unknown) => boolean;
  isLikelyNetworkError: (error: unknown) => boolean;
}

export function useTickets({
  getApiToken,
  setApiUnavailable,
  setLoading,
  setError,
  setNeedsConsent,
  currentUser,
  auth0Sub,
  auth0Name,
  auth0Email,
  isConsentRequiredError,
  isForbiddenError,
  isLikelyNetworkError,
}: UseTicketsParams) {
  const ARCHIVED_PAGE_SIZE = 50;
  const ARCHIVED_CACHE_TTL_MS = 5 * 60 * 1000;
  /** Approved operational work from `getAll` (approved-only server filter). */
  const [operationalTickets, setOperationalTickets] = useState<Ticket[]>([]);
  /** Requester/owner scope from `getMySubmissions` (includes intake / non-approved). */
  const [mySubmissionsTickets, setMySubmissionsTickets] = useState<Ticket[]>([]);
  const [boardCountsById, setBoardCountsById] = useState<Record<number, number>>(
    {},
  );
  const [serverTicketListMeta, setServerTicketListMeta] = useState<{
    totalCount: number;
    totalPages: number;
  } | null>(null);
  const [filter, setFilter] = useState<FilterOption>("all");
  const [filterValue, setFilterValue] = useState("");
  const debouncedFilterValue = useDebouncedValue(filterValue, 300);
  const [selectedBoardId, setSelectedBoardId] = useState<number | "all">("all");
  const [searchQuery, setSearchQuery] = useState("");
  const debouncedSearchQuery = useDebouncedValue(searchQuery, 300);
  const [pageSize, setPageSize] = useState<PageSizeOption>(10);
  const [ticketListSort, setTicketListSort] =
    useState<TicketListSortOption>("newest-first");
  const [myTicketsOnly, setMyTicketsOnly] = useState(false);
  const [myTicketApprovalFilter, setMyTicketApprovalFilter] =
    useState<MyTicketApprovalFilter>("all");
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [archivedTickets, setArchivedTickets] = useState<ArchivedTicket[]>([]);
  const [archivedSearchQuery, setArchivedSearchQuery] = useState("");
  const debouncedArchivedSearchQuery = useDebouncedValue(archivedSearchQuery, 250);
  const [archivedLoading, setArchivedLoading] = useState(false);
  const [archivedLoadingMore, setArchivedLoadingMore] = useState(false);
  const [archivedHasMore, setArchivedHasMore] = useState(false);
  const [archivedError, setArchivedError] = useState<string | null>(null);
  const [highlightedArchivedTicketId, setHighlightedArchivedTicketId] =
    useState<string | null>(null);
  const [reactivatingArchivedTicketId, setReactivatingArchivedTicketId] =
    useState<string | null>(null);
  const [ticketToDelete, setTicketToDelete] = useState<Ticket | null>(null);
  const [deleting, setDeleting] = useState(false);
  const ticketSilentRefreshInFlightRef = useRef(false);
  const ticketSilentRefreshRequestIdRef = useRef(0);
  const ticketReconcileInFlightRef = useRef<Set<string>>(new Set());
  const ticketChangeSyncInFlightRef = useRef(false);
  const lastTicketChangeSyncUtcRef = useRef<string | null>(null);
  const lastTicketFullRefreshAtRef = useRef(0);
  const previousSelectedBoardIdRef = useRef<number | "all">(selectedBoardId);
  const lastBoardForCacheRestoreRef = useRef<number | "all" | null>(null);
  const activeBoardPageCacheRef = useRef<
    Map<string, { items: Ticket[]; meta: { totalCount: number; totalPages: number } }>
  >(new Map());
  const boardSwitchFallbackTicketsRef = useRef<Ticket[]>([]);
  const operationalTicketsRef = useRef<Ticket[]>([]);
  const mySubmissionsTicketsRef = useRef<Ticket[]>([]);
  const archivedCacheRef = useRef<
    Map<
      string,
      {
        items: ArchivedTicket[];
        page: number;
        totalPages: number;
        fetchedAt: number;
      }
    >
  >(new Map());
  const ticketsLoadedRef = useRef(false);
  const skipNextServerPagingEffectRef = useRef(false);

  useEffect(() => {
    operationalTicketsRef.current = operationalTickets;
  }, [operationalTickets]);

  useEffect(() => {
    mySubmissionsTicketsRef.current = mySubmissionsTickets;
  }, [mySubmissionsTickets]);

  const listForTicketViews = useMemo(
    () => (myTicketsOnly ? mySubmissionsTickets : operationalTickets),
    [mySubmissionsTickets, myTicketsOnly, operationalTickets],
  );

  /** Board counts, dashboard, and reports use operational (approved) data only. */
  const allTickets = operationalTickets;

  const setAllTickets = useCallback((update: SetStateAction<Ticket[]>) => {
    setOperationalTickets(update);
    setMySubmissionsTickets(update);
  }, []);

  const useClientFilterMode = useMemo(
    () =>
      debouncedSearchQuery.trim() !== "" ||
      filter !== "all" ||
      myTicketsOnly ||
      ticketListSort === "due-soonest" ||
      ticketListSort === "most-overdue" ||
      pageSize === "all",
    [
      debouncedSearchQuery,
      filter,
      myTicketsOnly,
      ticketListSort,
      pageSize,
    ],
  );

  const useServerDrivenPaging = useMemo(
    () => !useClientFilterMode,
    [useClientFilterMode],
  );

  const buildActiveTicketQueryOptions = useCallback((): TicketListQueryOptions => {
    const boardScope =
      selectedBoardId === "all" ? undefined : selectedBoardId;

    if (useClientFilterMode) {
      return {
        unpaged: true,
        sort: ticketListSort,
        ...(boardScope !== undefined ? { boardId: boardScope } : {}),
      };
    }

    const numericPageSize = pageSize === "all" ? 100 : pageSize;
    return {
      page: currentPage,
      pageSize: numericPageSize,
      sort: ticketListSort,
      ...(boardScope !== undefined ? { boardId: boardScope } : {}),
    };
  }, [
    currentPage,
    pageSize,
    selectedBoardId,
    ticketListSort,
    useClientFilterMode,
  ]);

  const applyTicketPageResponse = useCallback(
    (data: PagedTicketList, scope: "operational" | "mySubmissions") => {
      if (scope === "mySubmissions") {
        setMySubmissionsTickets(data.items);
        setServerTicketListMeta(null);
        return;
      }

      const boardScope =
        selectedBoardId === "all" ? undefined : selectedBoardId;

      if (useClientFilterMode && boardScope !== undefined) {
        setOperationalTickets((prev) => {
          const others = prev.filter((ticket) => ticket.boardId !== boardScope);
          return [...others, ...data.items];
        });
      } else {
        setOperationalTickets(data.items);
      }

      if (useServerDrivenPaging) {
        const meta = {
          totalCount: data.totalCount,
          totalPages: data.totalPages,
        };
        setServerTicketListMeta(meta);
        const cacheKey = selectedBoardId === "all" ? "all" : `board:${selectedBoardId}`;
        activeBoardPageCacheRef.current.set(cacheKey, {
          items: data.items,
          meta,
        });
      } else {
        setServerTicketListMeta(null);
      }
    },
    [selectedBoardId, useClientFilterMode, useServerDrivenPaging],
  );

  const loadBoardCountsSilently = useCallback(
    async (providedToken?: string) => {
      try {
        const token = providedToken ?? (await getApiToken());
        const counts = await ticketService.getBoardCounts(token);
        setBoardCountsById(counts);
      } catch (error) {
        console.error("Failed to load board counts", error);
      }
    },
    [getApiToken],
  );

  const upsertActiveTicketLocally = useCallback(
    (incomingTicket: Ticket, options?: { syncSelectedTicket?: boolean }) => {
      const previousTicket =
        operationalTicketsRef.current.find((ticket) => ticket.id === incomingTicket.id) ??
        mySubmissionsTicketsRef.current.find((ticket) => ticket.id === incomingTicket.id) ??
        (selectedTicket?.id === incomingTicket.id ? selectedTicket : null);

      const wasApproved = previousTicket ? isTicketApproved(previousTicket) : false;
      const nowApproved = isTicketApproved(incomingTicket);

      const creatorCandidates = getCreatorMatchCandidates(
        currentUser,
        auth0Sub,
        auth0Email,
      );
      const ownerCandidates = getOwnerMatchCandidates(
        currentUser,
        auth0Name,
        auth0Email,
      );

      setArchivedTickets((currentTickets) => removeById(currentTickets, incomingTicket.id));

      setOperationalTickets((currentTickets) => {
        if (!nowApproved) {
          return removeById(currentTickets, incomingTicket.id);
        }
        return upsertById(currentTickets, incomingTicket);
      });

      setMySubmissionsTickets((currentTickets) => {
        const mine = ticketMatchesMyTicketsScope(
          incomingTicket,
          currentUser,
          creatorCandidates,
          ownerCandidates,
        );
        const had = currentTickets.some((t) => t.id === incomingTicket.id);
        if (!mine && !had) {
          return currentTickets;
        }
        if (!mine && had) {
          return removeById(currentTickets, incomingTicket.id);
        }
        return upsertById(currentTickets, incomingTicket);
      });
      setBoardCountsById((currentCounts) => {
        if (!wasApproved && !nowApproved) {
          return currentCounts;
        }

        if (!previousTicket && nowApproved) {
          return {
            ...currentCounts,
            [incomingTicket.boardId]:
              (currentCounts[incomingTicket.boardId] ?? 0) + 1,
          };
        }

        if (previousTicket && wasApproved && !nowApproved) {
          return {
            ...currentCounts,
            [previousTicket.boardId]: Math.max(
              0,
              (currentCounts[previousTicket.boardId] ?? 0) - 1,
            ),
          };
        }

        if (!previousTicket) {
          return currentCounts;
        }

        if (!wasApproved && nowApproved) {
          return {
            ...currentCounts,
            [incomingTicket.boardId]:
              (currentCounts[incomingTicket.boardId] ?? 0) + 1,
          };
        }

        if (previousTicket.boardId === incomingTicket.boardId) {
          return currentCounts;
        }

        return {
          ...currentCounts,
          [previousTicket.boardId]: Math.max(
            0,
            (currentCounts[previousTicket.boardId] ?? 0) - 1,
          ),
          [incomingTicket.boardId]:
            (currentCounts[incomingTicket.boardId] ?? 0) + 1,
        };
      });

      if (
        options?.syncSelectedTicket === false ||
        (isModalOpen && options?.syncSelectedTicket !== true)
      ) {
        return;
      }

      setSelectedTicket((currentTicket) =>
        currentTicket?.id === incomingTicket.id ? incomingTicket : currentTicket,
      );
    },
    [
      auth0Email,
      auth0Name,
      auth0Sub,
      currentUser,
      isModalOpen,
      selectedTicket,
    ],
  );

  const applyArchivedTicketLocally = useCallback(
    (incomingTicket: ArchivedTicket) => {
      setOperationalTickets((currentTickets) =>
        removeById(currentTickets, incomingTicket.id),
      );
      setMySubmissionsTickets((currentTickets) =>
        removeById(currentTickets, incomingTicket.id),
      );
      setArchivedTickets((currentTickets) => upsertById(currentTickets, incomingTicket));

      setBoardCountsById((currentCounts) => ({
        ...currentCounts,
        [incomingTicket.boardId]: Math.max(
          0,
          (currentCounts[incomingTicket.boardId] ?? 0) - 1,
        ),
      }));

      if (selectedTicket?.id === incomingTicket.id) {
        setIsModalOpen(false);
        setSelectedTicket(null);
      }
    },
    [selectedTicket],
  );

  const removeTicketLocally = useCallback(
    (ticketId: string) => {
      const normalizedTicketId = ticketId.trim();
      if (!normalizedTicketId) {
        return;
      }

      setOperationalTickets((currentTickets) =>
        removeById(currentTickets, normalizedTicketId),
      );
      setMySubmissionsTickets((currentTickets) =>
        removeById(currentTickets, normalizedTicketId),
      );
      setArchivedTickets((currentTickets) => removeById(currentTickets, normalizedTicketId));

      const removedTicket =
        operationalTicketsRef.current.find((ticket) => ticket.id === normalizedTicketId) ??
        mySubmissionsTicketsRef.current.find((ticket) => ticket.id === normalizedTicketId);
      if (removedTicket) {
        setBoardCountsById((currentCounts) => ({
          ...currentCounts,
          [removedTicket.boardId]: Math.max(
            0,
            (currentCounts[removedTicket.boardId] ?? 0) - 1,
          ),
        }));
      }

      if (selectedTicket?.id === normalizedTicketId) {
        setIsModalOpen(false);
        setSelectedTicket(null);
      }
    },
    [selectedTicket],
  );

  /** Operational list (paged or unpaged per `buildActiveTicketQueryOptions`). */
  const loadOperationalTicketsFromServer = useCallback(
    async (
      token: string,
      options?: { isCancelled?: () => boolean },
    ) => {
      const data = await ticketService.getAll(
        token,
        buildActiveTicketQueryOptions(),
      );
      if (options?.isCancelled?.()) {
        return;
      }
      applyTicketPageResponse(data, "operational");
    },
    [applyTicketPageResponse, buildActiveTicketQueryOptions],
  );

  /** Requester / owner scope (always full list from API). */
  const loadMySubmissionsFromServer = useCallback(
    async (
      token: string,
      options?: { isCancelled?: () => boolean },
    ) => {
      const items = await ticketService.getMySubmissions(token);
      if (options?.isCancelled?.()) {
        return;
      }
      const page: PagedTicketList = {
        items,
        page: 1,
        pageSize: Math.max(items.length, 1),
        totalCount: items.length,
        totalPages: 1,
      };
      applyTicketPageResponse(page, "mySubmissions");
    },
    [applyTicketPageResponse],
  );

  /** Full refresh: approved operational work + my submissions (for tab switches without extra round-trips). */
  const fetchActiveTicketList = useCallback(
    async (
      providedToken?: string,
      options?: { isCancelled?: () => boolean },
    ) => {
      const token = providedToken ?? (await getApiToken());
      if (options?.isCancelled?.()) {
        return;
      }

      await Promise.all([
        loadOperationalTicketsFromServer(token, options),
        loadMySubmissionsFromServer(token, options),
      ]);

      setApiUnavailable(false);
      lastTicketChangeSyncUtcRef.current = new Date().toISOString();
      lastTicketFullRefreshAtRef.current = Date.now();
    },
    [
      getApiToken,
      loadMySubmissionsFromServer,
      loadOperationalTicketsFromServer,
      setApiUnavailable,
    ],
  );

  const refreshTicketsSilently = useCallback(
    async (providedToken?: string) => {
      if (ticketSilentRefreshInFlightRef.current) {
        return;
      }

      ticketSilentRefreshInFlightRef.current = true;
      const requestId = ++ticketSilentRefreshRequestIdRef.current;

      try {
        const token = providedToken ?? (await getApiToken());
        await Promise.all([
          fetchActiveTicketList(token),
          loadBoardCountsSilently(token),
        ]);

        if (requestId !== ticketSilentRefreshRequestIdRef.current) {
          return;
        }
      } catch (error) {
        console.error("Failed to refresh tickets silently", error);

        if (requestId !== ticketSilentRefreshRequestIdRef.current) {
          return;
        }

        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        }
      } finally {
        if (requestId === ticketSilentRefreshRequestIdRef.current) {
          ticketSilentRefreshInFlightRef.current = false;
        }
      }
    },
    [
      fetchActiveTicketList,
      getApiToken,
      isLikelyNetworkError,
      loadBoardCountsSilently,
      setApiUnavailable,
    ],
  );

  const loadAllTickets = useCallback(
    async (providedToken?: string) => {
      setLoading(true);
      setError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        await Promise.all([
          fetchActiveTicketList(token),
          loadBoardCountsSilently(token),
        ]);
        skipNextServerPagingEffectRef.current = true;
        ticketsLoadedRef.current = true;
        setNeedsConsent(false);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load tickets", error);

        if (isConsentRequiredError(error)) {
          setApiUnavailable(false);
          setNeedsConsent(true);
          setError("CORTEX API consent is required before tickets can load.");
        } else if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setError("You do not have permission to view tickets.");
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setError(API_USER_MESSAGES.loadTickets);
        }
      } finally {
        setLoading(false);
      }
    },
    [
      fetchActiveTicketList,
      getApiToken,
      isConsentRequiredError,
      isForbiddenError,
      isLikelyNetworkError,
      loadBoardCountsSilently,
      setApiUnavailable,
      setError,
      setLoading,
      setNeedsConsent,
    ],
  );

  const syncTicketChangesSilently = useCallback(
    async (providedToken?: string) => {
      if (ticketChangeSyncInFlightRef.current) {
        return;
      }

      if (myTicketsOnly) {
        await refreshTicketsSilently(providedToken);
        return;
      }

      const sinceUtc = lastTicketChangeSyncUtcRef.current;
      if (!sinceUtc) {
        await refreshTicketsSilently(providedToken);
        return;
      }

      if (Date.now() - lastTicketFullRefreshAtRef.current >= 60_000) {
        await refreshTicketsSilently(providedToken);
        return;
      }

      ticketChangeSyncInFlightRef.current = true;
      const syncStartedAt = new Date().toISOString();

      try {
        const token = providedToken ?? (await getApiToken());
        const listOptions =
          selectedBoardId === "all"
            ? { sinceUtc }
            : { sinceUtc, boardId: selectedBoardId };
        const [updatedPage, archivedPage, nextBoardCounts] = await Promise.all([
          ticketService.getAll(token, listOptions),
          ticketService.getArchived(token, listOptions),
          ticketService.getBoardCounts(token),
        ]);

        const updatedTickets = updatedPage.items;
        const archivedTicketsDelta = archivedPage.items;

        const updatedTicketIds = new Set(updatedTickets.map((ticket) => ticket.id));
        const archivedTicketIds = new Set(
          archivedTicketsDelta.map((ticket) => ticket.id),
        );

        setOperationalTickets((currentTickets) => {
          let nextTickets =
            archivedTicketIds.size > 0
              ? currentTickets.filter((ticket) => !archivedTicketIds.has(ticket.id))
              : currentTickets;

          for (const ticket of updatedTickets) {
            nextTickets = upsertById(nextTickets, ticket);
          }

          return nextTickets;
        });

        setMySubmissionsTickets((currentTickets) => {
          let next =
            archivedTicketIds.size > 0
              ? currentTickets.filter((ticket) => !archivedTicketIds.has(ticket.id))
              : currentTickets;
          const hadIds = new Set(currentTickets.map((t) => t.id));
          for (const ticket of updatedTickets) {
            if (hadIds.has(ticket.id)) {
              next = upsertById(next, ticket);
            }
          }
          return next;
        });

        setArchivedTickets((currentTickets) => {
          let nextTickets =
            updatedTicketIds.size > 0
              ? currentTickets.filter((ticket) => !updatedTicketIds.has(ticket.id))
              : currentTickets;

          for (const ticket of archivedTicketsDelta) {
            nextTickets = upsertById(nextTickets, ticket);
          }

          return nextTickets;
        });
        setBoardCountsById(nextBoardCounts);

        if (selectedTicket && archivedTicketIds.has(selectedTicket.id)) {
          setIsModalOpen(false);
          setSelectedTicket(null);
        } else if (!isModalOpen && selectedTicket) {
          const updatedSelectedTicket = updatedTickets.find(
            (ticket) => ticket.id === selectedTicket.id,
          );
          if (updatedSelectedTicket) {
            setSelectedTicket(updatedSelectedTicket);
          }
        }

        setApiUnavailable(false);
        lastTicketChangeSyncUtcRef.current = syncStartedAt;
      } catch (error) {
        console.error("Failed to sync ticket changes", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        }
      } finally {
        ticketChangeSyncInFlightRef.current = false;
      }
    },
    [
      getApiToken,
      isLikelyNetworkError,
      isModalOpen,
      myTicketsOnly,
      refreshTicketsSilently,
      selectedBoardId,
      selectedTicket,
      setApiUnavailable,
    ],
  );

  const reconcileTicketByIdSilently = useCallback(
    async (
      ticketId: string,
      providedToken?: string,
      options?: { syncSelectedTicket?: boolean },
    ): Promise<Ticket | null> => {
      const normalizedTicketId = ticketId.trim();
      if (!normalizedTicketId) {
        return null;
      }

      if (ticketReconcileInFlightRef.current.has(normalizedTicketId)) {
        return null;
      }

      ticketReconcileInFlightRef.current.add(normalizedTicketId);
      try {
        const token = providedToken ?? (await getApiToken());
        const fetchedTicket = await ticketService.getById(normalizedTicketId, token);
        upsertActiveTicketLocally(fetchedTicket, {
          syncSelectedTicket: options?.syncSelectedTicket,
        });
        setApiUnavailable(false);
        return fetchedTicket;
      } catch (error) {
        if (
          isForbiddenError(error) ||
          (error instanceof Error &&
            "status" in error &&
            (error as { status?: number }).status === 404)
        ) {
          removeTicketLocally(normalizedTicketId);
          return null;
        }

        console.error("Failed to reconcile ticket", error);
        if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        }
        return null;
      } finally {
        ticketReconcileInFlightRef.current.delete(normalizedTicketId);
      }
    },
    [
      getApiToken,
      isForbiddenError,
      isLikelyNetworkError,
      removeTicketLocally,
      setApiUnavailable,
      upsertActiveTicketLocally,
    ],
  );

  const loadArchivedTickets = useCallback(
    async (
      providedToken?: string,
      options?: { fullCatalog?: boolean; append?: boolean; forceRefresh?: boolean },
    ) => {
      const append = options?.append === true;
      if (append) {
        setArchivedLoadingMore(true);
      } else if (archivedTickets.length === 0) {
        setArchivedLoading(true);
      }
      setArchivedError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const fullCatalog = options?.fullCatalog === true;
        const boardScope =
          fullCatalog || selectedBoardId === "all" ? undefined : selectedBoardId;
        const scopeKey = boardScope === undefined ? "all" : `board:${boardScope}`;
        const cached = archivedCacheRef.current.get(scopeKey);
        const cacheIsValid =
          cached &&
          Date.now() - cached.fetchedAt < ARCHIVED_CACHE_TTL_MS &&
          options?.forceRefresh !== true;

        if (!append && cacheIsValid) {
          setArchivedTickets(cached.items);
          setArchivedHasMore(cached.page < cached.totalPages);
          setApiUnavailable(false);
          return;
        }

        const nextPage = append ? (cached?.page ?? 0) + 1 : 1;
        if (append && cached && cached.page >= cached.totalPages) {
          setArchivedHasMore(false);
          return;
        }

        const requestOptions: TicketListQueryOptions = {
          page: nextPage,
          pageSize: ARCHIVED_PAGE_SIZE,
          ...(boardScope !== undefined ? { boardId: boardScope } : {}),
        };
        const data = await ticketService.getArchived(token, requestOptions);
        const mergedItems = append
          ? [...(cached?.items ?? []), ...data.items]
          : data.items;
        const dedupedItems = Array.from(
          new Map(mergedItems.map((ticket) => [ticket.id, ticket])).values(),
        ).sort(
          (left, right) =>
            parseTicketTime(right.archivedDate) - parseTicketTime(left.archivedDate),
        );

        archivedCacheRef.current.set(scopeKey, {
          items: dedupedItems,
          page: data.page,
          totalPages: data.totalPages,
          fetchedAt: Date.now(),
        });

        setArchivedTickets(dedupedItems);
        setArchivedHasMore(data.page < data.totalPages);
        setApiUnavailable(false);
      } catch (error) {
        console.error("Failed to load archived tickets", error);

        if (isForbiddenError(error)) {
          setApiUnavailable(false);
          setArchivedError("You do not have permission to view archived tickets.");
        } else if (isLikelyNetworkError(error)) {
          setApiUnavailable(true);
        } else {
          setApiUnavailable(false);
          setArchivedError("Failed to load archived tickets.");
        }
      } finally {
        if (append) {
          setArchivedLoadingMore(false);
        } else {
          setArchivedLoading(false);
        }
      }
    },
    [
      archivedTickets.length,
      getApiToken,
      isForbiddenError,
      isLikelyNetworkError,
      selectedBoardId,
      setApiUnavailable,
    ],
  );

  useEffect(() => {
    const previous = previousSelectedBoardIdRef.current;
    previousSelectedBoardIdRef.current = selectedBoardId;
    if (previous !== selectedBoardId) {
      void loadArchivedTickets();
    }
  }, [loadArchivedTickets, selectedBoardId]);

  const archivedTicketsForView = useMemo(() => {
    const boardScoped =
      selectedBoardId === "all"
        ? archivedTickets
        : archivedTickets.filter((ticket) => ticket.boardId === selectedBoardId);

    const search = normalize(debouncedArchivedSearchQuery);
    if (!search) {
      return boardScoped;
    }

    return boardScoped.filter((ticket) => {
      const haystack = [
        ticket.id,
        ticket.title,
        ticket.status,
        ticket.priority,
        ticket.boardName,
        ticket.synitiOwner,
        ticket.businessOwner,
        ticket.archivedByDisplayName,
        ticket.createdByDisplayName,
      ]
        .map((value) => normalize(String(value ?? "")))
        .join(" ");
      return haystack.includes(search);
    });
  }, [archivedTickets, debouncedArchivedSearchQuery, selectedBoardId]);

  useLayoutEffect(() => {
    if (!ticketsLoadedRef.current) {
      return;
    }
    if (!useServerDrivenPaging) {
      lastBoardForCacheRestoreRef.current = selectedBoardId;
      return;
    }
    if (lastBoardForCacheRestoreRef.current === null) {
      lastBoardForCacheRestoreRef.current = selectedBoardId;
      return;
    }
    if (lastBoardForCacheRestoreRef.current === selectedBoardId) {
      return;
    }
    lastBoardForCacheRestoreRef.current = selectedBoardId;
    const cacheKey = selectedBoardId === "all" ? "all" : `board:${selectedBoardId}`;
    const cached = activeBoardPageCacheRef.current.get(cacheKey);
    if (cached) {
      setOperationalTickets(cached.items);
      setServerTicketListMeta(cached.meta);
    }
  }, [selectedBoardId, useServerDrivenPaging]);

  useEffect(() => {
    if (!useServerDrivenPaging || !ticketsLoadedRef.current) {
      return;
    }
    if (skipNextServerPagingEffectRef.current) {
      skipNextServerPagingEffectRef.current = false;
      return;
    }

    let cancelled = false;

    void (async () => {
      try {
        const token = await getApiToken();
        await loadOperationalTicketsFromServer(token, {
          isCancelled: () => cancelled,
        });
        if (cancelled) {
          return;
        }
        setApiUnavailable(false);
        lastTicketChangeSyncUtcRef.current = new Date().toISOString();
        lastTicketFullRefreshAtRef.current = Date.now();
      } catch (e) {
        console.error("Failed to load ticket page", e);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [
    currentPage,
    debouncedSearchQuery,
    filter,
    getApiToken,
    loadOperationalTicketsFromServer,
    myTicketsOnly,
    pageSize,
    selectedBoardId,
    setApiUnavailable,
    ticketListSort,
    useClientFilterMode,
    useServerDrivenPaging,
  ]);

  const ticketsForBoard = useMemo(() => {
    const filterInput = normalize(debouncedFilterValue);
    const searchInput = normalize(debouncedSearchQuery);
    let filteredTickets =
      selectedBoardId === "all"
        ? listForTicketViews
        : listForTicketViews.filter((ticket) => ticket.boardId === selectedBoardId);

    if (filter !== "all" && filterInput) {
      if (filter === "status") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.status ?? "").includes(filterInput),
        );
      } else if (filter === "sla") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.slaStatus ?? "").includes(filterInput),
        );
      } else if (filter === "attention") {
        filteredTickets = isAttentionFilterValue(filterInput)
          ? filteredTickets.filter((ticket) =>
              ticketMatchesAttentionFilter(ticket, filterInput),
            )
          : [];
      } else {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.priority ?? "").includes(filterInput),
        );
      }
    }

    if (myTicketsOnly) {
      const creatorCandidates = getCreatorMatchCandidates(
        currentUser,
        auth0Sub,
        auth0Email,
      );
      const ownerCandidates = getOwnerMatchCandidates(
        currentUser,
        auth0Name,
        auth0Email,
      );
      if (
        creatorCandidates.size === 0 &&
        ownerCandidates.size === 0 &&
        currentUser?.id == null
      ) {
        filteredTickets = [];
      } else {
        filteredTickets = filteredTickets.filter((ticket) =>
          ticketMatchesMyTicketsScope(
            ticket,
            currentUser,
            creatorCandidates,
            ownerCandidates,
          ),
        );
      }
    }

    if (!searchInput) {
      return filteredTickets;
    }

    return filteredTickets.filter((ticket) => ticketMatchesSearch(ticket, searchInput));
  }, [
    listForTicketViews,
    auth0Sub,
    auth0Email,
    auth0Name,
    currentUser,
    debouncedFilterValue,
    debouncedSearchQuery,
    filter,
    myTicketApprovalFilter,
    myTicketsOnly,
    selectedBoardId,
  ]);

  const approvalFilteredTicketsForBoard = useMemo(() => {
    if (!myTicketsOnly || myTicketApprovalFilter === "all") {
      return ticketsForBoard;
    }

    return ticketsForBoard.filter(
      (ticket) => getTicketApprovalStatus(ticket) === myTicketApprovalFilter,
    );
  }, [myTicketApprovalFilter, myTicketsOnly, ticketsForBoard]);

  const requesterLifecycleSummary = useMemo<RequesterLifecycleSummary | null>(() => {
    if (!myTicketsOnly) {
      return null;
    }

    return mySubmissionsTickets.reduce<RequesterLifecycleSummary>(
      (summary, ticket) => {
        const status = getTicketApprovalStatus(ticket);
        summary.total += 1;
        if (status === "NeedsMoreInfo") {
          summary.needsAttention += 1;
        } else if (status === "PendingApproval") {
          summary.pendingApproval += 1;
        } else if (status === "Approved") {
          summary.activeWork += 1;
        } else if (status === "Rejected") {
          summary.rejected += 1;
        }
        return summary;
      },
      {
        total: 0,
        needsAttention: 0,
        pendingApproval: 0,
        activeWork: 0,
        rejected: 0,
      },
    );
  }, [myTicketsOnly, mySubmissionsTickets]);

  const shouldUseBoardSwitchFallback = useMemo(() => {
    if (!useServerDrivenPaging) {
      return false;
    }
    if (approvalFilteredTicketsForBoard.length > 0) {
      return false;
    }
    if (selectedBoardId === "all") {
      return false;
    }
    const sourceList = myTicketsOnly ? mySubmissionsTickets : operationalTickets;
    if (sourceList.length === 0) {
      return false;
    }
    return sourceList.every((ticket) => ticket.boardId !== selectedBoardId);
  }, [
    approvalFilteredTicketsForBoard.length,
    mySubmissionsTickets,
    myTicketsOnly,
    operationalTickets,
    selectedBoardId,
    useServerDrivenPaging,
  ]);

  useEffect(() => {
    if (approvalFilteredTicketsForBoard.length > 0) {
      boardSwitchFallbackTicketsRef.current = approvalFilteredTicketsForBoard;
    }
  }, [approvalFilteredTicketsForBoard]);

  const tickets = useMemo(() => {
    if (
      shouldUseBoardSwitchFallback &&
      boardSwitchFallbackTicketsRef.current.length > 0
    ) {
      return boardSwitchFallbackTicketsRef.current;
    }
    return approvalFilteredTicketsForBoard;
  }, [approvalFilteredTicketsForBoard, shouldUseBoardSwitchFallback]);

  const sortedTickets = useMemo(() => {
    if (useServerDrivenPaging) {
      return tickets;
    }
    return sortTicketsForList(tickets, ticketListSort, {
      requesterContext: myTicketsOnly,
    });
  }, [myTicketsOnly, ticketListSort, tickets, useServerDrivenPaging]);

  const totalTickets = useMemo(() => {
    if (
      serverTicketListMeta &&
      useServerDrivenPaging &&
      !shouldUseBoardSwitchFallback
    ) {
      return serverTicketListMeta.totalCount;
    }
    return sortedTickets.length;
  }, [
    serverTicketListMeta,
    sortedTickets.length,
    shouldUseBoardSwitchFallback,
    useServerDrivenPaging,
  ]);

  const totalPages = useMemo(() => {
    if (
      serverTicketListMeta &&
      useServerDrivenPaging &&
      !shouldUseBoardSwitchFallback
    ) {
      return Math.max(1, serverTicketListMeta.totalPages);
    }
    if (pageSize === "all") {
      return 1;
    }
    return Math.max(1, Math.ceil(totalTickets / pageSize));
  }, [
    pageSize,
    serverTicketListMeta,
    shouldUseBoardSwitchFallback,
    totalTickets,
    useServerDrivenPaging,
  ]);

  const pagedTickets = useMemo(() => {
    if (useServerDrivenPaging) {
      return sortedTickets;
    }
    if (pageSize === "all") {
      return sortedTickets;
    }

    const startIndex = (currentPage - 1) * pageSize;
    return sortedTickets.slice(startIndex, startIndex + pageSize);
  }, [currentPage, pageSize, sortedTickets, useServerDrivenPaging]);

  const numericPageSize =
    pageSize === "all" ? Math.max(totalTickets, 1) : pageSize;

  const showingStart =
    totalTickets === 0
      ? 0
      : (currentPage - 1) * numericPageSize + 1;
  const showingEnd =
    totalTickets === 0
      ? 0
      : Math.min(currentPage * numericPageSize, totalTickets);

  useEffect(() => {
    setCurrentPage(1);
  }, [
    selectedBoardId,
    filter,
    debouncedFilterValue,
    debouncedSearchQuery,
    pageSize,
    ticketListSort,
    myTicketsOnly,
  ]);

  useEffect(() => {
    setCurrentPage((page) => Math.min(page, totalPages));
  }, [totalPages]);

  const handleSaveTicket = useCallback(
    async (
      updatedTicket: TicketMutationInput,
      attachments: File[],
    ): Promise<TicketSaveResult | undefined> => {
      if (!selectedTicket) return undefined;
      const isCreateAction = !selectedTicket.id;
      const actionLabel = isCreateAction ? "create" : "update";
      const wasNeedsMoreInfo =
        !isCreateAction && selectedTicket.approvalStatus === "NeedsMoreInfo";

      try {
        const token = await getApiToken();
        let savedTicket: Ticket;
        let successMessage = isCreateAction ? "Ticket created" : "Ticket updated";

        if (isCreateAction) {
          const createPayload: CreateTicketInput = {
            title: updatedTicket.title?.trim() ?? "",
            description: updatedTicket.description?.trim() ?? "",
            priority: updatedTicket.priority?.trim() ?? "",
            department: updatedTicket.department,
            boardId: updatedTicket.boardId,
            storyPoints: updatedTicket.storyPoints,
            synitiOwner: updatedTicket.synitiOwner,
            businessOwner: updatedTicket.businessOwner,
          };

          savedTicket = await ticketService.create(createPayload, token);
          upsertActiveTicketLocally(savedTicket, { syncSelectedTicket: false });
        } else {
          savedTicket = await ticketService.update(selectedTicket.id, updatedTicket, token);
          upsertActiveTicketLocally(savedTicket, { syncSelectedTicket: true });
        }

        if (attachments.length > 0) {
          try {
            await attachmentService.upload(savedTicket.id, attachments, token);
            successMessage +=
              attachments.length === 1
                ? " with 1 attachment"
                : ` with ${attachments.length} attachments`;
          } catch (attachmentError) {
            console.error("Failed to upload attachments", attachmentError);
            notifyTicketSaveSuccess(
              isCreateAction,
              savedTicket,
              successMessage,
              0,
              {
                resubmittedForReview:
                  !isCreateAction &&
                  wasNeedsMoreInfo &&
                  savedTicket.approvalStatus === "PendingApproval",
              },
            );
            toast.error(
              getUserFacingErrorMessage(
                attachmentError,
                "Ticket saved, but attachments could not be uploaded",
              ),
            );
            setIsModalOpen(false);
            setSelectedTicket(null);
            return {
              outcome: "saved",
              savedTicket,
              shouldCloseModal: true,
            };
          }
        }

        notifyTicketSaveSuccess(
          isCreateAction,
          savedTicket,
          successMessage,
          attachments.length,
          {
            resubmittedForReview:
              !isCreateAction &&
              wasNeedsMoreInfo &&
              savedTicket.approvalStatus === "PendingApproval",
          },
        );
        if (isCreateAction) {
          setIsModalOpen(false);
          setSelectedTicket(null);
        }
        return {
          outcome: "saved",
          savedTicket,
          shouldCloseModal: isCreateAction,
        };
      } catch (error) {
        const isUpdate = Boolean(selectedTicket?.id);
        if (
          isUpdate &&
          error instanceof ApiError &&
          error.status === 409
        ) {
          try {
            const token = await getApiToken();
            const fresh = await ticketService.getById(selectedTicket.id, token);
            upsertActiveTicketLocally(fresh, { syncSelectedTicket: false });
            setSelectedTicket(fresh);
            toast.success(
              "This ticket was updated elsewhere. The latest version is loaded — review your changes, then save again.",
              { id: "ticket-conflict-reloaded" },
            );
            return { outcome: "reloaded" };
          } catch (reloadError) {
            console.error("Failed to reload ticket after save conflict", reloadError);
            toast.error(
              getUserFacingErrorMessage(
                reloadError,
                "Could not load the latest ticket. Refresh the page and try again.",
              ),
              { id: `ticket-save-error-${actionLabel}` },
            );
            throw reloadError;
          }
        }

        console.error("Failed to save ticket", error);
        toast.error(getUserFacingErrorMessage(error, API_USER_MESSAGES.saveChanges), {
          id: `ticket-save-error-${actionLabel}`,
        });
        throw error;
      }
    },
    [getApiToken, selectedTicket, upsertActiveTicketLocally],
  );

  const requestDeleteTicket = useCallback((ticket: Ticket) => {
    setTicketToDelete(ticket);
  }, []);

  const confirmDeleteTicket = useCallback(async () => {
    if (!ticketToDelete) return;

    try {
      setDeleting(true);
      const token = await getApiToken();
      await ticketService.delete(ticketToDelete.id, token);
      removeTicketLocally(ticketToDelete.id);
      toast.success("Ticket deleted");
    } catch (error) {
      console.error("Failed to delete ticket", error);
      toast.error("Failed to delete ticket");
    } finally {
      setDeleting(false);
      setTicketToDelete(null);
    }
  }, [getApiToken, removeTicketLocally, ticketToDelete]);

  const handleArchiveTicket = useCallback(
    async (ticket: Ticket, changeReason?: string) => {
      if (!ticket.id) {
        return;
      }

      try {
        const token = await getApiToken();
        const archivedTicket = await ticketService.archiveWithReason(
          ticket.id,
          changeReason,
          token,
        );

        applyArchivedTicketLocally(archivedTicket);

        setIsModalOpen(false);
        setSelectedTicket(null);
        toast.success("Ticket archived");
      } catch (error) {
        console.error("Failed to archive ticket", error);
        toast.error(getUserFacingErrorMessage(error, "Failed to archive ticket"));
        throw error;
      }
    },
    [applyArchivedTicketLocally, getApiToken],
  );

  const handleReactivateArchivedTicket = useCallback(
    async (ticket: ArchivedTicket) => {
      if (!ticket.id) {
        return;
      }

      try {
        setReactivatingArchivedTicketId(ticket.id);
        const token = await getApiToken();
        const restoredTicket = await ticketService.reactivateArchived(ticket.id, token);

        upsertActiveTicketLocally(restoredTicket, { syncSelectedTicket: false });

        toast.success(
          restoredTicket.status !== ticket.status
            ? `Ticket reactivated and reopened as ${restoredTicket.status}`
            : "Ticket reactivated",
        );
      } catch (error) {
        console.error("Failed to reactivate archived ticket", error);
        toast.error(getUserFacingErrorMessage(error, "Failed to reactivate archived ticket"));
      } finally {
        setReactivatingArchivedTicketId(null);
      }
    },
    [getApiToken, upsertActiveTicketLocally],
  );

  const closeModal = useCallback(() => {
    setIsModalOpen(false);
    setTimeout(() => {
      setSelectedTicket(null);
    }, 0);
  }, []);

  const openTicket = useCallback((ticket: Ticket) => {
    setSelectedTicket(ticket);
    setIsModalOpen(true);
  }, []);

  const openTicketById = useCallback(
    async (ticketId: string, providedToken?: string) => {
      const existingTicket =
        operationalTickets.find((ticket) => ticket.id === ticketId) ??
        mySubmissionsTickets.find((ticket) => ticket.id === ticketId);
      if (existingTicket) {
        openTicket(existingTicket);
        return;
      }

      const token = providedToken ?? (await getApiToken());
      const fetchedTicket = await ticketService.getById(ticketId, token);

      upsertActiveTicketLocally(fetchedTicket, { syncSelectedTicket: false });

      openTicket(fetchedTicket);
    },
    [
      getApiToken,
      mySubmissionsTickets,
      openTicket,
      operationalTickets,
      upsertActiveTicketLocally,
    ],
  );

  return {
    allTickets,
    boardCountsById,
    setAllTickets,
    filter,
    setFilter,
    filterValue,
    setFilterValue,
    selectedBoardId,
    setSelectedBoardId,
    searchQuery,
    setSearchQuery,
    pageSize,
    setPageSize,
    ticketListSort,
    setTicketListSort,
    myTicketsOnly,
    setMyTicketsOnly,
    myTicketApprovalFilter,
    setMyTicketApprovalFilter,
    currentPage,
    setCurrentPage,
    selectedTicket,
    setSelectedTicket,
    isModalOpen,
    setIsModalOpen,
    archivedTickets,
    archivedSearchQuery,
    setArchivedSearchQuery,
    archivedTicketsForView,
    setArchivedTickets,
    archivedLoading,
    archivedLoadingMore,
    archivedHasMore,
    archivedError,
    requesterLifecycleSummary,
    highlightedArchivedTicketId,
    setHighlightedArchivedTicketId,
    reactivatingArchivedTicketId,
    ticketToDelete,
    setTicketToDelete,
    deleting,
    refreshTicketsSilently,
    syncTicketChangesSilently,
    reconcileTicketByIdSilently,
    upsertActiveTicketLocally,
    applyArchivedTicketLocally,
    removeTicketLocally,
    loadAllTickets,
    loadArchivedTickets,
    tickets,
    sortedTickets,
    totalTickets,
    totalPages,
    pagedTickets,
    showingStart,
    showingEnd,
    handleSaveTicket,
    requestDeleteTicket,
    confirmDeleteTicket,
    handleArchiveTicket,
    handleReactivateArchivedTicket,
    closeModal,
    openTicket,
    openTicketById,
  };
}
