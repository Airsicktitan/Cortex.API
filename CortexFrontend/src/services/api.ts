import type {
  CreateTicketInput,
  ReassignmentApplyRequest,
  ReassignmentApplyResponse,
  ReviewerQualitySignalMetricsPayload,
  Ticket,
  TicketMutationInput,
  TicketTriageApplyRequest,
  TicketTriageGenerateApiResponse,
} from "../types/ticket";
import type { ArchivedTicket } from "../types/archivedTicket";
import type {
  PagedArchivedTicketList,
  PagedTicketList,
} from "../types/pagedList";
import type { TicketAttachment } from "../types/attachment";
import type { ScreenshotInsightResult } from "../types/screenshotInsight";
import type { TicketAuditEntry } from "../types/ticketAudit";
import type {
  OwnerWorkloadPreviewRequest,
  OwnerWorkloadPreviewResponse,
  RoutingPreviewRequest,
  RoutingPreviewResponse,
  TicketRoutingLatestResponse,
} from "../types/ticketRoutingInsight";
import type { WorkflowMetricsSnapshot } from "../types/workflowMetrics";
import type {
  RepeatIssueAiReviewResponse,
  RepeatIssueGroupDetailResponse,
  RepeatIssueOverviewResponse,
} from "../types/repeatIssues";
import type {
  CortexDecisionResult,
  RebalanceSuggestion,
  WorkloadSnapshot,
} from "../types/cortexDecision";
import type { CortexAiAssessment } from "../types/cortexAiAssessment";
import type {
  AdminUpdateUserInput,
  Auth0RoleOption,
  CreateUserInput,
  OnlineUser,
  SyncUsersFromAuth0Result,
  UpdateUserProfileInput,
  UserAuth0RolesResponse,
  UserDirectoryEntry,
  UserProfile,
  UserRecord,
  UserRoleMutationRequest,
} from "../types/user";
import {
  isClarityState,
  type IntakeAssistRequest,
  type IntakeAssistResult,
} from "../types/intakeAssist";

const API_BASE_URL = import.meta.env.VITE_API_URL;

export type TicketListQueryOptions = {
  sinceUtc?: string;
  /** When set, the API applies SQL filtering to this board only (reduces payload for board-scoped sync). */
  boardId?: number;
  page?: number;
  pageSize?: number;
  sort?: string;
  /** Loads the full visible list (ignores page/pageSize on the server). */
  unpaged?: boolean;
};

function withTicketQuery(path: string, options?: TicketListQueryOptions) {
  if (!options) {
    return path;
  }

  const searchParams = new URLSearchParams();
  if (options.sinceUtc) {
    searchParams.set("sinceUtc", options.sinceUtc);
  }
  if (options.boardId !== undefined) {
    searchParams.set("boardId", String(options.boardId));
  }
  if (options.page !== undefined) {
    searchParams.set("page", String(options.page));
  }
  if (options.pageSize !== undefined) {
    searchParams.set("pageSize", String(options.pageSize));
  }
  if (options.sort) {
    searchParams.set("sort", options.sort);
  }
  if (options.unpaged === true) {
    searchParams.set("unpaged", "true");
  }

  const qs = searchParams.toString();
  return qs ? `${path}?${qs}` : path;
}

const authHeaders = (token: string, includeJson = false): HeadersInit => ({
  ...(includeJson ? { "Content-Type": "application/json" } : {}),
  Authorization: `Bearer ${token}`,
});

/** User-visible copy only — use with `ensureSuccess` / `getUserFacingErrorMessage`. */
export const API_USER_MESSAGES = {
  generic: "Something went wrong. Please try again.",
  loadTickets: "Unable to load tickets",
  saveChanges: "Unable to save changes",
} as const;

export class ApiError extends Error {
  status: number;
  /** Parsed server body for diagnostics; never display to end users. */
  rawMessage?: string;
  /** Optional machine-readable sentinel from the backend (e.g. ACCESS_NOT_APPROVED). */
  code?: string;

  constructor(
    message: string,
    status: number,
    rawMessage?: string,
    code?: string,
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.rawMessage = rawMessage;
    this.code = code;
  }
}

interface ParsedErrorBody {
  message: string;
  code?: string;
}

async function readErrorMessage(
  response: Response,
  fallbackMessage: string,
): Promise<ParsedErrorBody> {
  try {
    const data = (await response.json()) as unknown;

    if (typeof data === "string" && data.trim()) {
      return { message: data };
    }

    if (typeof data === "object" && data !== null) {
      const codeRaw = "code" in data ? (data as { code?: unknown }).code : undefined;
      const code =
        typeof codeRaw === "string" && codeRaw.trim() ? codeRaw.trim() : undefined;

      const detail = "detail" in data ? data.detail : undefined;
      if (typeof detail === "string" && detail.trim()) {
        return { message: detail, code };
      }

      const message = "message" in data ? data.message : undefined;
      if (typeof message === "string" && message.trim()) {
        return { message, code };
      }

      const title = "title" in data ? data.title : undefined;
      if (typeof title === "string" && title.trim()) {
        return { message: title, code };
      }

      if (code) {
        return { message: fallbackMessage, code };
      }
    }
  } catch {
    // Ignore unreadable response bodies and fall back to the provided message.
  }

  return { message: fallbackMessage };
}

/** Always throws with a caller-supplied, user-safe `userMessage`. Raw bodies are kept on `rawMessage` for dev logging only. */
export async function ensureSuccess(
  response: Response,
  userMessage: string,
): Promise<void> {
  if (response.ok) {
    return;
  }

  const parsed = await readErrorMessage(response, userMessage);
  const effectiveMessage =
    parsed.message.trim() && parsed.message !== userMessage
      ? parsed.message
      : userMessage;
  const rawMessage =
    effectiveMessage !== userMessage ? effectiveMessage : undefined;

  if (import.meta.env.DEV && rawMessage) {
    console.warn("[API error]", response.status, userMessage, rawMessage);
  }

  throw new ApiError(effectiveMessage, response.status, rawMessage, parsed.code);
}

/**
 * True when the backend explicitly signalled that the authenticated identity is not
 * approved for Cortex (distinct from a generic 403 "missing permission" case).
 */
export function isAccessNotApprovedError(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    error.status === 403 &&
    error.code === "ACCESS_NOT_APPROVED"
  );
}

export function isLikelyNetworkError(error: unknown): boolean {
  if (error instanceof ApiError) {
    return false;
  }

  if (error instanceof TypeError) {
    return true;
  }

  if (error instanceof Error) {
    const normalizedMessage = error.message.toLowerCase();
    return (
      normalizedMessage.includes("failed to fetch") ||
      normalizedMessage.includes("networkerror") ||
      normalizedMessage.includes("load failed")
    );
  }

  return false;
}

export function getUserFacingErrorMessage(
  error: unknown,
  fallback: string,
): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (isLikelyNetworkError(error)) {
    return "Unable to connect. Please try again.";
  }

  return fallback;
}

export const ticketService = {
  // Get User
  async getCurrentUser(token: string) {
    const response = await fetch(`${API_BASE_URL}/users/me`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load current user");
    return response.json();
  },

  async getAll(
    token: string,
    options?: TicketListQueryOptions,
  ): Promise<PagedTicketList> {
    const response = await fetch(withTicketQuery(`${API_BASE_URL}/tickets`, options), {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json() as Promise<PagedTicketList>;
  },

  async getMySubmissions(token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/my-submissions`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json() as Promise<Ticket[]>;
  },

  async getBoardCounts(token: string): Promise<Record<number, number>> {
    const response = await fetch(`${API_BASE_URL}/tickets/board-counts`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    const data = (await response.json()) as Array<{
      boardId?: number;
      count?: number;
    }>;

    const counts: Record<number, number> = {};
    for (const entry of data) {
      if (
        typeof entry?.boardId === "number" &&
        Number.isFinite(entry.boardId)
      ) {
        counts[entry.boardId] =
          typeof entry.count === "number" && Number.isFinite(entry.count)
            ? entry.count
            : 0;
      }
    }

    return counts;
  },

  // Get ticket by ID
  async getById(id: string, token: string): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load ticket");
    return response.json();
  },

  // Get tickets by status
  async getByStatus(status: string, token: string): Promise<Ticket[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/status/${status}`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json();
  },

  // Get tickets by priority
  async getByPriority(priority: string, token: string): Promise<Ticket[]> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/priority/${priority}`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json();
  },

  async getArchived(
    token: string,
    options?: TicketListQueryOptions,
  ): Promise<PagedArchivedTicketList> {
    const response = await fetch(
      withTicketQuery(`${API_BASE_URL}/tickets/archived`, options),
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to load archived tickets");
    return response.json() as Promise<PagedArchivedTicketList>;
  },

  async getHistory(id: string, token: string): Promise<TicketAuditEntry[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/history`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load ticket history");
    return response.json();
  },

  async getLatestRouting(
    id: string,
    token: string,
  ): Promise<TicketRoutingLatestResponse> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${id}/routing/latest`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to load recommendation details");
    return response.json() as Promise<TicketRoutingLatestResponse>;
  },

  async getTicketDecision(
    id: string,
    token: string,
  ): Promise<CortexDecisionResult> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/decision`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load Cortex decision");
    return response.json() as Promise<CortexDecisionResult>;
  },

  async postWorkloadPreview(
    body: OwnerWorkloadPreviewRequest,
    token: string,
  ): Promise<OwnerWorkloadPreviewResponse> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/routing/workload-preview`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: JSON.stringify({
          ownerKeys: body.ownerKeys,
          excludeTicketId: body.excludeTicketId ?? undefined,
        }),
      },
    );

    await ensureSuccess(response, "Workload preview unavailable");
    return response.json() as Promise<OwnerWorkloadPreviewResponse>;
  },

  async postRoutingPreview(
    body: RoutingPreviewRequest,
    token: string,
  ): Promise<RoutingPreviewResponse> {
    const response = await fetch(`${API_BASE_URL}/tickets/routing/preview`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({
        ticketId: body.ticketId,
        boardId: body.boardId,
        priority: body.priority,
        title: body.title,
        department: body.department,
      }),
    });

    await ensureSuccess(response, "Recommendation preview unavailable");
    return response.json() as Promise<RoutingPreviewResponse>;
  },

  // Create ticket
  async create(
    ticket: CreateTicketInput,
    token: string,
  ): Promise<Ticket> {
    const createPayload = {
      ...ticket,
    } as CreateTicketInput & {
      status?: string;
    };
    delete createPayload.status;

    const response = await fetch(`${API_BASE_URL}/tickets`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(createPayload),
    });

    await ensureSuccess(response, API_USER_MESSAGES.saveChanges);
    return response.json();
  },

  // Update ticket
  async update(
    id: string,
    ticket: TicketMutationInput,
    token: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(ticket),
    });

    await ensureSuccess(response, API_USER_MESSAGES.saveChanges);
    return response.json();
  },

  async archive(id: string, token: string): Promise<ArchivedTicket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/archive`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({}),
    });

    await ensureSuccess(response, "Unable to archive ticket");
    return response.json();
  },

  async archiveWithReason(
    id: string,
    changeReason: string | undefined,
    token: string,
  ): Promise<ArchivedTicket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/archive`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({
        changeReason: changeReason?.trim() || undefined,
      }),
    });

    await ensureSuccess(response, "Unable to archive ticket");
    return response.json();
  },

  async reactivateArchived(id: string, token: string): Promise<Ticket> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/archived/${id}/reactivate`,
      {
        method: "POST",
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to reactivate archived ticket");
    return response.json();
  },

  // Delete ticket
  async delete(id: string, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}`, {
      method: "DELETE",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to delete ticket");
  },

  async getPendingApproval(token: string): Promise<PagedTicketList> {
    const response = await fetch(`${API_BASE_URL}/tickets/pending-approval`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, API_USER_MESSAGES.loadTickets);
    return response.json() as Promise<PagedTicketList>;
  },

  /** Unified constrained AI intake + vision fusion (advisory; does not persist). */
  async assessUnifiedIntake(
    ticketId: string,
    token: string,
    signal?: AbortSignal,
  ): Promise<CortexAiAssessment> {
    const response = await fetch(`${API_BASE_URL}/ai/assess`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({ ticketId }),
      signal,
    });

    await ensureSuccess(response, "Unable to load Cortex AI assessment");
    return response.json() as Promise<CortexAiAssessment>;
  },

  /** Phase 1 advisory triage for PendingApproval intake review (200 OK may include `unavailable: true`). */
  async generateTriage(
    id: string,
    token: string,
    signal?: AbortSignal,
  ): Promise<TicketTriageGenerateApiResponse> {
    const response = await fetch(`${API_BASE_URL}/tickets/${encodeURIComponent(id)}/triage`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({}),
      signal,
    });

    await ensureSuccess(response, "Unable to load AI triage");
    return response.json() as Promise<TicketTriageGenerateApiResponse>;
  },

  /**
   * Explicit reviewer action: apply persisted AI triage suggestions to the ticket's
   * canonical Priority / Status fields. Does NOT call the AI. A 409 indicates the
   * suggestion is stale against the current vocabulary and surfaces as ApiError(409).
   */
  async applyTriageSuggestions(
    id: string,
    request: TicketTriageApplyRequest,
    token: string,
    signal?: AbortSignal,
  ): Promise<Ticket> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${encodeURIComponent(id)}/triage/apply`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: JSON.stringify(request),
        signal,
      },
    );

    await ensureSuccess(response, "Unable to apply AI triage suggestions");
    return response.json() as Promise<Ticket>;
  },

  /** Best-effort workflow metric; failures are ignored client-side. */
  async recordReviewerQualitySignal(
    ticketId: string,
    payload: ReviewerQualitySignalMetricsPayload,
    token: string,
  ): Promise<void> {
    try {
      const response = await fetch(
        `${API_BASE_URL}/tickets/${encodeURIComponent(ticketId)}/metrics/reviewer-quality-signal`,
        {
          method: "POST",
          headers: authHeaders(token, true),
          body: JSON.stringify(payload),
        },
      );
      if (response.ok) {
        return;
      }
    } catch {
      /* ignore */
    }
  },

  /**
   * Stateless, user-facing Improve Request assist. Server never mutates a ticket;
   * a 200 with `unavailable: true` means AI is misconfigured or failed and the
   * UI should leave the draft untouched.
   */
  async intakeAssist(
    request: IntakeAssistRequest,
    token: string,
    signal?: AbortSignal,
  ): Promise<IntakeAssistResult> {
    const response = await fetch(`${API_BASE_URL}/tickets/intake-assist`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(request),
      signal,
    });

    await ensureSuccess(response, "Unable to improve request");
    const raw = (await response.json()) as Record<string, unknown>;

    const suggestedSummary =
      typeof raw.suggestedSummary === "string" ? raw.suggestedSummary : null;
    const improvedDescription =
      typeof raw.improvedDescription === "string" ? raw.improvedDescription : null;
    const guidanceMessage =
      typeof raw.guidanceMessage === "string" ? raw.guidanceMessage : null;
    const unavailable = raw.unavailable === true;
    const unavailableReason =
      typeof raw.unavailableReason === "string" ? raw.unavailableReason : null;

    const missingDetails = Array.isArray(raw.missingDetails)
      ? raw.missingDetails.filter((entry): entry is string => typeof entry === "string")
      : [];

    // Default to requires_clarification so the UI never renders an unknown pill.
    const clarityState = isClarityState(raw.clarityState)
      ? raw.clarityState
      : "requires_clarification";

    return {
      suggestedSummary,
      improvedDescription,
      missingDetails,
      clarityState,
      guidanceMessage,
      unavailable,
      unavailableReason,
    };
  },

  async approveTicket(id: string, token: string): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/approve`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({}),
    });

    await ensureSuccess(response, "Unable to approve ticket");
    return response.json() as Promise<Ticket>;
  },

  async returnTicketForDetail(
    id: string,
    token: string,
    reason: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/return`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({ reason }),
    });

    await ensureSuccess(response, "Unable to return ticket");
    return response.json() as Promise<Ticket>;
  },

  async rejectTicket(
    id: string,
    token: string,
    reason: string,
  ): Promise<Ticket> {
    const response = await fetch(`${API_BASE_URL}/tickets/${id}/reject`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify({ reason }),
    });

    await ensureSuccess(response, "Unable to reject ticket");
    return response.json() as Promise<Ticket>;
  },

  async applyReassignment(
    id: string,
    body: ReassignmentApplyRequest,
    token: string,
  ): Promise<ReassignmentApplyResponse> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${encodeURIComponent(id)}/reassignment/apply`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: JSON.stringify({
          ticketId: body.ticketId,
          selectedOwnerId: body.selectedOwnerId,
          reason: body.reason,
          source: body.source,
          concurrencyToken: body.concurrencyToken,
          expectedCurrentOwnerKey: body.expectedCurrentOwnerKey,
        }),
      },
    );

    await ensureSuccess(response, "Unable to apply reassignment");
    return response.json() as Promise<ReassignmentApplyResponse>;
  },
};

export const workloadService = {
  async getSnapshots(token: string): Promise<WorkloadSnapshot[]> {
    const response = await fetch(`${API_BASE_URL}/workload/snapshot`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load workload snapshot");
    return response.json() as Promise<WorkloadSnapshot[]>;
  },
};

export const decisionService = {
  async getRebalanceSuggestions(token: string): Promise<RebalanceSuggestion[]> {
    const response = await fetch(`${API_BASE_URL}/rebalance/suggestions`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load rebalance suggestions");
    return response.json() as Promise<RebalanceSuggestion[]>;
  },
};

export const metricsService = {
  async getWorkflowMetricsSnapshot(
    token: string,
  ): Promise<WorkflowMetricsSnapshot> {
    const response = await fetch(`${API_BASE_URL}/metrics/snapshot`, {
      headers: authHeaders(token),
    });
    await ensureSuccess(response, "Unable to load workflow metrics");
    return response.json() as Promise<WorkflowMetricsSnapshot>;
  },
};

export const repeatIssuesService = {
  async getOverview(
    token: string,
    topN = 8,
  ): Promise<RepeatIssueOverviewResponse> {
    const response = await fetch(
      `${API_BASE_URL}/metrics/repeat-issues/?topN=${topN}`,
      { headers: authHeaders(token) },
    );
    await ensureSuccess(response, "Unable to load recurring issue overview");
    return response.json() as Promise<RepeatIssueOverviewResponse>;
  },

  async getGroupDetail(
    groupKey: string,
    token: string,
  ): Promise<RepeatIssueGroupDetailResponse> {
    const response = await fetch(
      `${API_BASE_URL}/metrics/repeat-issues/${encodeURIComponent(groupKey)}`,
      { headers: authHeaders(token) },
    );
    await ensureSuccess(response, "Unable to load recurring issue detail");
    return response.json() as Promise<RepeatIssueGroupDetailResponse>;
  },

  async generateAiReview(
    groupKey: string,
    token: string,
  ): Promise<RepeatIssueAiReviewResponse> {
    const response = await fetch(
      `${API_BASE_URL}/metrics/repeat-issues/${encodeURIComponent(groupKey)}/ai-review`,
      {
        method: "POST",
        headers: authHeaders(token),
      },
    );
    await ensureSuccess(response, "Unable to generate AI review");
    return response.json() as Promise<RepeatIssueAiReviewResponse>;
  },
};

export const attachmentService = {
  async getByTicket(ticketId: string, token: string): Promise<TicketAttachment[]> {
    const response = await fetch(`${API_BASE_URL}/tickets/${ticketId}/attachments`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load attachments");
    return response.json();
  },

  async upload(
    ticketId: string,
    files: File[],
    token: string,
  ): Promise<TicketAttachment[]> {
    const formData = new FormData();
    files.forEach((file) => formData.append("files", file));

    const response = await fetch(`${API_BASE_URL}/tickets/${ticketId}/attachments`, {
      method: "POST",
      headers: authHeaders(token),
      body: formData,
    });

    await ensureSuccess(response, "Unable to upload attachments");
    return response.json();
  },

  async download(ticketId: string, attachmentId: number, token: string): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${ticketId}/attachments/${attachmentId}/download`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to download attachment");
    return response.blob();
  },

  async getByArchivedTicket(ticketId: string, token: string): Promise<TicketAttachment[]> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/archived/${ticketId}/attachments`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to load archived attachments");
    return response.json();
  },

  async downloadArchived(
    ticketId: string,
    attachmentId: number,
    token: string,
  ): Promise<Blob> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/archived/${ticketId}/attachments/${attachmentId}/download`,
      {
        headers: authHeaders(token),
      },
    );

    await ensureSuccess(response, "Unable to download archived attachment");
    return response.blob();
  },

  async analyzeScreenshotInsight(
    ticketId: string,
    token: string,
    signal?: AbortSignal,
  ): Promise<ScreenshotInsightResult> {
    const response = await fetch(
      `${API_BASE_URL}/tickets/${ticketId}/attachments/screenshot-insight`,
      {
        method: "POST",
        headers: authHeaders(token, true),
        body: "{}",
        signal,
      },
    );

    await ensureSuccess(response, "Unable to analyze screenshots");
    return response.json() as Promise<ScreenshotInsightResult>;
  },
};

let userDirectoryCache: UserDirectoryEntry[] | null = null;
let userDirectoryRequest: Promise<UserDirectoryEntry[]> | null = null;

/** Fired on `window` after directory cache is cleared so owner pickers refetch. */
export const USER_DIRECTORY_INVALIDATED_EVENT = "cortex-user-directory-invalidated";

function notifyUserDirectoryInvalidated() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(USER_DIRECTORY_INVALIDATED_EVENT));
  }
}

export const userService = {
  async getCurrentUser(token: string): Promise<UserProfile> {
    const response = await fetch(`${API_BASE_URL}/users/me`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load current user");
    return response.json();
  },

  async getAll(token: string): Promise<UserRecord[]> {
    const response = await fetch(`${API_BASE_URL}/users`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load users");
    return response.json();
  },

  async syncFromAuth0(token: string): Promise<SyncUsersFromAuth0Result> {
    const response = await fetch(`${API_BASE_URL}/users/sync-from-auth0`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: "{}",
    });

    await ensureSuccess(response, "Unable to import users from Auth0");
    const result = (await response.json()) as SyncUsersFromAuth0Result;
    userService.clearDirectoryCache();
    return result;
  },

  async getDirectory(token: string): Promise<UserDirectoryEntry[]> {
    if (userDirectoryCache) {
      return userDirectoryCache;
    }

    if (userDirectoryRequest) {
      return userDirectoryRequest;
    }

    userDirectoryRequest = (async () => {
      const response = await fetch(`${API_BASE_URL}/users/directory`, {
        headers: authHeaders(token),
      });

      await ensureSuccess(response, "Unable to load user directory");
      const directoryEntries = (await response.json()) as UserDirectoryEntry[];
      userDirectoryCache = directoryEntries;
      return directoryEntries;
    })();

    try {
      return await userDirectoryRequest;
    } finally {
      userDirectoryRequest = null;
    }
  },

  clearDirectoryCache() {
    userDirectoryCache = null;
    userDirectoryRequest = null;
    notifyUserDirectoryInvalidated();
  },

  async getOnlineUsers(token: string): Promise<OnlineUser[]> {
    const response = await fetch(`${API_BASE_URL}/users/online`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load online users");
    return response.json();
  },

  async updateProfile(
    profile: UpdateUserProfileInput,
    token: string,
  ): Promise<UserProfile> {
    const response = await fetch(`${API_BASE_URL}/users/profile`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(profile),
    });

    await ensureSuccess(response, "Unable to update profile");
    return response.json();
  },

  async updatePresence(token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/users/me/presence`, {
      method: "POST",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to update presence");
  },

  async updateUser(
    id: number,
    user: AdminUpdateUserInput,
    token: string,
  ): Promise<UserRecord> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: "PUT",
      headers: authHeaders(token, true),
      body: JSON.stringify(user),
    });

    await ensureSuccess(response, "Unable to update user");
    return response.json();
  },

  async getAvailableAuth0Roles(token: string): Promise<Auth0RoleOption[]> {
    const response = await fetch(`${API_BASE_URL}/users/available-roles`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load Auth0 roles");
    return response.json();
  },

  async getUserAuth0Roles(
    id: number,
    token: string,
  ): Promise<UserAuth0RolesResponse> {
    const response = await fetch(`${API_BASE_URL}/users/${id}/roles`, {
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to load user roles");
    return response.json();
  },

  async mutateUserAuth0Role(
    id: number,
    body: UserRoleMutationRequest,
    token: string,
  ): Promise<UserRecord> {
    const response = await fetch(`${API_BASE_URL}/users/${id}/roles/mutation`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(body),
    });

    await ensureSuccess(response, "Unable to update user role");
    return response.json();
  },

  async createUser(user: CreateUserInput, token: string): Promise<UserRecord> {
    const response = await fetch(`${API_BASE_URL}/users`, {
      method: "POST",
      headers: authHeaders(token, true),
      body: JSON.stringify(user),
    });

    await ensureSuccess(response, "Unable to create user");
    return response.json();
  },

  async deleteUser(id: number, token: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/users/${id}`, {
      method: "DELETE",
      headers: authHeaders(token),
    });

    await ensureSuccess(response, "Unable to delete user");
  },
};
