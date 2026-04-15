import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import type { ArchivedTicket } from "../types/archivedTicket";
import type { CreateTicketInput, Ticket, TicketMutationInput } from "../types/ticket";
import type { UserProfile } from "../types/user";
import {
  API_USER_MESSAGES,
  attachmentService,
  getUserFacingErrorMessage,
  ticketService,
} from "../services/api";
import toast from "react-hot-toast";

export type FilterOption = "all" | "status" | "priority" | "sla";
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

function normalize(value: string) {
  return value.trim().toLowerCase();
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
  ]) {
    const normalized = normalize(String(value ?? ""));
    if (normalized) {
      candidates.add(normalized);
    }
  }
  return candidates;
}

function ticketIsOwnedByCurrentUser(ticket: Ticket, candidates: Set<string>): boolean {
  const syn = normalize(String(ticket.synitiOwner ?? ""));
  const bus = normalize(String(ticket.businessOwner ?? ""));
  if (!syn && !bus) {
    return false;
  }
  return (syn !== "" && candidates.has(syn)) || (bus !== "" && candidates.has(bus));
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

function sortTicketsForList(tickets: Ticket[], sort: TicketListSortOption): Ticket[] {
  const copy = [...tickets];

  switch (sort) {
    case "newest-first":
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

interface UseTicketsParams {
  getApiToken: (providedToken?: string) => Promise<string>;
  setApiUnavailable: Dispatch<SetStateAction<boolean>>;
  setLoading: Dispatch<SetStateAction<boolean>>;
  setError: Dispatch<SetStateAction<string | null>>;
  setNeedsConsent: Dispatch<SetStateAction<boolean>>;
  currentUser: UserProfile | null;
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
  auth0Name,
  auth0Email,
  isConsentRequiredError,
  isForbiddenError,
  isLikelyNetworkError,
}: UseTicketsParams) {
  const [allTickets, setAllTickets] = useState<Ticket[]>([]);
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
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [archivedTickets, setArchivedTickets] = useState<ArchivedTicket[]>([]);
  const [archivedLoading, setArchivedLoading] = useState(false);
  const [archivedError, setArchivedError] = useState<string | null>(null);
  const [highlightedArchivedTicketId, setHighlightedArchivedTicketId] =
    useState<string | null>(null);
  const [reactivatingArchivedTicketId, setReactivatingArchivedTicketId] =
    useState<string | null>(null);
  const [ticketToDelete, setTicketToDelete] = useState<Ticket | null>(null);
  const [deleting, setDeleting] = useState(false);
  const ticketSilentRefreshInFlightRef = useRef(false);
  const ticketSilentRefreshRequestIdRef = useRef(0);

  const refreshTicketsSilently = useCallback(
    async (providedToken?: string) => {
      if (ticketSilentRefreshInFlightRef.current) {
        return;
      }

      ticketSilentRefreshInFlightRef.current = true;
      const requestId = ++ticketSilentRefreshRequestIdRef.current;

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await ticketService.getAll(token);

        if (requestId !== ticketSilentRefreshRequestIdRef.current) {
          return;
        }

        setAllTickets(data);
        setApiUnavailable(false);
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
    [getApiToken, isLikelyNetworkError, setApiUnavailable],
  );

  const loadAllTickets = useCallback(
    async (providedToken?: string) => {
      setLoading(true);
      setError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await ticketService.getAll(token);
        setAllTickets(data);
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
      getApiToken,
      isConsentRequiredError,
      isForbiddenError,
      isLikelyNetworkError,
      setApiUnavailable,
      setError,
      setLoading,
      setNeedsConsent,
    ],
  );

  const loadArchivedTickets = useCallback(
    async (providedToken?: string) => {
      setArchivedLoading(true);
      setArchivedError(null);

      try {
        const token = providedToken ?? (await getApiToken());
        const data = await ticketService.getArchived(token);
        setArchivedTickets(data);
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
        setArchivedLoading(false);
      }
    },
    [getApiToken, isForbiddenError, isLikelyNetworkError, setApiUnavailable],
  );

  const tickets = useMemo(() => {
    const filterInput = normalize(debouncedFilterValue);
    const searchInput = normalize(debouncedSearchQuery);
    let filteredTickets =
      selectedBoardId === "all"
        ? allTickets
        : allTickets.filter((ticket) => ticket.boardId === selectedBoardId);

    if (filter !== "all" && filterInput) {
      if (filter === "status") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.status ?? "").includes(filterInput),
        );
      } else if (filter === "sla") {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.slaStatus ?? "").includes(filterInput),
        );
      } else {
        filteredTickets = filteredTickets.filter((ticket) =>
          normalize(ticket.priority ?? "").includes(filterInput),
        );
      }
    }

    if (myTicketsOnly) {
      const candidates = getOwnerMatchCandidates(currentUser, auth0Name, auth0Email);
      if (candidates.size === 0) {
        filteredTickets = [];
      } else {
        filteredTickets = filteredTickets.filter((ticket) =>
          ticketIsOwnedByCurrentUser(ticket, candidates),
        );
      }
    }

    if (!searchInput) {
      return filteredTickets;
    }

    return filteredTickets.filter((ticket) => ticketMatchesSearch(ticket, searchInput));
  }, [
    allTickets,
    auth0Email,
    auth0Name,
    currentUser,
    debouncedFilterValue,
    debouncedSearchQuery,
    filter,
    myTicketsOnly,
    selectedBoardId,
  ]);

  const sortedTickets = useMemo(
    () => sortTicketsForList(tickets, ticketListSort),
    [ticketListSort, tickets],
  );

  const totalTickets = sortedTickets.length;
  const totalPages =
    pageSize === "all" ? 1 : Math.max(1, Math.ceil(totalTickets / pageSize));
  const pagedTickets = useMemo(() => {
    if (pageSize === "all") {
      return sortedTickets;
    }

    const startIndex = (currentPage - 1) * pageSize;
    return sortedTickets.slice(startIndex, startIndex + pageSize);
  }, [currentPage, pageSize, sortedTickets]);
  const showingStart =
    totalTickets === 0
      ? 0
      : (currentPage - 1) * (pageSize === "all" ? totalTickets : pageSize) + 1;
  const showingEnd =
    pageSize === "all" ? totalTickets : Math.min(totalTickets, currentPage * pageSize);

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
    async (updatedTicket: TicketMutationInput, attachments: File[]) => {
      if (!selectedTicket) return;
      const isCreateAction = !selectedTicket.id;
      const actionLabel = isCreateAction ? "create" : "update";

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
          setAllTickets((prev) => [savedTicket, ...prev]);
        } else {
          savedTicket = await ticketService.update(selectedTicket.id, updatedTicket, token);
          setAllTickets((prev) =>
            prev.map((ticket) => (ticket.id === savedTicket.id ? savedTicket : ticket)),
          );
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
            toast.success(successMessage, { id: "ticket-save-success" });
            toast.error(
              getUserFacingErrorMessage(
                attachmentError,
                "Ticket saved, but attachments could not be uploaded",
              ),
            );
            setIsModalOpen(false);
            setSelectedTicket(null);
            return;
          }
        }

        toast.success(successMessage, { id: "ticket-save-success" });
        setIsModalOpen(false);
        setSelectedTicket(null);
      } catch (error) {
        console.error("Failed to save ticket", error);
        toast.error(getUserFacingErrorMessage(error, API_USER_MESSAGES.saveChanges), {
          id: `ticket-save-error-${actionLabel}`,
        });
        throw error;
      }
    },
    [getApiToken, selectedTicket],
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
      setAllTickets((prev) => prev.filter((ticket) => ticket.id !== ticketToDelete.id));
      toast.success("Ticket deleted");
    } catch (error) {
      console.error("Failed to delete ticket", error);
      toast.error("Failed to delete ticket");
    } finally {
      setDeleting(false);
      setTicketToDelete(null);
    }
  }, [getApiToken, ticketToDelete]);

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

        setAllTickets((currentTickets) =>
          currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
        );
        setArchivedTickets((currentTickets) => [
          archivedTicket,
          ...currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
        ]);

        setIsModalOpen(false);
        setSelectedTicket(null);
        toast.success("Ticket archived");
      } catch (error) {
        console.error("Failed to archive ticket", error);
        toast.error(getUserFacingErrorMessage(error, "Failed to archive ticket"));
        throw error;
      }
    },
    [getApiToken],
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

        setArchivedTickets((currentTickets) =>
          currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
        );
        setAllTickets((currentTickets) => [
          restoredTicket,
          ...currentTickets.filter((currentTicket) => currentTicket.id !== ticket.id),
        ]);

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
    [getApiToken],
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
      const existingTicket = allTickets.find((ticket) => ticket.id === ticketId);
      if (existingTicket) {
        openTicket(existingTicket);
        return;
      }

      const token = providedToken ?? (await getApiToken());
      const fetchedTicket = await ticketService.getById(ticketId, token);

      setAllTickets((currentTickets) => {
        if (currentTickets.some((ticket) => ticket.id === fetchedTicket.id)) {
          return currentTickets;
        }

        return [fetchedTicket, ...currentTickets];
      });

      openTicket(fetchedTicket);
    },
    [allTickets, getApiToken, openTicket],
  );

  return {
    allTickets,
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
    currentPage,
    setCurrentPage,
    selectedTicket,
    setSelectedTicket,
    isModalOpen,
    setIsModalOpen,
    archivedTickets,
    setArchivedTickets,
    archivedLoading,
    archivedError,
    highlightedArchivedTicketId,
    setHighlightedArchivedTicketId,
    reactivatingArchivedTicketId,
    ticketToDelete,
    setTicketToDelete,
    deleting,
    refreshTicketsSilently,
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
