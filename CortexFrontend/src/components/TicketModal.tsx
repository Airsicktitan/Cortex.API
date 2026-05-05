import {
  useCallback,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
} from "react";
import type {
  ApprovalStatus,
  ApprovalTriagePreview,
  Ticket,
  TicketMutationInput,
  TicketSaveResult,
  TicketTriageGenerateApiResponse,
} from "../types/ticket";
import type { TicketAttachment } from "../types/attachment";
import {
  persistedScreenshotInsightToResult,
  screenshotInsightPersistedHasContent,
  type ScreenshotInsightResult,
} from "../types/screenshotInsight";
import type { RealtimeEvent } from "../types/realtime";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { TicketStatusDefinition } from "../types/ticketStatus";
import type { UserDirectoryEntry } from "../types/user";
import {
  CLARITY_STATE_PILL_CLASS,
  type IntakeAssistResult,
} from "../types/intakeAssist";
import { commentService } from "../services/commentService";
import {
  attachmentService,
  getUserFacingErrorMessage,
  TICKET_ATTACHMENTS_CHANGED_EVENT,
  ticketService,
  USER_DIRECTORY_INVALIDATED_EVENT,
  userService,
} from "../services/api";
import type { Comment } from "../types/comment";
import CommentList from "./CommentList";
import AddComment from "./AddComment";
import TicketHistoryModal from "./TicketHistoryModal";
import UserCombobox from "./UserCombobox";
import { CortexTabbedPanel } from "./CortexTabbedPanel";
import { ExternalSourceContextCard } from "./ExternalSourceContextCard";
import { SapTicketReferenceContextCard } from "./SapTicketReferenceContextCard";
import { SynitiKnowledgeContextCard } from "./SynitiKnowledgeContextCard";
import { GovernanceContextSummaryCard } from "./GovernanceContextSummaryCard";
import { ApprovalOutcomeMessage } from "./approval/ApprovalOutcomeMessage";
import { ApprovalTriageModalColumn } from "./approval/ApprovalTriageSlot";
import { CortexTooltip } from "./ui/Tooltip";
import { ScrollableViewport } from "./ui/ScrollableViewport";
import { ScreenshotInsightEvidenceCard } from "./ticket-modal/ScreenshotInsightEvidenceCard";
import { IntakeAssistResultPanel } from "./ticket-modal/IntakeAssistResultPanel";
import { getIntakeAssistResultFingerprint } from "../utils/intakeAssistFingerprint";
import {
  deriveReviewerIntakeQualitySignal,
  getReviewerIntakeQualityCopy,
  triageHasContent,
  type ReviewerIntakeQualityKind,
} from "../utils/approvalTriage";
import { useAuth0 } from "@auth0/auth0-react";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaBadgeClass,
  getSlaDisplayLabel,
  getUrgencyChip,
} from "../utils/ticketSla";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import toast from "react-hot-toast";
import {
  normalizeRoles,
  canCreateTickets,
  canEditTickets,
  canReviewApprovalQueue,
} from "../utils/role";
import { readOnlyOwnerDetailDisplay, USER_ID_TOKEN_PREFIX } from "../utils/ownerIdentity";
import {
  getActivitySignal,
  getWaitingOnLabel,
} from "../utils/ticketActivity";
import type { CortexSlaRisk } from "../types/cortexRisk";
import type { TicketExternalSourceContextItem } from "../types/integrations";
import type { SapTicketReferenceContext, SapTicketReferenceMatch } from "../types/sapTicketReference";
import type { SynitiKnowledgeContext } from "../types/synitiKnowledgeContext";

const API_AUDIENCE = "https://cortex-api";
const MAX_TITLE_LENGTH = 200;
const MAX_DESCRIPTION_LENGTH = 4000;
const TYPING_PING_THROTTLE_MS = 2000;
const TYPING_INDICATOR_TTL_MS = 5000;
/** Pixels from bottom to treat as "at bottom" for auto-scroll and new-comment handling */
const COMMENT_THREAD_NEAR_BOTTOM_PX = 80;

function formatFileSize(fileSize: number) {
  if (fileSize < 1024) {
    return `${fileSize} B`;
  }

  if (fileSize < 1024 * 1024) {
    return `${(fileSize / 1024).toFixed(1)} KB`;
  }

  return `${(fileSize / (1024 * 1024)).toFixed(1)} MB`;
}

function getQueuedAttachmentKey(file: File) {
  return `${file.name}:${file.size}:${file.lastModified}`;
}

/** v1 screenshot insight: PNG, JPEG, WebP only (aligned with backend). */
function isImageAttachmentForInsight(a: TicketAttachment): boolean {
  const name = a.fileName.toLowerCase();
  if (
    name.endsWith(".png") ||
    name.endsWith(".jpg") ||
    name.endsWith(".jpeg") ||
    name.endsWith(".webp")
  ) {
    return true;
  }
  const ct = (a.contentType || "").toLowerCase().split(";")[0].trim();
  return (
    ct === "image/png" ||
    ct === "image/jpeg" ||
    ct === "image/jpg" ||
    ct === "image/webp"
  );
}

function getTicketApprovalStatus(ticket: Ticket): ApprovalStatus {
  return ticket.approvalStatus ?? "Approved";
}

function normalizeAdvisorySlaRiskTier(
  raw: string | null | undefined,
): ApprovalTriagePreview["potentialSlaRisk"] {
  const t = raw?.trim();
  if (!t) {
    return undefined;
  }
  const lower = t.toLowerCase();
  if (lower === "low" || lower === "medium" || lower === "high") {
    return (lower.charAt(0).toUpperCase() + lower.slice(1)) as
      | "Low"
      | "Medium"
      | "High";
  }
  return undefined;
}

function mapTicketTriageApiToPreview(
  response: TicketTriageGenerateApiResponse,
): ApprovalTriagePreview {
  return {
    summary: response.summary ?? undefined,
    suggestedPriority: response.suggestedPriority ?? undefined,
    priorityReason: response.priorityReason ?? undefined,
    suggestedStatus: response.suggestedStatus ?? undefined,
    missingDetailHints:
      response.missingDetails?.filter(
        (s) => typeof s === "string" && s.trim().length > 0,
      ) ?? [],
    potentialSlaRisk: normalizeAdvisorySlaRiskTier(response.potentialSlaRisk),
    slaRiskReason: response.slaRiskReason?.trim() || undefined,
  };
}

/** Match server/API priority casing (Critical | High | Medium | Low). */
function canonicalizeTicketPriority(raw: string): string {
  const t = raw.trim();
  for (const p of ["Critical", "High", "Medium", "Low"] as const) {
    if (p.toLowerCase() === t.toLowerCase()) {
      return p;
    }
  }
  return t;
}

function requesterSummaryCopy(ticket: Ticket): string {
  switch (getTicketApprovalStatus(ticket)) {
    case "PendingApproval":
      return "This request is waiting for reviewer approval before active work begins.";
    case "NeedsMoreInfo":
      return "A reviewer needs more information before this request can move into active work.";
    case "Rejected":
      return "This request was closed during intake and did not move into active work.";
    case "Approved":
    default:
      return "This request has been approved and is now moving through active work.";
  }
}

function reconcileCommentsById(
  current: Comment[],
  incoming: Comment[],
): Comment[] {
  if (incoming.length === 0) {
    return current.length === 0 ? current : [];
  }

  const currentById = new Map(current.map((comment) => [comment.id, comment]));
  let changed = incoming.length !== current.length;
  const nextComments = incoming.map((incomingComment) => {
    const existingComment = currentById.get(incomingComment.id);
    if (!existingComment) {
      changed = true;
      return incomingComment;
    }

    if (
      existingComment.body === incomingComment.body &&
      existingComment.createdBy === incomingComment.createdBy &&
      existingComment.createdByDisplayName ===
        incomingComment.createdByDisplayName &&
      existingComment.createdDate === incomingComment.createdDate &&
      existingComment.lastModifiedDate === incomingComment.lastModifiedDate
    ) {
      return existingComment;
    }

    changed = true;
    return incomingComment;
  });

  return changed ? nextComments : current;
}

function upsertCommentById(current: Comment[], incoming: Comment): Comment[] {
  const existingIndex = current.findIndex(
    (comment) => comment.id === incoming.id,
  );
  if (existingIndex < 0) {
    return [...current, incoming];
  }

  const existingComment = current[existingIndex];
  if (
    existingComment.body === incoming.body &&
    existingComment.createdBy === incoming.createdBy &&
    existingComment.createdByDisplayName === incoming.createdByDisplayName &&
    existingComment.createdDate === incoming.createdDate &&
    existingComment.lastModifiedDate === incoming.lastModifiedDate
  ) {
    return current;
  }

  const nextComments = [...current];
  nextComments[existingIndex] = incoming;
  return nextComments;
}

interface TicketModalProps {
  ticket: Ticket;
  latestRealtimeEvent?: RealtimeEvent | null;
  ticketBoards: TicketBoardDefinition[];
  ticketStatuses: TicketStatusDefinition[];
  isOpen: boolean;
  onClose: () => void;
  onSave: (
    updatedTicket: TicketMutationInput,
    attachments: File[],
  ) => Promise<TicketSaveResult | void>;
  onArchive: (ticket: Ticket, changeReason?: string) => Promise<void>;
  onDelete: (ticket: Ticket) => void;
  currentUser: {
    id?: number;
    displayName: string;
    department?: string;
    role?: string;
    roles?: string[];
  } | null;
  createdByDisplayName: string;
  /** Active-ticket modal hides redundant approved-state UI; requester/reviewer contexts keep it. */
  approvalDisplayContext?: "active" | "requester" | "reviewer";
  /** Refreshes ticket after modal-only actions (e.g. regenerate AI triage). */
  refreshPersistedTicket?: (
    ticketId: string,
    providedToken?: string,
  ) => Promise<Ticket | null>;
  /** Parent list/queue refresh after triage is persisted. */
  onTriagePersisted?: () => void;
  /**
   * After triage apply succeeds, merge the server ticket into parent state (e.g. selected ticket).
   * Needed while the modal is open because list upsert normally skips selected ticket sync.
   */
  onTriageApplySuccess?: (ticket: Ticket) => void;
  /**
   * When set, shows intake review actions for pending / needs-info tickets.
   * Each handler returns the updated ticket on success (or `null` when the
   * action was rejected / failed) so callers can sequence follow-up UI work.
   */
  intakeApprovalHandlers?: {
    approve: () => Promise<unknown>;
    returnForDetail: (reason: string) => Promise<unknown>;
    reject: (reason: string) => Promise<unknown>;
  };
  onOpenSourceTicket?: (ticketId: string) => void | Promise<void>;
}

type CreateFormField = "title" | "description" | "priority" | "storyPoints";
type CreateFormErrors = Partial<Record<CreateFormField, string>>;
type TypingPresence = {
  key: string;
  displayName?: string;
  expiresAt: number;
};

function isDirectoryDeveloperRole(role: string | undefined): boolean {
  return (role ?? "").trim().toLowerCase() === "developer";
}

function isDirectoryGuestRole(role: string | undefined): boolean {
  return (role ?? "").trim().toLowerCase() === "guest";
}

function isDirectorySynitiDepartment(department: string | undefined): boolean {
  return (department ?? "").trim().toLowerCase() === "syniti";
}

export default function TicketModal({
  ticket,
  latestRealtimeEvent,
  ticketBoards,
  ticketStatuses,
  isOpen,
  onClose,
  onSave,
  onArchive,
  onDelete,
  currentUser,
  createdByDisplayName,
  approvalDisplayContext = "requester",
  refreshPersistedTicket,
  onTriagePersisted,
  onTriageApplySuccess,
  intakeApprovalHandlers,
  onOpenSourceTicket,
}: TicketModalProps) {
  const defaultBoard =
    ticketBoards.find((board) => board.id === ticket.boardId) ??
    ticketBoards.find((board) => board.name === "Ticket") ??
    ticketBoards[0];
  const [priority, setPriority] = useState(ticket.priority);
  const [status, setStatus] = useState(ticket.id ? ticket.status : "New");
  const [department, setDepartment] = useState(
    ticket.department || currentUser?.department || "",
  );
  const [boardId, setBoardId] = useState(defaultBoard?.id ?? 0);
  const [storyPoints, setStoryPoints] = useState<number | "">(
    ticket.storyPoints ?? (defaultBoard?.requiresStoryPoints ? 1 : ""),
  );
  const [synitiOwner, setSynitiOwner] = useState(ticket.synitiOwner || "");
  const [businessOwner, setBusinessOwner] = useState(
    ticket.businessOwner || "",
  );
  const [ownerDirectory, setOwnerDirectory] = useState<UserDirectoryEntry[]>(
    [],
  );
  const [ownerDirectoryLoading, setOwnerDirectoryLoading] = useState(false);
  const [ownerDirectoryLoaded, setOwnerDirectoryLoaded] = useState(false);
  const [ownerDirectoryError, setOwnerDirectoryError] = useState<string | null>(
    null,
  );
  const [saving, setSaving] = useState(false);
  const [archiving, setArchiving] = useState(false);
  const [description, setDescription] = useState(ticket.description || "");
  const [title, setTitle] = useState(ticket.title || "");
  const [changeReason, setChangeReason] = useState("");
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [intakeReasonModal, setIntakeReasonModal] = useState<
    "return" | "reject" | null
  >(null);
  const [intakeReasonDraft, setIntakeReasonDraft] = useState("");
  const [intakeActionPending, setIntakeActionPending] = useState(false);
  const [triagePreviewOverride, setTriagePreviewOverride] =
    useState<ApprovalTriagePreview | null>(null);
  const [regenerateTriageLoading, setRegenerateTriageLoading] = useState(false);
  /** Which AI triage apply action is currently in-flight; null when idle. */
  const [triageApplyPending, setTriageApplyPending] = useState<
    "priority" | "status" | "both" | null
  >(null);
  /** User-facing error from the last apply attempt (e.g. 409 conflict). */
  const [triageApplyError, setTriageApplyError] = useState<string | null>(null);
  const [comments, setComments] = useState<Comment[]>([]);
  const [loadingComments, setLoadingComments] = useState(false);
  const [attachments, setAttachments] = useState<TicketAttachment[]>([]);
  const [queuedAttachments, setQueuedAttachments] = useState<File[]>([]);
  const [loadingAttachments, setLoadingAttachments] = useState(false);
  const [isAttachmentDropActive, setIsAttachmentDropActive] = useState(false);
  const [attachmentActionId, setAttachmentActionId] = useState<number | null>(
    null,
  );
  const [typingUsers, setTypingUsers] = useState<TypingPresence[]>([]);
  const [validationErrors, setValidationErrors] = useState<CreateFormErrors>(
    {},
  );
  const [isActionMenuOpen, setIsActionMenuOpen] = useState(false);
  const [isTicketDetailsOpen, setIsTicketDetailsOpen] = useState(false);
  const titleFieldId = useId();
  const titleInputRef = useRef<HTMLInputElement | null>(null);
  const actionMenuRef = useRef<HTMLDivElement | null>(null);
  // Intake-assist ("Improve for review") state. Stateless on the server;
  // result lives here until the requester applies it or dismisses it.
  const [intakeAssistResult, setIntakeAssistResult] =
    useState<IntakeAssistResult | null>(null);
  const [intakeAssistEditableDescription, setIntakeAssistEditableDescription] =
    useState("");
  const [intakeAssistLoading, setIntakeAssistLoading] = useState(false);
  const [intakeAssistError, setIntakeAssistError] = useState<string | null>(
    null,
  );
  const intakeAssistAbortRef = useRef<AbortController | null>(null);
  const [screenshotInsightResult, setScreenshotInsightResult] =
    useState<ScreenshotInsightResult | null>(null);
  const [screenshotInsightLoading, setScreenshotInsightLoading] =
    useState(false);
  const [screenshotInsightError, setScreenshotInsightError] = useState<
    string | null
  >(null);
  /** Muted “Analyzing…” line for soft auto-run only (not manual). */
  const [screenshotInsightAutoHint, setScreenshotInsightAutoHint] =
    useState(false);
  const screenshotInsightAbortRef = useRef<AbortController | null>(null);
  /** Prevents repeated auto screenshot analysis per modal open (token discipline). */
  const screenshotInsightAutoTriggeredForOpenRef = useRef(false);
  /** Improve Request was invoked at least once this modal session (for save metrics). */
  const intakeAssistUsedInSessionRef = useRef(false);
  const lastIntakeAssistSnapshotRef = useRef<{
    clarityState: string;
    missingDetailCount: number;
  } | null>(null);
  const lastReviewerQualityMetricsKeyRef = useRef<string | null>(null);

  const { getAccessTokenSilently } = useAuth0();

  // Used to prevent older comment fetches from overwriting newer ones
  const commentsLoadVersion = useRef(0);
  const attachmentsLoadVersion = useRef(0);
  const lastTypingPingAtRef = useRef(0);
  const pendingLocalCommentRef = useRef<{
    id: string;
    expiresAt: number;
  } | null>(null);
  const commentThreadScrollRef = useRef<HTMLDivElement | null>(null);
  /**
   * The main ticket-editing column owns vertical scrolling for the modal body
   * (Cortex Decision, Attachments, Cortex analysis, visual evidence, review actions,
   * etc.). A single jump-to-bottom control lives on this container so users
   * can quickly reach the save/review actions after long content — the old
   * per-section nested scroll in Cortex Decision has been removed.
   */
  const mainColumnScrollRef = useRef<HTMLDivElement | null>(null);
  const ticketDetailsScrollRef = useRef<HTMLDivElement | null>(null);
  const commentThreadNearBottomRef = useRef(true);
  const commentThreadOpenScrollPendingRef = useRef(false);
  const commentThreadSendScrollPendingRef = useRef(false);
  const prevCommentIdsSigRef = useRef<string>("");
  /** Latest ticket from props; used so we don't list `ticket` as a layout-effect dependency (avoids resetting the form on every parent/realtime object update). */
  const ticketPropRef = useRef(ticket);
  ticketPropRef.current = ticket;
  /** Reset form fields from ticket only when opening the modal or switching to a different ticket (id), not when the same ticket object is replaced while editing. */
  const lastHydratedFormKeyRef = useRef<string | null>(null);
  /** Tracks last known server `ticket.priority` so we can sync the dropdown when AI updates priority without overwriting a divergent approver edit. */
  const lastServerTicketPriorityRef = useRef(ticket.priority);
  const [pendingNewCommentsCount, setPendingNewCommentsCount] = useState(0);
  const [latestRisk, setLatestRisk] = useState<CortexSlaRisk | null>(null);
  const [externalSourceContexts, setExternalSourceContexts] = useState<
    TicketExternalSourceContextItem[]
  >([]);
  const [externalSourceContextLoading, setExternalSourceContextLoading] =
    useState(false);
  const [externalSourceContextError, setExternalSourceContextError] =
    useState(false);
  const [sapReferenceContext, setSapReferenceContext] = useState<
    SapTicketReferenceContext | null
  >(null);
  const [sapReferenceContextLoading, setSapReferenceContextLoading] =
    useState(false);
  const [sapReferenceContextError, setSapReferenceContextError] =
    useState(false);
  const [synitiKnowledgeContext, setSynitiKnowledgeContext] = useState<
    SynitiKnowledgeContext | null
  >(null);
  const [synitiKnowledgeContextLoading, setSynitiKnowledgeContextLoading] =
    useState(false);
  const [synitiKnowledgeContextError, setSynitiKnowledgeContextError] =
    useState(false);
  const authRoles = useMemo(
    () => normalizeRoles(currentUser?.roles, currentUser?.role),
    [currentUser?.roles, currentUser?.role],
  );
  const synitiOwnerOptions = useMemo(
    () =>
      ownerDirectory.filter(
        (u) =>
          u.isActive &&
          u.isSynitiOwnerEligible &&
          isDirectorySynitiDepartment(u.department) &&
          !isDirectoryGuestRole(u.role),
      ),
    [ownerDirectory],
  );
  const businessOwnerOptions = useMemo(
    () =>
      ownerDirectory.filter(
        (u) =>
          u.isActive &&
          u.isBusinessOwnerEligible &&
          !isDirectoryDeveloperRole(u.role) &&
          !isDirectoryGuestRole(u.role),
      ),
    [ownerDirectory],
  );
  const isCreateMode = !ticket.id;
  const canCreateTicket = isCreateMode && canCreateTickets(authRoles);
  const approvalStatusForEdit = getTicketApprovalStatus(ticket);
  const canReviewerEditPendingApproval =
    Boolean(ticket.id) &&
    approvalDisplayContext === "reviewer" &&
    approvalStatusForEdit === "PendingApproval" &&
    canReviewApprovalQueue(authRoles);
  const isCurrentUserCreator =
    currentUser?.id != null &&
    String(currentUser.id) === String(ticket.createdBy).trim();
  const canEditNeedsMoreInfoAsRequester =
    Boolean(ticket.id) &&
    approvalStatusForEdit === "NeedsMoreInfo" &&
    isCurrentUserCreator;
  const canUpdateTicket =
    Boolean(ticket.id) &&
    (canEditNeedsMoreInfoAsRequester ||
      canReviewerEditPendingApproval ||
      (canEditTickets(authRoles) && approvalStatusForEdit !== "NeedsMoreInfo"));
  const canSaveTicket = canCreateTicket || canUpdateTicket;
  const canDeleteTicket = Boolean(ticket.id) && canEditTickets(authRoles);
  const canArchiveTicket = Boolean(ticket.id) && canEditTickets(authRoles);
  /** Existing ticket: User/Guest cannot edit (API enforces Business Manager+). */
  const formReadOnly = Boolean(ticket.id) && !canUpdateTicket;
  const selectedBoard =
    ticketBoards.find((board) => board.id === boardId) ?? defaultBoard;
  const selectedBoardRequiresStoryPoints =
    selectedBoard?.requiresStoryPoints ?? false;

  const routingLivePreviewInput = useMemo(
    () =>
      ticket.id
        ? {
            boardId,
            priority,
            title,
            department,
          }
        : null,
    [ticket.id, boardId, priority, title, department],
  );

  const validateCreateForm = useCallback((): CreateFormErrors => {
    const errors: CreateFormErrors = {};
    const trimmedTitle = title.trim();
    const trimmedDescription = description.trim();
    const trimmedPriority = priority.trim();

    if (!trimmedTitle) {
      errors.title = "Title is required.";
    } else if (trimmedTitle.length > MAX_TITLE_LENGTH) {
      errors.title = `Title must be ${MAX_TITLE_LENGTH} characters or fewer.`;
    }

    if (!trimmedDescription) {
      errors.description = "Description is required.";
    } else if (trimmedDescription.length > MAX_DESCRIPTION_LENGTH) {
      errors.description = `Description must be ${MAX_DESCRIPTION_LENGTH} characters or fewer.`;
    }

    if (!trimmedPriority) {
      errors.priority = "Priority is required.";
    }

    if (selectedBoardRequiresStoryPoints && storyPoints === "") {
      errors.storyPoints = "Story points are required for this board.";
    }

    return errors;
  }, [
    description,
    priority,
    selectedBoardRequiresStoryPoints,
    storyPoints,
    title,
  ]);
  const hasCreateValidationErrors = Object.keys(validationErrors).length > 0;
  const quickMoveBoards = useMemo(
    () =>
      ticketBoards.filter(
        (board) =>
          board.isEnabled && Boolean(ticket.id) && board.id !== boardId,
      ),
    [boardId, ticket.id, ticketBoards],
  );
  const availableStatusOptions = useMemo(() => {
    const enabledStatuses = ticketStatuses.filter(
      (statusDefinition) => statusDefinition.isEnabled,
    );
    const hasCurrentStatus = enabledStatuses.some(
      (statusDefinition) => statusDefinition.name === status,
    );

    if (!hasCurrentStatus && status) {
      return [
        ...enabledStatuses,
        {
          id: 0,
          name: status,
          description: "Current ticket status",
          isEnabled: false,
          createdDateUtc: "",
          lastModifiedDateUtc: undefined,
        },
      ];
    }

    return enabledStatuses;
  }, [status, ticketStatuses]);
  const getApiToken = useCallback(async () => {
    return getAccessTokenSilently({
      authorizationParams: {
        audience: API_AUDIENCE,
      },
    });
  }, [getAccessTokenSilently]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      setExternalSourceContexts([]);
      setExternalSourceContextError(false);
      setExternalSourceContextLoading(false);
      return;
    }

    const controller = new AbortController();
    setExternalSourceContextLoading(true);
    setExternalSourceContextError(false);

    void (async () => {
      try {
        const token = await getApiToken();
        const list = await ticketService.getExternalSourceContexts(
          ticket.id,
          token,
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setExternalSourceContexts(list);
        }
      } catch {
        if (!controller.signal.aborted) {
          setExternalSourceContexts([]);
          setExternalSourceContextError(true);
        }
      } finally {
        if (!controller.signal.aborted) {
          setExternalSourceContextLoading(false);
        }
      }
    })();

    return () => controller.abort();
  }, [isOpen, ticket.id, getApiToken]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      setSapReferenceContext(null);
      setSapReferenceContextError(false);
      setSapReferenceContextLoading(false);
      return;
    }

    const controller = new AbortController();
    setSapReferenceContext(null);
    setSapReferenceContextLoading(true);
    setSapReferenceContextError(false);

    void (async () => {
      try {
        const token = await getApiToken();
        const data = await ticketService.getSapReferenceContext(
          ticket.id,
          token,
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setSapReferenceContext(data);
        }
      } catch {
        if (!controller.signal.aborted) {
          setSapReferenceContext(null);
          setSapReferenceContextError(true);
        }
      } finally {
        if (!controller.signal.aborted) {
          setSapReferenceContextLoading(false);
        }
      }
    })();

    return () => controller.abort();
  }, [isOpen, ticket.id, getApiToken]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      setSynitiKnowledgeContext(null);
      setSynitiKnowledgeContextError(false);
      setSynitiKnowledgeContextLoading(false);
      return;
    }

    const controller = new AbortController();
    setSynitiKnowledgeContext(null);
    setSynitiKnowledgeContextLoading(true);
    setSynitiKnowledgeContextError(false);

    void (async () => {
      try {
        const token = await getApiToken();
        const data = await ticketService.getSynitiKnowledgeContext(
          ticket.id,
          token,
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setSynitiKnowledgeContext(data);
        }
      } catch {
        if (!controller.signal.aborted) {
          setSynitiKnowledgeContext(null);
          setSynitiKnowledgeContextError(true);
        }
      } finally {
        if (!controller.signal.aborted) {
          setSynitiKnowledgeContextLoading(false);
        }
      }
    })();

    return () => controller.abort();
  }, [isOpen, ticket.id, getApiToken]);

  /** Merge a server ticket into modal form state after save (stay-open edit path). */
  const applyServerTicketToForm = useCallback(
    (saved: Ticket) => {
      const resolvedBoard =
        ticketBoards.find((board) => board.id === saved.boardId) ??
        ticketBoards.find((board) => board.name === "Ticket") ??
        ticketBoards[0];
      setTitle(saved.title || "");
      setDescription(saved.description || "");
      setPriority(saved.priority);
      lastServerTicketPriorityRef.current = saved.priority;
      setStatus(saved.status);
      setDepartment(saved.department || currentUser?.department || "");
      setBoardId(saved.boardId);
      setStoryPoints(
        saved.storyPoints ?? (resolvedBoard?.requiresStoryPoints ? 1 : ""),
      );
      setSynitiOwner(saved.synitiOwner || "");
      setBusinessOwner(saved.businessOwner || "");
      setChangeReason("");
      setQueuedAttachments([]);
      setTriagePreviewOverride(saved.approvalTriagePreview ?? null);
      setTriageApplyError(null);
    },
    [currentUser?.department, ticketBoards],
  );

  useEffect(() => {
    setTriagePreviewOverride(null);
  }, [ticket.id]);

  useEffect(() => {
    if (!isOpen) {
      setLatestRisk(null);
    }
  }, [isOpen, ticket.id]);

  const triageDisplayTicket = useMemo(
    (): Ticket => ({
      ...ticket,
      priority,
      status,
      approvalTriagePreview:
        triagePreviewOverride ?? ticket.approvalTriagePreview,
    }),
    [ticket, priority, status, triagePreviewOverride],
  );

  const reviewerIntakeQualityKind: ReviewerIntakeQualityKind | null =
    useMemo(() => {
      if (approvalDisplayContext !== "reviewer" || !ticket.id) {
        return null;
      }
      const preview =
        triagePreviewOverride ?? ticket.approvalTriagePreview ?? null;
      return deriveReviewerIntakeQualitySignal(preview);
    }, [
      approvalDisplayContext,
      ticket.id,
      triagePreviewOverride,
      ticket.approvalTriagePreview,
    ]);

  const reviewerIntakeQualityCopy = useMemo(
    () =>
      reviewerIntakeQualityKind === null
        ? null
        : getReviewerIntakeQualityCopy(reviewerIntakeQualityKind),
    [reviewerIntakeQualityKind],
  );

  const showAiTriageColumn = useMemo(
    () => approvalDisplayContext === "reviewer" && Boolean(ticket.id),
    [approvalDisplayContext, ticket.id],
  );

  const externalSourceContextSection = useMemo(
    () => (
      <ExternalSourceContextCard
        contexts={externalSourceContexts}
        loading={externalSourceContextLoading}
        loadError={externalSourceContextError}
      />
    ),
    [
      externalSourceContexts,
      externalSourceContextLoading,
      externalSourceContextError,
    ],
  );

  const sapTicketReferenceContextSection = useMemo(
    () => (
      <SapTicketReferenceContextCard
        context={sapReferenceContext}
        loading={sapReferenceContextLoading}
        loadError={sapReferenceContextError}
        ticketTitle={ticket.title}
        ticketDescription={ticket.description}
      />
    ),
    [
      sapReferenceContext,
      sapReferenceContextLoading,
      sapReferenceContextError,
    ],
  );

  const sapIntentOnlyFromApi = sapReferenceContext?.sapIntentOnly === true;

  /** Successful SAP context only — for Decision-tab assist; omit while loading/error. */
  const sapDecisionAssistMatches = useMemo(():
    | SapTicketReferenceMatch[]
    | undefined => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return undefined;
    }
    if (sapReferenceContextLoading || sapReferenceContextError) {
      return undefined;
    }
    if (sapIntentOnlyFromApi) {
      return undefined;
    }
    const m = sapReferenceContext?.matches;
    if (!m?.length) {
      return undefined;
    }
    return m;
  }, [
    approvalDisplayContext,
    ticket.id,
    sapReferenceContextLoading,
    sapReferenceContextError,
    sapIntentOnlyFromApi,
    sapReferenceContext?.matches,
  ]);

  const sapIntentOnlyForAssist = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return false;
    }
    if (sapReferenceContextLoading || sapReferenceContextError) {
      return false;
    }
    return sapIntentOnlyFromApi;
  }, [
    approvalDisplayContext,
    ticket.id,
    sapReferenceContextLoading,
    sapReferenceContextError,
    sapIntentOnlyFromApi,
  ]);

  const sapDecisionAssistTicketText = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return undefined;
    }
    const joined = [ticket.title, ticket.description]
      .filter((s) => Boolean(s?.trim()))
      .join("\n")
      .trim();
    return joined.length > 0 ? joined : undefined;
  }, [
    approvalDisplayContext,
    ticket.id,
    ticket.title,
    ticket.description,
  ]);

  const reviewerSourceContextTabSlot = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return null;
    }

    const showExtSection =
      externalSourceContextLoading ||
      externalSourceContexts.length > 0 ||
      externalSourceContextError;

    if (!showExtSection) {
      return null;
    }

    return (
      <section aria-label="External source context">
        <ExternalSourceContextCard
          contexts={externalSourceContexts}
          loading={externalSourceContextLoading}
          loadError={externalSourceContextError}
          variant="embedded"
        />
      </section>
    );
  }, [
    approvalDisplayContext,
    ticket.id,
    externalSourceContexts,
    externalSourceContextLoading,
    externalSourceContextError,
  ]);

  const reviewerSapEvidenceSlot = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return null;
    }

    return (
      <SapTicketReferenceContextCard
        purpose="evidence"
        variant="embedded"
        context={sapReferenceContext}
        loading={sapReferenceContextLoading}
        loadError={sapReferenceContextError}
        ticketTitle={ticket.title}
        ticketDescription={ticket.description}
      />
    );
  }, [
    approvalDisplayContext,
    ticket.id,
    sapReferenceContext,
    sapReferenceContextLoading,
    sapReferenceContextError,
    ticket.title,
    ticket.description,
  ]);

  const reviewerGovernanceSummarySlot = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return null;
    }

    const sapMatches = sapReferenceContextError
      ? []
      : (sapReferenceContext?.matches ?? []);

    const synitiMatches = synitiKnowledgeContextError
      ? []
      : (synitiKnowledgeContext?.matches ?? []);

    return (
      <GovernanceContextSummaryCard
        sapMatches={sapMatches}
        synitiMatches={synitiMatches}
      />
    );
  }, [
    approvalDisplayContext,
    ticket.id,
    sapReferenceContext?.matches,
    sapReferenceContextError,
    synitiKnowledgeContext?.matches,
    synitiKnowledgeContextError,
  ]);

  const reviewerSynitiEvidenceSlot = useMemo(() => {
    if (approvalDisplayContext !== "reviewer" || !ticket.id) {
      return null;
    }

    return (
      <SynitiKnowledgeContextCard
        context={synitiKnowledgeContext}
        loading={synitiKnowledgeContextLoading}
        loadError={synitiKnowledgeContextError}
      />
    );
  }, [
    approvalDisplayContext,
    ticket.id,
    synitiKnowledgeContext,
    synitiKnowledgeContextLoading,
    synitiKnowledgeContextError,
  ]);

  const sourceContextBundleSection = useMemo(
    () => (
      <>
        {externalSourceContextSection}
        {sapTicketReferenceContextSection}
      </>
    ),
    [externalSourceContextSection, sapTicketReferenceContextSection],
  );

  useEffect(() => {
    if (!isOpen || !ticket.id || !showAiTriageColumn) {
      return;
    }
    if (approvalDisplayContext !== "reviewer") {
      return;
    }
    if (reviewerIntakeQualityKind === null) {
      return;
    }

    const preview =
      triagePreviewOverride ?? ticket.approvalTriagePreview ?? null;
    const hintCount = preview?.missingDetailHints?.length ?? 0;
    const key = `${ticket.id}:${reviewerIntakeQualityKind}:${hintCount}`;

    if (lastReviewerQualityMetricsKeyRef.current === key) {
      return;
    }
    lastReviewerQualityMetricsKeyRef.current = key;

    void (async () => {
      try {
        const token = await getApiToken();
        await ticketService.recordReviewerQualitySignal(
          ticket.id,
          {
            reviewerSignal: reviewerIntakeQualityKind,
            missingDetailHintCount: hintCount > 0 ? hintCount : undefined,
          },
          token,
        );
      } catch {
        /* workflow metrics are best-effort */
      }
    })();
  }, [
    isOpen,
    ticket.id,
    showAiTriageColumn,
    approvalDisplayContext,
    reviewerIntakeQualityKind,
    triagePreviewOverride,
    ticket.approvalTriagePreview,
    getApiToken,
  ]);

  /**
   * My Tickets (requester): hide collaboration affordances while a request is still
   * awaiting intake approval — same boundary as reviewer modal (no comment thread).
   */
  const commentsColumnEnabled = useMemo(() => {
    if (!ticket.id) {
      return false;
    }
    if (approvalDisplayContext === "reviewer") {
      return false;
    }
    if (
      approvalDisplayContext === "requester" &&
      getTicketApprovalStatus(ticket) === "PendingApproval"
    ) {
      return false;
    }
    return true;
  }, [approvalDisplayContext, ticket]);

  const handleRegenerateTriageAnalysis = useCallback(async () => {
    if (!ticket.id) {
      return;
    }
    const serverPriorityBeforeCall = ticket.priority;
    const localPriorityStillMatchesServer =
      priority.trim() === (serverPriorityBeforeCall ?? "").trim();
    setRegenerateTriageLoading(true);
    try {
      const token = await getApiToken();
      const result = await ticketService.generateTriage(ticket.id, token);
      if (result.unavailable) {
        toast.error(
          result.unavailableReason?.trim() || "AI triage is not available.",
        );
        return;
      }
      setTriagePreviewOverride(mapTicketTriageApiToPreview(result));
      const refreshedTicket = refreshPersistedTicket
        ? await refreshPersistedTicket(ticket.id, token)
        : null;
      if (
        refreshedTicket &&
        triageHasContent(refreshedTicket.approvalTriagePreview)
      ) {
        setTriagePreviewOverride(null);
      }
      if (localPriorityStillMatchesServer) {
        if (refreshedTicket) {
          setPriority(refreshedTicket.priority);
        } else if (result.suggestedPriority) {
          setPriority(canonicalizeTicketPriority(result.suggestedPriority));
        }
      }
      onTriagePersisted?.();
      toast.success("Analysis updated.");
    } catch (error) {
      toast.error(
        getUserFacingErrorMessage(error, "Unable to regenerate analysis."),
      );
    } finally {
      setRegenerateTriageLoading(false);
    }
  }, [
    getApiToken,
    onTriagePersisted,
    priority,
    refreshPersistedTicket,
    ticket.id,
    ticket.priority,
  ]);

  /**
   * Reviewer apply: posts the selected AI triage suggestions to the backend,
   * updates local fields from the server response, and surfaces 409 inline
   * instead of a full board refresh.
   */
  const handleApplyTriageSuggestions = useCallback(
    async (action: "priority" | "status" | "both") => {
      if (!ticket.id || triageApplyPending) {
        return;
      }

      setTriageApplyPending(action);
      setTriageApplyError(null);

      try {
        const token = await getApiToken();
        const updated = await ticketService.applyTriageSuggestions(
          ticket.id,
          {
            applyPriority: action !== "status",
            applyStatus: action !== "priority",
          },
          token,
        );

        // Sync local form fields to server truth; parent reconciles its list
        // via the realtime event that the backend publishes after apply.
        if (action !== "status") {
          setPriority(updated.priority);
          lastServerTicketPriorityRef.current = updated.priority;
        }
        if (action !== "priority") {
          setStatus(updated.status);
        }

        // Use the server-returned preview immediately; clearing the override would
        // revert to a stale prop until the parent/list syncs.
        setTriagePreviewOverride(updated.approvalTriagePreview ?? null);

        onTriageApplySuccess?.(updated);
        onTriagePersisted?.();

        const successCopy =
          action === "priority"
            ? "Priority suggestion applied."
            : action === "status"
              ? "Status suggestion applied."
              : "AI suggestions applied.";
        toast.success(successCopy);
      } catch (error) {
        const message = getUserFacingErrorMessage(
          error,
          "Unable to apply AI triage suggestions.",
        );
        setTriageApplyError(message);
        toast.error(message);
      } finally {
        setTriageApplyPending(null);
      }
    },
    [
      getApiToken,
      onTriageApplySuccess,
      onTriagePersisted,
      ticket.id,
      triageApplyPending,
    ],
  );

  // ✅ CRITICAL: useLayoutEffect prevents the “1 frame of old ticket data”
  // Hydrate from props only when the modal opens or the ticket identity changes — not when `ticket`
  // is a new object reference for the same id (e.g. realtime refresh), or Syniti/Business owner edits reset.
  useLayoutEffect(() => {
    if (!isOpen) {
      lastHydratedFormKeyRef.current = null;
      return;
    }

    const t = ticketPropRef.current;
    const formKey = t.id ?? "__new__";
    if (lastHydratedFormKeyRef.current === formKey) {
      return;
    }
    lastHydratedFormKeyRef.current = formKey;

    const resolvedDefaultBoard =
      ticketBoards.find((board) => board.id === t.boardId) ??
      ticketBoards.find((board) => board.name === "Ticket") ??
      ticketBoards[0];

    setTitle(t.title || "");
    setDescription(t.description || "");
    setPriority(t.priority);
    lastServerTicketPriorityRef.current = t.priority;
    setStatus(t.id ? t.status : "New");
    setDepartment(t.department || currentUser?.department || "");
    setBoardId(resolvedDefaultBoard?.id ?? 0);
    setStoryPoints(
      t.storyPoints ?? (resolvedDefaultBoard?.requiresStoryPoints ? 1 : ""),
    );
    setSynitiOwner(t.synitiOwner || "");
    setBusinessOwner(t.businessOwner || "");
    setChangeReason("");
    setQueuedAttachments([]);
    setIsAttachmentDropActive(false);
    setIsHistoryModalOpen(false);
    setIsActionMenuOpen(false);
    setIsTicketDetailsOpen(false);
    setTypingUsers([]);
    setTriagePreviewOverride(null);
    setTriageApplyError(null);
    setTriageApplyPending(null);
    lastTypingPingAtRef.current = 0;
    pendingLocalCommentRef.current = null;
  }, [isOpen, ticket.id, ticketBoards, currentUser?.department]);

  useEffect(() => {
    if (!ticket.id) {
      return;
    }
    const prevServer = lastServerTicketPriorityRef.current;
    if ((ticket.priority ?? "") === (prevServer ?? "")) {
      return;
    }
    if (priority.trim() === (prevServer ?? "").trim()) {
      setPriority(ticket.priority);
    }
    lastServerTicketPriorityRef.current = ticket.priority;
  }, [ticket.id, ticket.priority, priority]);

  // Create ticket: when profile/department loads after open, fill department if still empty.
  useEffect(() => {
    if (!isOpen || ticket.id) {
      return;
    }
    setDepartment((prev) =>
      prev.trim() !== ""
        ? prev
        : ticket.department || currentUser?.department || "",
    );
  }, [isOpen, ticket.id, ticket.department, currentUser?.department]);

  useEffect(() => {
    if (!isOpen || ticket.id) {
      return;
    }

    const focusHandle = window.requestAnimationFrame(() => {
      const titleInput = titleInputRef.current;
      if (!titleInput) {
        return;
      }

      titleInput.focus();
    });

    return () => window.cancelAnimationFrame(focusHandle);
  }, [isOpen, ticket.id]);

  useEffect(() => {
    if (!isCreateMode) {
      setValidationErrors({});
      return;
    }

    setValidationErrors(validateCreateForm());
  }, [isCreateMode, validateCreateForm]);

  useEffect(() => {
    if (!isActionMenuOpen) {
      return;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (!actionMenuRef.current?.contains(event.target as Node)) {
        setIsActionMenuOpen(false);
      }
    };

    window.addEventListener("mousedown", handlePointerDown);
    return () => window.removeEventListener("mousedown", handlePointerDown);
  }, [isActionMenuOpen]);

  // Reset intake-assist state when the modal opens or the ticket identity changes,
  // and abort any in-flight assist request so stale results don't leak across tickets.
  useEffect(() => {
    intakeAssistAbortRef.current?.abort();
    intakeAssistAbortRef.current = null;
    setIntakeAssistResult(null);
    setIntakeAssistEditableDescription("");
    setIntakeAssistLoading(false);
    setIntakeAssistError(null);
    screenshotInsightAbortRef.current?.abort();
    screenshotInsightAbortRef.current = null;
    setScreenshotInsightResult(
      persistedScreenshotInsightToResult(
        ticketPropRef.current.screenshotInsight,
      ) ?? null,
    );
    setScreenshotInsightLoading(false);
    setScreenshotInsightError(null);
    intakeAssistUsedInSessionRef.current = false;
    lastIntakeAssistSnapshotRef.current = null;
    lastReviewerQualityMetricsKeyRef.current = null;
  }, [ticket.id, isOpen]);

  // When the same ticket is refreshed (e.g. after save or GET), hydrate screenshot insight from the server.
  useEffect(() => {
    if (!isOpen || !ticket.id) {
      return;
    }
    const persisted = persistedScreenshotInsightToResult(
      ticket.screenshotInsight,
    );
    if (persisted) {
      setScreenshotInsightResult(persisted);
    }
  }, [isOpen, ticket.id, ticket.screenshotInsight]);

  useEffect(() => {
    if (!isOpen) {
      screenshotInsightAutoTriggeredForOpenRef.current = false;
    }
  }, [isOpen]);

  useEffect(() => {
    screenshotInsightAutoTriggeredForOpenRef.current = false;
  }, [ticket.id]);

  // Abort any in-flight intake-assist request on unmount.
  useEffect(() => {
    return () => {
      intakeAssistAbortRef.current?.abort();
      intakeAssistAbortRef.current = null;
      screenshotInsightAbortRef.current?.abort();
      screenshotInsightAbortRef.current = null;
    };
  }, []);

  const handleImproveIntake = useCallback(async () => {
    const trimmedTitle = title.trim();
    const trimmedDescription = description.trim();
    if (!trimmedDescription) {
      return;
    }
    if (intakeAssistLoading) {
      return;
    }

    intakeAssistAbortRef.current?.abort();
    const controller = new AbortController();
    intakeAssistAbortRef.current = controller;

    setIntakeAssistLoading(true);
    setIntakeAssistError(null);
    try {
      const token = await getApiToken();
      const result = await ticketService.intakeAssist(
        {
          title: trimmedTitle,
          description: trimmedDescription,
          boardName: selectedBoard?.name ?? ticket.boardName ?? undefined,
          ticketId: ticket.id?.trim() || undefined,
          clientFlow: isCreateMode ? "create" : "edit",
        },
        token,
        controller.signal,
      );
      if (controller.signal.aborted) {
        return;
      }
      intakeAssistUsedInSessionRef.current = true;
      lastIntakeAssistSnapshotRef.current = {
        clarityState: result.clarityState,
        missingDetailCount: result.missingDetails.length,
      };
      setIntakeAssistResult(result);
      setIntakeAssistEditableDescription(result.improvedDescription ?? "");
    } catch (error) {
      if (controller.signal.aborted) {
        return;
      }
      const message = getUserFacingErrorMessage(
        error,
        "Unable to improve request.",
      );
      setIntakeAssistError(message);
    } finally {
      if (intakeAssistAbortRef.current === controller) {
        intakeAssistAbortRef.current = null;
      }
      if (!controller.signal.aborted) {
        setIntakeAssistLoading(false);
      }
    }
  }, [
    title,
    description,
    intakeAssistLoading,
    getApiToken,
    isCreateMode,
    selectedBoard?.name,
    ticket.boardName,
    ticket.id,
  ]);

  const handleUseIntakeSummary = useCallback(() => {
    const summary = intakeAssistResult?.suggestedSummary?.trim();
    if (!summary) {
      return;
    }
    setTitle(summary.slice(0, MAX_TITLE_LENGTH));
  }, [intakeAssistResult]);

  const handleUseIntakeDescription = useCallback(() => {
    const next = intakeAssistEditableDescription.trim();
    if (!next) {
      return;
    }
    setDescription(next.slice(0, MAX_DESCRIPTION_LENGTH));
  }, [intakeAssistEditableDescription]);

  const handleDismissIntakeAssist = useCallback(() => {
    intakeAssistAbortRef.current?.abort();
    intakeAssistAbortRef.current = null;
    setIntakeAssistResult(null);
    setIntakeAssistEditableDescription("");
    setIntakeAssistError(null);
    setIntakeAssistLoading(false);
  }, []);

  const imageAttachmentsForInsight = useMemo(
    () => attachments.filter(isImageAttachmentForInsight),
    [attachments],
  );

  const handleAnalyzeScreenshots = useCallback(
    async (options?: { silent?: boolean }) => {
      const silent = Boolean(options?.silent);
      if (screenshotInsightLoading || !ticket.id) {
        if (silent) {
          screenshotInsightAutoTriggeredForOpenRef.current = false;
        }
        return;
      }
      if (imageAttachmentsForInsight.length === 0) {
        if (silent) {
          screenshotInsightAutoTriggeredForOpenRef.current = false;
        }
        return;
      }
      if (silent) {
        screenshotInsightAutoTriggeredForOpenRef.current = true;
        setScreenshotInsightAutoHint(true);
      }
      screenshotInsightAbortRef.current?.abort();
      const controller = new AbortController();
      screenshotInsightAbortRef.current = controller;
      setScreenshotInsightLoading(true);
      setScreenshotInsightError(null);
      try {
        const token = await getApiToken();
        const result = await attachmentService.analyzeScreenshotInsight(
          ticket.id,
          token,
          controller.signal,
        );
        if (controller.signal.aborted) {
          return;
        }
        if (result.unavailable) {
          const fromPersisted = persistedScreenshotInsightToResult(
            ticketPropRef.current.screenshotInsight,
          );
          setScreenshotInsightResult(fromPersisted ?? result);
        } else {
          setScreenshotInsightResult(result);
          if (refreshPersistedTicket) {
            await refreshPersistedTicket(ticket.id, token);
          }
        }
      } catch (error) {
        if (controller.signal.aborted) {
          return;
        }
        if (!silent) {
          setScreenshotInsightError(
            getUserFacingErrorMessage(error, "Unable to analyze screenshots."),
          );
        }
      } finally {
        if (silent) {
          setScreenshotInsightAutoHint(false);
        }
        if (screenshotInsightAbortRef.current === controller) {
          screenshotInsightAbortRef.current = null;
        }
        if (!controller.signal.aborted) {
          setScreenshotInsightLoading(false);
        }
      }
    },
    [
      getApiToken,
      imageAttachmentsForInsight.length,
      refreshPersistedTicket,
      screenshotInsightLoading,
      ticket.id,
    ],
  );

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      return;
    }
    if (approvalDisplayContext !== "reviewer") {
      return;
    }
    if (screenshotInsightAutoTriggeredForOpenRef.current) {
      return;
    }
    if (screenshotInsightPersistedHasContent(ticket.screenshotInsight)) {
      return;
    }
    if (imageAttachmentsForInsight.length === 0) {
      return;
    }
    if (screenshotInsightLoading) {
      return;
    }

    void handleAnalyzeScreenshots({ silent: true });
  }, [
    approvalDisplayContext,
    handleAnalyzeScreenshots,
    imageAttachmentsForInsight.length,
    isOpen,
    screenshotInsightLoading,
    ticket.id,
    ticket.screenshotInsight,
  ]);

  const handleSave = useCallback(async () => {
    if (saving || archiving) {
      return;
    }

    if (isCreateMode) {
      const nextErrors = validateCreateForm();
      setValidationErrors(nextErrors);
      if (Object.keys(nextErrors).length > 0) {
        return;
      }
    }

    setSaving(true);
    try {
      const snapshot = lastIntakeAssistSnapshotRef.current;
      const intakeAssistSave =
        intakeAssistUsedInSessionRef.current && snapshot
          ? {
              intakeAssistUsedBeforeSave: true,
              lastIntakeClarityState: snapshot.clarityState,
              lastIntakeMissingDetailCount: snapshot.missingDetailCount,
            }
          : undefined;

      const result = await onSave(
        {
          title,
          description,
          priority,
          status: ticket.id ? status : undefined,
          department: !ticket.id ? department.trim() || undefined : undefined,
          boardId,
          storyPoints:
            selectedBoardRequiresStoryPoints && storyPoints !== ""
              ? Number(storyPoints)
              : undefined,
          synitiOwner: synitiOwner || undefined,
          businessOwner: businessOwner || undefined,
          changeReason: ticket.id
            ? changeReason.trim() || undefined
            : undefined,
          concurrencyToken: ticket.id ? ticket.concurrencyToken : undefined,
          ...(intakeAssistSave ? { intakeAssistSave } : {}),
        },
        queuedAttachments,
      );
      if (!result) {
        return;
      }
      if (result.outcome === "reloaded") {
        return;
      }
      if (result.outcome === "saved") {
        if (result.shouldCloseModal) {
          onClose();
          return;
        }
        applyServerTicketToForm(result.savedTicket);
      }
    } catch {
      // The parent save handler already surfaces the error to the user.
    } finally {
      setSaving(false);
    }
  }, [
    applyServerTicketToForm,
    archiving,
    isCreateMode,
    onSave,
    onClose,
    title,
    description,
    priority,
    status,
    ticket.id,
    department,
    boardId,
    selectedBoardRequiresStoryPoints,
    storyPoints,
    synitiOwner,
    businessOwner,
    changeReason,
    ticket.concurrencyToken,
    queuedAttachments,
    saving,
    validateCreateForm,
  ]);

  const handleAssignToMe = useCallback(async () => {
    if (!currentUser?.id || !canUpdateTicket || saving || archiving) return;
    const ownerToken = `${USER_ID_TOKEN_PREFIX}${currentUser.id}`;
    setSynitiOwner(ownerToken);
    try {
      await onSave(
        {
          title,
          description,
          priority,
          status,
          boardId,
          storyPoints:
            selectedBoardRequiresStoryPoints && storyPoints !== ""
              ? Number(storyPoints)
              : undefined,
          synitiOwner: ownerToken,
          businessOwner: businessOwner || undefined,
          concurrencyToken: ticket.concurrencyToken,
        },
        [],
      );
    } catch {
      // parent surfaces the error
    }
  }, [
    currentUser,
    canUpdateTicket,
    saving,
    archiving,
    onSave,
    title,
    description,
    priority,
    status,
    boardId,
    selectedBoardRequiresStoryPoints,
    storyPoints,
    businessOwner,
    ticket.concurrencyToken,
  ]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (isHistoryModalOpen) {
        return;
      }

      if (isTicketDetailsOpen) {
        if (e.key === "Escape") {
          e.preventDefault();
          setIsTicketDetailsOpen(false);
        }
        return;
      }

      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }

      if (e.key === "Enter" && !e.shiftKey) {
        const active = document.activeElement;
        const isCommentComposerFocused = Boolean(
          active instanceof Element &&
          active.closest('[data-comment-composer="true"]'),
        );

        if (isCommentComposerFocused) return;
        if (active?.tagName === "TEXTAREA") return;
        if (
          saving ||
          (isCreateMode ? hasCreateValidationErrors : !title.trim()) ||
          !canSaveTicket ||
          (selectedBoardRequiresStoryPoints && storyPoints === "")
        ) {
          return;
        }

        e.preventDefault();
        handleSave();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [
    canSaveTicket,
    hasCreateValidationErrors,
    handleSave,
    isCreateMode,
    isHistoryModalOpen,
    isTicketDetailsOpen,
    isOpen,
    onClose,
    selectedBoardRequiresStoryPoints,
    saving,
    storyPoints,
    title,
  ]);

  // ✅ CRITICAL: clear comments + show loading BEFORE paint, and guard response ordering
  useLayoutEffect(() => {
    if (!isOpen) return;

    // Always reset the right panel instantly on ticket change
    setComments([]);
    setLoadingComments(!!ticket.id);

    // bump version so older requests can't win
    commentsLoadVersion.current += 1;

    if (ticket.id) {
      commentThreadOpenScrollPendingRef.current = true;
      commentThreadSendScrollPendingRef.current = false;
      prevCommentIdsSigRef.current = "";
      commentThreadNearBottomRef.current = true;
      setPendingNewCommentsCount(0);
    }
  }, [isOpen, ticket.id]);

  useLayoutEffect(() => {
    if (!isOpen) return;

    setAttachments([]);
    setLoadingAttachments(!!ticket.id);
    attachmentsLoadVersion.current += 1;
  }, [isOpen, ticket.id]);

  const reloadComments = useCallback(async (options?: { silent?: boolean }) => {
    if (!ticket.id) return;

    const myVersion = ++commentsLoadVersion.current;
    if (!options?.silent) {
      setLoadingComments(true);
    }

    try {
      const token = await getApiToken();
      const data = await commentService.getByTicket(ticket.id, token);

      if (commentsLoadVersion.current !== myVersion) return;

      setComments((current) => reconcileCommentsById(current, data));
    } finally {
      if (!options?.silent && commentsLoadVersion.current === myVersion) {
        setLoadingComments(false);
      }
    }
  }, [getApiToken, ticket.id]);

  const reloadAttachments = useCallback(async () => {
    if (!ticket.id) return;

    const myVersion = ++attachmentsLoadVersion.current;
    setLoadingAttachments(true);

    try {
      const token = await getApiToken();
      const data = await attachmentService.getByTicket(ticket.id, token);

      if (attachmentsLoadVersion.current !== myVersion) return;

      setAttachments(data);
    } catch (error) {
      console.error("Failed to load attachments", error);

      if (attachmentsLoadVersion.current === myVersion) {
        toast.error(
          getUserFacingErrorMessage(error, "Unable to load attachments."),
        );
      }
    } finally {
      if (attachmentsLoadVersion.current === myVersion) {
        setLoadingAttachments(false);
      }
    }
  }, [getApiToken, ticket.id]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      return;
    }

    const handleTicketAttachmentsChanged = (event: Event) => {
      if (!(event instanceof CustomEvent)) {
        return;
      }

      const changedTicketId =
        typeof event.detail?.ticketId === "string" ? event.detail.ticketId : "";
      if (changedTicketId !== ticket.id) {
        return;
      }

      void reloadAttachments();
    };

    window.addEventListener(
      TICKET_ATTACHMENTS_CHANGED_EVENT,
      handleTicketAttachmentsChanged,
    );
    return () => {
      window.removeEventListener(
        TICKET_ATTACHMENTS_CHANGED_EVENT,
        handleTicketAttachmentsChanged,
      );
    };
  }, [isOpen, reloadAttachments, ticket.id]);

  const loadOwnerDirectory = useCallback(async () => {
    setOwnerDirectoryLoading(true);
    setOwnerDirectoryError(null);

    try {
      const token = await getApiToken();
      const directoryEntries = await userService.getDirectory(token);
      setOwnerDirectory(directoryEntries);
      setOwnerDirectoryLoaded(true);
    } catch (error) {
      console.error("Failed to load user directory", error);
      setOwnerDirectoryError(
        getUserFacingErrorMessage(error, "Unable to load users."),
      );
    } finally {
      setOwnerDirectoryLoading(false);
    }
  }, [getApiToken]);

  useEffect(() => {
    const onDirectoryInvalidated = () => {
      void loadOwnerDirectory();
    };
    window.addEventListener(
      USER_DIRECTORY_INVALIDATED_EVENT,
      onDirectoryInvalidated,
    );
    return () =>
      window.removeEventListener(
        USER_DIRECTORY_INVALIDATED_EVENT,
        onDirectoryInvalidated,
      );
  }, [loadOwnerDirectory]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      return;
    }
    if (!commentsColumnEnabled) {
      setComments([]);
      setLoadingComments(false);
      setTypingUsers([]);
      setPendingNewCommentsCount(0);
      return;
    }
    void reloadComments();
  }, [isOpen, reloadComments, ticket.id, commentsColumnEnabled]);

  useEffect(() => {
    if (
      !isOpen ||
      !canSaveTicket ||
      ownerDirectoryLoading ||
      ownerDirectoryLoaded ||
      ownerDirectoryError
    ) {
      return;
    }

    void loadOwnerDirectory();
  }, [
    canSaveTicket,
    isOpen,
    loadOwnerDirectory,
    ownerDirectoryError,
    ownerDirectoryLoaded,
    ownerDirectoryLoading,
  ]);

  useLayoutEffect(() => {
    if (!isOpen || !ticket.id || loadingComments) {
      return;
    }

    const el = commentThreadScrollRef.current;
    if (!el) {
      return;
    }

    const captureCommentIdsSig = (list: Comment[]) =>
      list
        .map((c) => c.id)
        .sort((a, b) => a - b)
        .join(",");

    const applyPrevSigFromComments = () => {
      prevCommentIdsSigRef.current = captureCommentIdsSig(comments);
    };

    if (commentThreadSendScrollPendingRef.current) {
      el.scrollTop = el.scrollHeight;
      commentThreadNearBottomRef.current = true;
      setPendingNewCommentsCount(0);
      commentThreadSendScrollPendingRef.current = false;
      applyPrevSigFromComments();
      return;
    }

    if (commentThreadOpenScrollPendingRef.current) {
      el.scrollTop = el.scrollHeight;
      commentThreadNearBottomRef.current = true;
      setPendingNewCommentsCount(0);
      commentThreadOpenScrollPendingRef.current = false;
      applyPrevSigFromComments();
      return;
    }

    const prevSig = prevCommentIdsSigRef.current;
    const nextSig = captureCommentIdsSig(comments);

    if (prevSig === nextSig) {
      return;
    }

    const prevIds = new Set(
      prevSig ? prevSig.split(",").map((id) => Number(id)) : [],
    );
    const added = comments.filter((c) => !prevIds.has(c.id));
    applyPrevSigFromComments();

    if (added.length === 0) {
      return;
    }

    if (commentThreadNearBottomRef.current) {
      el.scrollTop = el.scrollHeight;
      commentThreadNearBottomRef.current = true;
      setPendingNewCommentsCount(0);
    } else {
      setPendingNewCommentsCount((n) => n + added.length);
    }
  }, [comments, loadingComments, isOpen, ticket.id]);

  useEffect(() => {
    if (!isOpen || !ticket.id) return;
    void reloadAttachments();
  }, [isOpen, reloadAttachments, ticket.id]);

  useEffect(() => {
    if (
      !isOpen ||
      !ticket.id ||
      !latestRealtimeEvent ||
      latestRealtimeEvent.ticketId !== ticket.id
    ) {
      return;
    }

    if (latestRealtimeEvent.eventType === "comment.created") {
      if (!commentsColumnEnabled) {
        return;
      }
      const pending = pendingLocalCommentRef.current;

      if (pending) {
        const isExpired = Date.now() > pending.expiresAt;

        if (!isExpired) {
          const isLikelySelfEcho =
            latestRealtimeEvent.entityId === pending.id ||
            !latestRealtimeEvent.entityId;
          if (isLikelySelfEcho) {
            // Upsert if the event carries a full comment — idempotent for our own
            // echo (same ID overwrites with server data) and safe for the rare case
            // where another user's comment arrives without an entityId.
            if (latestRealtimeEvent.comment) {
              setComments((currentComments) =>
                upsertCommentById(currentComments, latestRealtimeEvent.comment!),
              );
            }
            return;
          }
        } else {
          pendingLocalCommentRef.current = null;
        }
      }

      if (latestRealtimeEvent.comment) {
        setComments((currentComments) =>
          upsertCommentById(currentComments, latestRealtimeEvent.comment!),
        );
        return;
      }

      void reloadComments({ silent: true });
    }

    if (latestRealtimeEvent.eventType === "comment.typing") {
      if (!commentsColumnEnabled) {
        return;
      }
      const actorDisplayName = latestRealtimeEvent.actorDisplayName?.trim();
      if (
        actorDisplayName &&
        currentUser?.displayName &&
        actorDisplayName.localeCompare(
          currentUser.displayName.trim(),
          undefined,
          {
            sensitivity: "accent",
          },
        ) === 0
      ) {
        return;
      }

      const actorUserId = latestRealtimeEvent.actorUserId;
      const actorKey =
        typeof actorUserId === "number" && Number.isFinite(actorUserId)
          ? `user:${actorUserId}`
          : actorDisplayName
            ? `name:${actorDisplayName.toLowerCase()}`
            : "unknown";
      const expiresAt = Date.now() + TYPING_INDICATOR_TTL_MS;

      setTypingUsers((current) => {
        const unexpired = current.filter(
          (entry) => entry.expiresAt > Date.now(),
        );
        const existingIndex = unexpired.findIndex(
          (entry) => entry.key === actorKey,
        );
        const nextEntry: TypingPresence = {
          key: actorKey,
          displayName: actorDisplayName,
          expiresAt,
        };

        if (existingIndex >= 0) {
          const next = [...unexpired];
          next[existingIndex] = nextEntry;
          return next;
        }

        return [...unexpired, nextEntry];
      });
    }

    if (latestRealtimeEvent.eventType === "attachment.created") {
      void reloadAttachments();
    }
  }, [
    commentsColumnEnabled,
    isOpen,
    latestRealtimeEvent,
    currentUser?.displayName,
    reloadAttachments,
    reloadComments,
    ticket.id,
  ]);

  useEffect(() => {
    if (!isOpen || !ticket.id) {
      setTypingUsers([]);
      return;
    }
    if (!commentsColumnEnabled) {
      setTypingUsers([]);
      return;
    }

    const intervalId = window.setInterval(() => {
      const now = Date.now();
      setTypingUsers((current) =>
        current.filter((entry) => entry.expiresAt > now),
      );
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [commentsColumnEnabled, isOpen, ticket.id]);

  const queueAttachments = useCallback((selectedFiles: File[]) => {
    if (selectedFiles.length === 0) {
      return;
    }

    setQueuedAttachments((currentFiles) => {
      const seenKeys = new Set(currentFiles.map(getQueuedAttachmentKey));
      const nextFiles = [...currentFiles];

      for (const file of selectedFiles) {
        const key = getQueuedAttachmentKey(file);
        if (seenKeys.has(key)) {
          continue;
        }

        seenKeys.add(key);
        nextFiles.push(file);
      }

      return nextFiles;
    });
  }, []);

  const signalTyping = useCallback(async () => {
    if (!ticket.id) {
      return;
    }

    const now = Date.now();
    if (now - lastTypingPingAtRef.current < TYPING_PING_THROTTLE_MS) {
      return;
    }
    lastTypingPingAtRef.current = now;

    try {
      const token = await getApiToken();
      await commentService.sendTyping(ticket.id, token);
    } catch (error) {
      console.error("Failed to send typing signal", error);
    }
  }, [getApiToken, ticket.id]);

  const scrollCommentThreadToBottom = useCallback(() => {
    const el = commentThreadScrollRef.current;
    if (!el) {
      return;
    }
    el.scrollTo({ top: el.scrollHeight, behavior: "smooth" });
  }, []);

  const handleCommentThreadScroll = useCallback(() => {
    const el = commentThreadScrollRef.current;
    if (!el) {
      return;
    }
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    const near = distanceFromBottom <= COMMENT_THREAD_NEAR_BOTTOM_PX;
    commentThreadNearBottomRef.current = near;
    if (near) {
      setPendingNewCommentsCount(0);
    }
  }, []);

  /**
   * Reviewer-only apply controls for the AI triage rail. Only rendered when
   * suggestions exist and the reviewer could actually change a canonical field.
   * Memoized here (before any early return) so hook order stays consistent.
   */
  const triageApplyControls = useMemo(() => {
    if (
      approvalDisplayContext !== "reviewer" ||
      !ticket.id ||
      getTicketApprovalStatus(ticket) !== "PendingApproval"
    ) {
      return undefined;
    }

    const preview =
      triagePreviewOverride ?? ticket.approvalTriagePreview ?? null;
    const suggestedPriority = preview?.suggestedPriority?.trim() ?? "";
    const suggestedStatus = preview?.suggestedStatus?.trim() ?? "";
    const currentPriority = (priority ?? "").trim();
    const currentStatus = (status ?? "").trim();

    const hasSuggestedPriority = suggestedPriority.length > 0;
    const hasSuggestedStatus = suggestedStatus.length > 0;
    const canApplyPriority =
      hasSuggestedPriority &&
      suggestedPriority.toLowerCase() !== currentPriority.toLowerCase();
    const canApplyStatus =
      hasSuggestedStatus &&
      suggestedStatus.toLowerCase() !== currentStatus.toLowerCase();
    const canApplyBoth = canApplyPriority && canApplyStatus;

    return {
      hasSuggestedPriority,
      hasSuggestedStatus,
      canApplyPriority,
      canApplyStatus,
      canApplyBoth,
      pendingAction: triageApplyPending,
      errorMessage: triageApplyError,
      onApplyPriority: () => handleApplyTriageSuggestions("priority"),
      onApplyStatus: () => handleApplyTriageSuggestions("status"),
      onApplyBoth: () => handleApplyTriageSuggestions("both"),
    };
  }, [
    approvalDisplayContext,
    handleApplyTriageSuggestions,
    priority,
    status,
    ticket,
    triageApplyError,
    triageApplyPending,
    triagePreviewOverride,
  ]);

  if (!isOpen) return null;

  const handleBoardSelectionChange = (nextBoardId: number) => {
    const nextBoard = ticketBoards.find((board) => board.id === nextBoardId);
    setBoardId(nextBoardId);

    if (nextBoard?.requiresStoryPoints) {
      setStoryPoints((currentValue) =>
        currentValue === "" ? 1 : currentValue,
      );
      return;
    }

    setStoryPoints("");
  };

  const handleDelete = () => {
    setIsActionMenuOpen(false);
    onClose();
    onDelete(ticket);
  };

  const handleArchive = async () => {
    setIsActionMenuOpen(false);
    setArchiving(true);
    try {
      await onArchive(ticket, changeReason.trim() || undefined);
      onClose();
    } catch {
      // The parent archive handler already surfaces the error to the user.
    } finally {
      setArchiving(false);
    }
  };

  const handleQuickMove = async (targetBoard: TicketBoardDefinition) => {
    if (!ticket.id || saving || archiving) {
      return;
    }

    const nextStoryPoints = targetBoard.requiresStoryPoints
      ? storyPoints === ""
        ? 1
        : Number(storyPoints)
      : undefined;

    setBoardId(targetBoard.id);
    setStoryPoints(nextStoryPoints ?? "");
    setIsActionMenuOpen(false);

    try {
      setSaving(true);
      const result = await onSave(
        {
          title,
          description,
          priority,
          status,
          boardId: targetBoard.id,
          storyPoints: nextStoryPoints,
          synitiOwner: synitiOwner || undefined,
          businessOwner: businessOwner || undefined,
          changeReason:
            changeReason.trim() || `Moved ticket to ${targetBoard.name}.`,
          concurrencyToken: ticket.concurrencyToken,
        },
        queuedAttachments,
      );
      if (!result || result.outcome === "reloaded") {
        return;
      }
      onClose();
      toast.success(`Moved ticket to ${targetBoard.name}`);
    } catch (error) {
      console.error("Failed to move ticket to board", error);
      toast.error(
        getUserFacingErrorMessage(
          error,
          `Unable to move ticket to ${targetBoard.name}.`,
        ),
      );
    } finally {
      setSaving(false);
    }
  };

  const addComment = async (body: string) => {
    if (!ticket.id) return;

    try {
      const token = await getApiToken();
      const createdComment = await commentService.create(
        ticket.id,
        body,
        token,
      );
      commentThreadSendScrollPendingRef.current = true;
      pendingLocalCommentRef.current = {
        id: String(createdComment.id),
        expiresAt: Date.now() + 2000, // 2 second protection window
      };
      setComments((current) => {
        if (current.some((comment) => comment.id === createdComment.id)) {
          return current;
        }
        return [...current, createdComment];
      });
    } catch (error) {
      console.error("Failed to add comment", error);
      toast.error(getUserFacingErrorMessage(error, "Unable to add comment"));
      throw error;
    }
  };

  const addQueuedAttachments = (event: ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = Array.from(event.target.files ?? []);
    queueAttachments(selectedFiles);

    event.target.value = "";
  };

  const handleAttachmentDragOver = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    event.stopPropagation();
    setIsAttachmentDropActive(true);
  };

  const handleAttachmentDragLeave = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    event.stopPropagation();
    setIsAttachmentDropActive(false);
  };

  const handleAttachmentDrop = (event: DragEvent<HTMLLabelElement>) => {
    event.preventDefault();
    event.stopPropagation();
    setIsAttachmentDropActive(false);

    const droppedFiles = Array.from(event.dataTransfer.files ?? []);
    if (droppedFiles.length === 0) {
      return;
    }

    queueAttachments(droppedFiles);
  };

  const removeQueuedAttachment = (fileToRemove: File) => {
    const targetKey = getQueuedAttachmentKey(fileToRemove);
    setQueuedAttachments((currentFiles) =>
      currentFiles.filter((file) => getQueuedAttachmentKey(file) !== targetKey),
    );
  };

  const openAttachment = async (attachment: TicketAttachment) => {
    if (!ticket.id) {
      return;
    }

    const previewWindow = window.open("", "_blank");
    if (!previewWindow) {
      toast.error("Your browser blocked the attachment preview window");
      return;
    }

    try {
      setAttachmentActionId(attachment.id);
      const token = await getApiToken();
      const blob = await attachmentService.download(
        ticket.id,
        attachment.id,
        token,
      );
      const objectUrl = URL.createObjectURL(blob);
      previewWindow.location.href = objectUrl;
      setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (error) {
      console.error("Failed to open attachment", error);
      previewWindow.close();
      toast.error(
        getUserFacingErrorMessage(error, "Unable to open attachment."),
      );
    } finally {
      setAttachmentActionId(null);
    }
  };

  const downloadAttachment = async (attachment: TicketAttachment) => {
    if (!ticket.id) {
      return;
    }

    try {
      setAttachmentActionId(attachment.id);
      const token = await getApiToken();
      const blob = await attachmentService.download(
        ticket.id,
        attachment.id,
        token,
      );
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = objectUrl;
      link.download = attachment.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 1_000);
    } catch (error) {
      console.error("Failed to download attachment", error);
      toast.error(
        getUserFacingErrorMessage(error, "Unable to download attachment."),
      );
    } finally {
      setAttachmentActionId(null);
    }
  };

  const createdByName =
    ticket.createdByDisplayName?.trim() ||
    createdByDisplayName.trim() ||
    ticket.createdByUser?.displayName?.trim() ||
    (!ticket.id ? currentUser?.displayName?.trim() : undefined) ||
    (typeof ticket.createdBy === "string"
      ? ticket.createdBy.trim()
      : typeof ticket.createdBy === "number" && ticket.createdBy > 0
        ? "Unknown user"
        : "") ||
    "Unknown User";
  const hasPersistedSla = Boolean(ticket.id);
  const requesterApprovalStatus = getTicketApprovalStatus(ticket);
  const isRequesterContext = approvalDisplayContext === "requester";
  const isApprovalQueueContext = approvalDisplayContext === "reviewer";
  const isRequesterIntakeTicket =
    isRequesterContext && requesterApprovalStatus !== "Approved";
  const showChangeReasonField = Boolean(ticket.id) && !isApprovalQueueContext;
  /** Reviewer + requester PendingApproval: no comment thread (intake vs collaboration). */
  const showCommentsColumn = commentsColumnEnabled;
  const canRenderTriageRegenerate =
    Boolean(ticket.id) && approvalDisplayContext === "reviewer";
  const canOfferTriageRegenerate =
    canRenderTriageRegenerate &&
    getTicketApprovalStatus(ticket) === "PendingApproval";
  const triageRegenerateDisabledReason =
    canOfferTriageRegenerate || !canRenderTriageRegenerate
      ? null
      : "Regenerate Analysis is available while the ticket is awaiting approval.";
  const ticketModalIsWorkspaceLayout =
    showAiTriageColumn || showCommentsColumn;
  const ticketModalGridClass = ticketModalIsWorkspaceLayout
    ? "grid-cols-1 max-lg:grid-rows-[minmax(0,1fr)_minmax(0,1fr)] lg:grid-cols-[minmax(0,1fr)_minmax(360px,440px)] lg:grid-rows-1 xl:grid-cols-[minmax(0,1fr)_minmax(420px,480px)]"
    : "grid-cols-1";
  const ticketModalWidthClass = ticketModalIsWorkspaceLayout
    ? "w-[min(96vw,1500px)] max-w-full"
    : "w-full max-w-5xl";
  const ticketModalHeightClass = ticketModalIsWorkspaceLayout
    ? "h-[min(92vh,calc(100dvh-1.5rem))] sm:h-[min(92vh,calc(100dvh-2rem))]"
    : "max-h-[min(92vh,calc(100dvh-1.5rem))] sm:max-h-[min(92vh,calc(100dvh-2rem))]";
  /** Viewport-safe: workspace uses fixed height for split panes; simple modal sizes to content up to max-h. */
  const ticketModalShellClass = `${ticketModalWidthClass} flex min-h-0 flex-col overflow-hidden overflow-x-hidden ${ticketModalHeightClass}`;
  const showRequesterRequestSummary = Boolean(ticket.id) && isRequesterContext;
  const activeTypingUsers = typingUsers.filter(
    (typingUser) => typingUser.expiresAt > Date.now(),
  );
  const typingIndicatorText =
    activeTypingUsers.length <= 0
      ? null
      : activeTypingUsers.length > 1
        ? "Multiple users are typing"
        : activeTypingUsers[0].displayName?.trim()
          ? `${activeTypingUsers[0].displayName} is typing...`
          : "Someone is typing...";
  const slaTooltip = buildSlaTooltip(ticket);
  const slaDisplayLabel = getSlaDisplayLabel(ticket);
  const slaBadgeClass = getSlaBadgeClass(slaDisplayLabel);
  const urgencyChip = hasPersistedSla && !isRequesterIntakeTicket ? getUrgencyChip(ticket) : null;
  const ticketActivity = hasPersistedSla && !isRequesterIntakeTicket ? getActivitySignal(ticket) : null;
  const waitingOnLabel = hasPersistedSla && !isRequesterIntakeTicket ? getWaitingOnLabel(ticket) : null;
  const urgencyGuidanceText =
    slaDisplayLabel === "Paused"
      ? waitingOnLabel
        ? `${waitingOnLabel} — SLA not started.`
        : "SLA not started — waiting for intake to complete."
      : slaDisplayLabel === "Overdue"
        ? `SLA overdue${waitingOnLabel ? ` — ${waitingOnLabel}` : " — assign or update before further delay"}.`
        : slaDisplayLabel === "At Risk"
          ? `SLA at risk${waitingOnLabel ? ` — ${waitingOnLabel}` : " — consider reassigning or escalating"}.`
          : waitingOnLabel && ticketActivity?.isStale
            ? `${waitingOnLabel} · no activity for ${ticketActivity.label}.`
            : null;
  const currentUserOwnerToken =
    currentUser?.id != null ? `${USER_ID_TOKEN_PREFIX}${currentUser.id}` : null;
  const canAssignToMe =
    Boolean(ticket.id) &&
    canUpdateTicket &&
    !isRequesterIntakeTicket &&
    Boolean(currentUserOwnerToken) &&
    synitiOwner !== currentUserOwnerToken;
  const showApprovedApprovalState = approvalDisplayContext !== "active";
  const approvalBadgePresentation = (() => {
    const s = getTicketApprovalStatus(ticket);
    const base = "rounded-full border px-3 py-1 text-xs font-semibold";
    if (s === "Approved") {
      if (!showApprovedApprovalState) {
        return null;
      }
      return {
        label: "Approved",
        className: `${base} border-emerald-300 bg-emerald-50 text-emerald-950 dark:border-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-100`,
      };
    }
    if (s === "PendingApproval") {
      return {
        label: "Pending Approval",
        className: `${base} border-amber-300 bg-amber-50 text-amber-950 dark:border-amber-700 dark:bg-amber-950/50 dark:text-amber-100`,
      };
    }
    if (s === "NeedsMoreInfo") {
      return {
        label: "Needs More Info",
        className: `${base} border-amber-300 bg-amber-50 text-amber-950 dark:border-amber-700 dark:bg-amber-950/50 dark:text-amber-100`,
      };
    }
    if (s === "Rejected") {
      return {
        label: "Rejected",
        className: `${base} border-red-300 bg-red-50 text-red-900 dark:border-red-800 dark:bg-red-950/50 dark:text-red-100`,
      };
    }
    return null;
  })();
  const priorityBadgeClass =
    priority === "Critical"
      ? "bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-200"
      : priority === "High"
        ? "bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-200"
        : priority === "Medium"
          ? "bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-200"
          : "bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-200";
  const ownerPickerDisabled =
    formReadOnly ||
    saving ||
    archiving ||
    (Boolean(ownerDirectoryError) && ownerDirectory.length === 0);
  const synitiOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : !ticket.id
      ? "Leave blank to use the matching Cortex recommendation."
      : undefined;
  const businessOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : !ticket.id
      ? "Leave blank to use the Cortex recommendation first, then default this ticket to you as the requester."
      : undefined;
  const requesterSummaryToneClass = (() => {
    switch (requesterApprovalStatus) {
      case "NeedsMoreInfo":
        return "border-amber-200 bg-amber-50/80 dark:border-amber-800 dark:bg-amber-950/30";
      case "PendingApproval":
        return "border-cortex-blue/20 bg-cortex-blue/5 dark:border-cortex-blue/40 dark:bg-cortex-blue/10";
      case "Rejected":
        return "border-red-200 bg-red-50/80 dark:border-red-900/40 dark:bg-red-950/25";
      case "Approved":
      default:
        return "border-emerald-200 bg-emerald-50/80 dark:border-emerald-800 dark:bg-emerald-950/25";
    }
  })();

  const ticketDetailsBody = (
    <div className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Created By
        </p>
        <p className="mt-1 text-gray-800 dark:text-slate-200">
          {formatDisplayValue(createdByName)}
        </p>
      </div>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Created Date
        </p>
        <p className="mt-1 text-gray-800 dark:text-slate-200">
          {formatDisplayDateTime(ticket.createdDate)}
        </p>
      </div>
      {ticket.lastModifiedDate ? (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Last Updated
          </p>
          <p className={`mt-1 ${ticketActivity ? ticketActivity.textClass : "text-gray-800 dark:text-slate-200"}`}>
            {formatDisplayDateTime(ticket.lastModifiedDate)}
            {ticketActivity ? ` · ${ticketActivity.label}` : ""}
          </p>
        </div>
      ) : null}
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Board
        </p>
        <p className="mt-1 text-gray-800 dark:text-slate-200">
          {formatDisplayValue(selectedBoard?.name ?? ticket.boardName)}
        </p>
      </div>
      {selectedBoardRequiresStoryPoints && (
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
            Story Points
          </p>
          <p className="mt-1 text-gray-800 dark:text-slate-200">
            {storyPoints === "" ? "—" : storyPoints}
          </p>
        </div>
      )}
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Syniti Owner
        </p>
        <p className="mt-1 text-gray-800 dark:text-slate-200">
          {formatDisplayValue(
            readOnlyOwnerDetailDisplay(synitiOwner, {
              baselineStored: ticket.synitiOwner ?? "",
              apiDisplayName: ticket.synitiOwnerDisplayName,
              directory: ownerDirectory,
            }),
          )}
        </p>
      </div>
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Business Owner
        </p>
        <p className="mt-1 text-gray-800 dark:text-slate-200">
          {formatDisplayValue(
            readOnlyOwnerDetailDisplay(businessOwner, {
              baselineStored: ticket.businessOwner ?? "",
              apiDisplayName: ticket.businessOwnerDisplayName,
              directory: ownerDirectory,
            }),
          )}
        </p>
      </div>
      {hasPersistedSla ? (
        <>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              SLA Status
            </p>
            <CortexTooltip content={slaTooltip}>
              <span
                className={`mt-1 inline-flex cursor-help rounded-full px-2.5 py-1 text-xs font-semibold ${slaBadgeClass}`}
                tabIndex={0}
              >
                {slaDisplayLabel}
              </span>
            </CortexTooltip>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              SLA Deadline
            </p>
            <p className="mt-1 text-gray-800 dark:text-slate-200">
              {slaDisplayLabel === "Paused"
                ? "Not started"
                : formatDisplayDateTime(ticket.slaTargetDate)}
            </p>
          </div>
          <div className="sm:col-span-2">
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              SLA Tracking
            </p>
            <p className="mt-1 text-gray-700 dark:text-slate-300">
              {formatSlaSummary(ticket)}
            </p>
          </div>
          {waitingOnLabel ? (
            <div className="sm:col-span-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                Waiting On
              </p>
              <p className="mt-1 text-gray-700 dark:text-slate-300">
                {waitingOnLabel}
                {ticketActivity ? ` · ${ticketActivity.label}` : ""}
              </p>
            </div>
          ) : null}
          {ticket.slaCompletedDate && (
            <div>
              <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                SLA Completed
              </p>
              <p className="mt-1 text-gray-800 dark:text-slate-200">
                {formatDisplayDateTime(ticket.slaCompletedDate)}
              </p>
            </div>
          )}
        </>
      ) : null}
    </div>
  );

  return (
    <div className="scroll-surface fixed inset-0 z-50 overflow-x-hidden overflow-y-hidden overscroll-y-contain">
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      {/* Modal: wrapper is pointer-events-none so wheel/click pass through to backdrop outside the dialog */}
      <div className="pointer-events-none flex min-h-full w-full items-start justify-center p-3 sm:items-center sm:p-4">
        <div
          className={`pointer-events-auto relative z-10 ${ticketModalShellClass} rounded-lg border border-gray-200 bg-white p-4 text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100 sm:p-6`}
          tabIndex={-1}
        >
          <div
            className={
              ticketModalIsWorkspaceLayout
                ? `grid min-h-0 flex-1 gap-4 overflow-hidden sm:gap-6 ${ticketModalGridClass}`
                : `grid h-[min(85vh,calc(100dvh-7.5rem))] min-h-0 gap-4 overflow-hidden sm:h-[min(88vh,calc(100dvh-8.5rem))] sm:gap-6 ${ticketModalGridClass}`
            }
          >
            {/* ================= MAIN: ticket details / editing ================= */}
            <div className="relative flex h-full max-h-full min-h-0 min-w-0 flex-col overflow-hidden">
              <ScrollableViewport
                viewportRef={mainColumnScrollRef}
                outerClassName="flex min-h-0 flex-1 flex-col overflow-hidden"
                viewportClassName="relative max-h-full min-h-0 flex-1 basis-0 space-y-6 overflow-y-auto overflow-x-hidden overscroll-y-contain pr-1 touch-pan-y"
                affordanceAriaLabel="Scroll ticket details down"
                affordanceScrollStepPx={320}
              >
                  {/* Header */}
                  <div className="flex items-start justify-between gap-3 border-b border-gray-200 pb-5 dark:border-slate-800">
                    <div className="min-w-0 flex-1">
                      {(isCreateMode || !title.trim()) && (
                        <label
                          htmlFor={titleFieldId}
                          className="mb-2 block text-lg font-medium text-gray-700 dark:text-slate-300"
                        >
                          Enter Ticket Title
                          {isCreateMode && (
                            <span className="ml-1 text-red-600 dark:text-red-400">
                              *
                            </span>
                          )}
                        </label>
                      )}
                      <input
                        id={titleFieldId}
                        ref={titleInputRef}
                        type="text"
                        value={title}
                        onChange={(e) => setTitle(e.target.value)}
                        readOnly={formReadOnly}
                        placeholder={
                          isCreateMode || !title.trim()
                            ? "Enter ticket title..."
                            : undefined
                        }
                        aria-label={
                          !isCreateMode && title.trim()
                            ? "Ticket title"
                            : undefined
                        }
                        className="mb-1 w-full min-w-0 truncate border-b border-gray-300 bg-transparent text-lg font-bold leading-tight text-gray-900 focus:border-cortex-blue focus:outline-none read-only:cursor-not-allowed read-only:opacity-80 dark:border-slate-700 dark:text-slate-100 sm:text-xl"
                      />
                      {isCreateMode && validationErrors.title && (
                        <p className="mt-1 text-xs text-red-600 dark:text-red-400">
                          {validationErrors.title}
                        </p>
                      )}
                      <p className="text-sm text-gray-500 dark:text-slate-400">
                        {formatTicketIdentifier(ticket.id)}
                      </p>
                      {ticket.id && (
                        <div>
                          <div className="mt-3 flex flex-wrap items-center gap-2">
                          {!isRequesterIntakeTicket ? (
                            <span className="rounded-full bg-cortex-blue-soft px-3 py-1 text-xs font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
                              {status}
                            </span>
                          ) : null}
                          {!isRequesterIntakeTicket ? (
                            <span
                              className={`rounded-full px-3 py-1 text-xs font-semibold ${priorityBadgeClass}`}
                            >
                              {priority}
                            </span>
                          ) : null}
                          {!isRequesterIntakeTicket ? (
                            <CortexTooltip content={slaTooltip}>
                              <span
                                className={`cursor-help rounded-full px-3 py-1 text-xs font-semibold ${slaBadgeClass}`}
                                tabIndex={0}
                              >
                                {slaDisplayLabel}
                              </span>
                            </CortexTooltip>
                          ) : null}
                          {approvalBadgePresentation ? (
                            <span
                              className={approvalBadgePresentation.className}
                            >
                              {approvalBadgePresentation.label}
                            </span>
                          ) : null}
                          </div>
                          {isApprovalQueueContext &&
                          getTicketApprovalStatus(ticket) === "PendingApproval" &&
                          /\bin\s*progress\b/i.test((status ?? "").trim()) ? (
                            <p className="mt-2 max-w-prose text-xs leading-snug text-amber-800 dark:text-amber-200">
                              Approval may still be required even while ticket Status looks
                              ahead—workflow fields stay editable until approval clears.
                            </p>
                          ) : null}
                        </div>
                      )}
                      {isCreateMode && (
                        <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                          Fields marked with * are required.
                        </p>
                      )}
                    </div>
                    <button
                      onClick={onClose}
                      className="text-gray-400 hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300 text-2xl font-bold"
                    >
                      ×
                    </button>
                  </div>

                  {(urgencyGuidanceText || canAssignToMe) ? (
                    <div
                      className={`flex flex-wrap items-center justify-between gap-3 rounded-md border px-4 py-3 ${
                        urgencyChip
                          ? urgencyChip.chipClass
                          : "border-gray-200 bg-gray-50 dark:border-slate-700 dark:bg-slate-800/50"
                      }`}
                    >
                      {urgencyGuidanceText ? (
                        <p className="text-sm font-medium">
                          {urgencyGuidanceText}
                        </p>
                      ) : (
                        <p className="text-sm text-gray-600 dark:text-slate-300">
                          Quick actions
                        </p>
                      )}
                      {canAssignToMe && (
                        <button
                          onClick={() => void handleAssignToMe()}
                          disabled={saving || archiving}
                          className="shrink-0 rounded-md border border-current bg-white/60 px-3 py-1.5 text-xs font-semibold transition-colors hover:bg-white/90 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-black/20 dark:hover:bg-black/40"
                        >
                          Assign to me
                        </button>
                      )}
                    </div>
                  ) : null}

                  {ticket.id ? (
                    <div className="space-y-3">
                      {requesterApprovalStatus !== "Approved" ||
                      showApprovedApprovalState ? (
                        <ApprovalOutcomeMessage
                          ticket={ticket}
                          variant="modalBanner"
                          audience={
                            intakeApprovalHandlers ? "reviewer" : "requester"
                          }
                        />
                      ) : null}
                    </div>
                  ) : null}

                  {showRequesterRequestSummary ? (
                    <div
                      className={`rounded-md border p-4 ${requesterSummaryToneClass}`}
                    >
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-gray-500 dark:text-slate-400">
                            Request Summary
                          </p>
                          <p className="mt-2 text-sm leading-snug text-gray-800 dark:text-slate-100">
                            {requesterSummaryCopy(ticket)}
                          </p>
                        </div>
                        {requesterApprovalStatus === "NeedsMoreInfo" ? (
                          <span className="inline-flex items-center rounded-full border border-amber-300 bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-950 dark:border-amber-700 dark:bg-amber-900/50 dark:text-amber-100">
                            Action needed
                          </span>
                        ) : null}
                      </div>

                      <div className="mt-4 grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                            Submitted
                          </p>
                          <p className="mt-1 text-gray-800 dark:text-slate-200">
                            {formatDisplayDateTime(ticket.createdDate)}
                          </p>
                        </div>
                        <div>
                          <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                            {requesterApprovalStatus === "Approved"
                              ? "Active board"
                              : "Requested board"}
                          </p>
                          <p className="mt-1 text-gray-800 dark:text-slate-200">
                            {formatDisplayValue(
                              selectedBoard?.name ?? ticket.boardName,
                            )}
                          </p>
                        </div>
                        {ticket.lastModifiedDate ? (
                          <div>
                            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                              Last updated
                            </p>
                            <p className="mt-1 text-gray-800 dark:text-slate-200">
                              {formatDisplayDateTime(ticket.lastModifiedDate)}
                            </p>
                          </div>
                        ) : null}
                        {requesterApprovalStatus === "Approved" ? (
                          <div>
                            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                              Current status
                            </p>
                            <p className="mt-1 text-gray-800 dark:text-slate-200">
                              {status}
                            </p>
                          </div>
                        ) : null}
                      </div>
                    </div>
                  ) : null}

                  {approvalDisplayContext !== "reviewer" && ticket.id
                    ? sourceContextBundleSection
                    : null}

                  {/* Description */}
                  <div className="rounded-md border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900/40">
                    <div className="mb-2 flex items-start justify-between gap-3">
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Description
                        {isCreateMode && (
                          <span className="ml-1 text-red-600 dark:text-red-400">
                            *
                          </span>
                        )}
                      </label>
                      {isCreateMode && !formReadOnly && (
                        <button
                          type="button"
                          onClick={handleImproveIntake}
                          disabled={
                            intakeAssistLoading || !description.trim()
                          }
                          className={`ai-button inline-flex shrink-0 items-center gap-1 rounded-md px-2.5 py-1.5 text-xs font-semibold text-cortex-blue-dark hover:bg-cortex-blue-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cortex-blue disabled:cursor-not-allowed disabled:opacity-55 dark:text-emerald-300 dark:hover:bg-emerald-950/40 dark:hover:text-emerald-200 ${
                            description.trim() ? "ai-button--ready" : ""
                          }`}
                        >
                          <span className="inline-flex items-center gap-1">
                            {intakeAssistLoading
                              ? "Improving…"
                              : "Improve for review"}
                          </span>
                        </button>
                      )}
                    </div>
                    {isCreateMode && !formReadOnly ? (
                      <p className="mb-2 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
                        Improve this request before submission so reviewers can
                        act without extra follow-up.
                      </p>
                    ) : null}
                    <textarea
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      readOnly={formReadOnly}
                      rows={4}
                      placeholder="Enter ticket description..."
                      className="w-full rounded-md border-gray-300 bg-white text-gray-900 leading-[1.5] shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500 read-only:cursor-not-allowed read-only:opacity-80"
                    />
                    {isCreateMode && validationErrors.description && (
                      <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                        {validationErrors.description}
                      </p>
                    )}
                    {intakeAssistError && (
                      <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                        {intakeAssistError}
                      </p>
                    )}
                    {intakeAssistResult && (
                      <IntakeAssistResultPanel
                        key={getIntakeAssistResultFingerprint(
                          intakeAssistResult,
                        )}
                        result={intakeAssistResult}
                        editableDescription={intakeAssistEditableDescription}
                        onChangeEditableDescription={
                          setIntakeAssistEditableDescription
                        }
                        onUseSummary={handleUseIntakeSummary}
                        onUseDescription={handleUseIntakeDescription}
                        onDismiss={handleDismissIntakeAssist}
                      />
                    )}
                  </div>

                  {!ticket.id && (
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                        Suggested Department
                      </label>
                      <input
                        type="text"
                        value={department}
                        onChange={(e) => setDepartment(e.target.value)}
                        placeholder="Defaults from your profile"
                        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
                      />
                      <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                        Used with title and department decision factors when you
                        leave the owner fields blank.
                      </p>
                    </div>
                  )}

                  {/* Editable Fields */}
                  <div className="grid grid-cols-1 gap-5 rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-800/40 md:grid-cols-2">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                        Board
                      </label>
                      <select
                        value={boardId}
                        onChange={(e) =>
                          handleBoardSelectionChange(Number(e.target.value))
                        }
                        disabled={Boolean(ticket.id && formReadOnly)}
                        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {ticketBoards.map((board) => (
                          <option key={board.id} value={board.id}>
                            {board.name}
                            {board.isEnabled ? "" : " (Disabled)"}
                          </option>
                        ))}
                      </select>
                    </div>

                    {selectedBoardRequiresStoryPoints ? (
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                          Story Points
                        </label>
                        <select
                          value={storyPoints}
                          onChange={(e) =>
                            setStoryPoints(Number(e.target.value))
                          }
                          disabled={Boolean(ticket.id && formReadOnly)}
                          className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {[1, 2, 3, 4, 5].map((value) => (
                            <option key={value} value={value}>
                              {value}
                            </option>
                          ))}
                        </select>
                        {isCreateMode && validationErrors.storyPoints && (
                          <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                            {validationErrors.storyPoints}
                          </p>
                        )}
                      </div>
                    ) : (
                      <div className="rounded-md border border-dashed border-gray-300 bg-gray-50 px-4 py-3 text-sm text-gray-500 dark:border-slate-700 dark:bg-slate-950/40 dark:text-slate-400">
                        Story points are only used on enhancement-style boards.
                      </div>
                    )}

                    {/* Priority */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                        Priority
                        {isCreateMode && (
                          <span className="ml-1 text-red-600 dark:text-red-400">
                            *
                          </span>
                        )}
                      </label>
                      <select
                        value={priority}
                        onChange={(e) => setPriority(e.target.value)}
                        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                      >
                        <option value="Critical">Critical</option>
                        <option value="High">High</option>
                        <option value="Medium">Medium</option>
                        <option value="Low">Low</option>
                      </select>
                      {isCreateMode && validationErrors.priority && (
                        <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                          {validationErrors.priority}
                        </p>
                      )}
                      {!isCreateMode &&
                      approvalDisplayContext !== "requester" &&
                      getTicketApprovalStatus(ticket) === "PendingApproval" ? (
                        <p className="mt-1.5 text-xs text-gray-500 dark:text-slate-400">
                          AI triage may update this ticket&apos;s priority and
                          status to match configured vocabulary when it suggests
                          a change. You can change either before approving.
                        </p>
                      ) : null}
                    </div>

                    {/* Status */}
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                        Status
                      </label>
                      {isCreateMode ? (
                        <input
                          type="text"
                          value="New"
                          readOnly
                          className="w-full rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                        />
                      ) : (
                        <select
                          value={status}
                          onChange={(e) => setStatus(e.target.value)}
                          disabled={Boolean(ticket.id && formReadOnly)}
                          className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                          {availableStatusOptions.map((statusDefinition) => (
                            <option
                              key={`${statusDefinition.id}-${statusDefinition.name}`}
                              value={statusDefinition.name}
                            >
                              {statusDefinition.name}
                              {statusDefinition.isEnabled ? "" : " (Disabled)"}
                            </option>
                          ))}
                        </select>
                      )}
                      {isCreateMode && (
                        <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                          Status defaults to New when creating a ticket.
                        </p>
                      )}
                    </div>

                    {/* Syniti Owner */}
                    <div>
                      <UserCombobox
                        label="Syniti Owner"
                        value={synitiOwner}
                        users={synitiOwnerOptions}
                        onChange={setSynitiOwner}
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled}
                        helperText={synitiOwnerHelperText}
                      />
                    </div>

                    {/* Business Owner */}
                    <div>
                      <UserCombobox
                        label="Business Owner"
                        value={businessOwner}
                        users={businessOwnerOptions}
                        onChange={setBusinessOwner}
                        loading={ownerDirectoryLoading}
                        disabled={ownerPickerDisabled}
                        helperText={businessOwnerHelperText}
                      />
                    </div>
                  </div>

                  {showChangeReasonField && (
                    <div className="mb-6">
                      <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Change Reason
                      </label>
                      <input
                        type="text"
                        value={changeReason}
                        onChange={(e) => setChangeReason(e.target.value)}
                        placeholder="Optional: explain why you're updating or archiving this ticket"
                        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
                      />
                    </div>
                  )}

                  <div className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-800/60">
                    <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                      <div>
                        <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                          Attachments
                        </label>
                        <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                          Add screenshots or supporting files to this ticket.
                        </p>
                      </div>

                      <button
                        type="button"
                        className="inline-flex cursor-default items-center rounded-md bg-cortex-blue px-3 py-2 text-sm font-medium text-white"
                      >
                        Attachments
                      </button>
                    </div>

                    {approvalDisplayContext === "reviewer" &&
                    ticket.id &&
                    imageAttachmentsForInsight.length > 0 ? (
                      <div className="mt-3 space-y-3">
                        <div className="flex flex-col gap-1.5 sm:flex-row sm:flex-wrap sm:items-start sm:gap-x-3 sm:gap-y-1.5">
                          <button
                            type="button"
                            onClick={() => void handleAnalyzeScreenshots()}
                            disabled={screenshotInsightLoading}
                            className="ai-button ai-button--ready inline-flex min-h-[2.5rem] shrink-0 items-center justify-center rounded-md px-3 py-2 text-xs font-semibold text-cortex-blue-dark hover:bg-cortex-blue-soft focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cortex-blue disabled:cursor-not-allowed disabled:opacity-60 dark:text-emerald-300 dark:hover:bg-emerald-950/40"
                          >
                            {screenshotInsightLoading ? (
                              <span className="inline-flex items-center">
                                <svg
                                  className="mr-2 h-4 w-4 shrink-0 animate-spin text-current"
                                  xmlns="http://www.w3.org/2000/svg"
                                  fill="none"
                                  viewBox="0 0 24 24"
                                  aria-hidden="true"
                                >
                                  <circle
                                    className="opacity-25"
                                    cx="12"
                                    cy="12"
                                    r="10"
                                    stroke="currentColor"
                                    strokeWidth="4"
                                  />
                                  <path
                                    className="opacity-75"
                                    fill="currentColor"
                                    d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                                  />
                                </svg>
                                Analyzing screenshots…
                              </span>
                            ) : (
                              <span>Analyze screenshots</span>
                            )}
                          </button>
                          <span className="text-xs text-gray-500 dark:text-slate-400 sm:pt-2">
                            {imageAttachmentsForInsight.length} image
                            {imageAttachmentsForInsight.length === 1
                              ? ""
                              : "s"}{" "}
                            (PNG, JPG, WEBP)
                          </span>
                        </div>
                        {screenshotInsightAutoHint ? (
                          <p
                            className="text-[11px] text-gray-500 dark:text-slate-500"
                            aria-live="polite"
                          >
                            Analyzing screenshots…
                          </p>
                        ) : null}
                        <p className="max-w-xl text-xs leading-snug text-gray-500 dark:text-slate-500">
                          {showAiTriageColumn
                            ? "Screenshot analysis adds reviewer-ready visual evidence to the Evidence tab."
                            : "Understand what&apos;s happening from screenshots before asking follow-up questions."}
                        </p>
                        {screenshotInsightError ? (
                          <p className="text-xs text-red-600 dark:text-red-400">
                            {screenshotInsightError}
                          </p>
                        ) : null}
                      </div>
                    ) : null}

                    <div className="mt-4 space-y-3">
                      <label
                        onDragOver={handleAttachmentDragOver}
                        onDragEnter={handleAttachmentDragOver}
                        onDragLeave={handleAttachmentDragLeave}
                        onDrop={handleAttachmentDrop}
                        className={`flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed px-4 py-6 text-center transition-colors ${
                          isAttachmentDropActive
                            ? "border-cortex-blue bg-cortex-blue/10"
                            : "border-gray-300 bg-white/70 hover:border-cortex-blue/60 hover:bg-cortex-blue/5 dark:border-slate-700 dark:bg-slate-900/40 dark:hover:border-cortex-blue/50 dark:hover:bg-cortex-blue/10"
                        }`}
                      >
                        <input
                          type="file"
                          multiple
                          accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,.txt,.csv"
                          className="sr-only"
                          onChange={addQueuedAttachments}
                        />
                        <span className="text-sm font-medium text-gray-900 dark:text-slate-100">
                          {isAttachmentDropActive
                            ? "Drop your picture here"
                            : "Drag and drop a picture here"}
                        </span>
                        <span className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                          or click to browse screenshots and supporting files
                        </span>
                        <span className="mt-2 text-xs text-gray-400 dark:text-slate-500">
                          Images, PDFs, Office documents, text, and CSV files
                          are supported.
                        </span>
                      </label>

                      {ticket.id ? (
                        loadingAttachments ? (
                          <p className="text-sm text-gray-500 dark:text-slate-400">
                            Loading attachments…
                          </p>
                        ) : attachments.length > 0 ? (
                          attachments.map((attachment) => (
                            <div
                              key={attachment.id}
                              className="flex flex-col gap-3 rounded-md border border-gray-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900/70 md:flex-row md:items-center md:justify-between"
                            >
                              <div className="min-w-0">
                                <p className="truncate text-sm font-medium text-gray-900 dark:text-slate-100">
                                  {attachment.fileName}
                                </p>
                                <p className="text-xs text-gray-500 dark:text-slate-400">
                                  {formatFileSize(attachment.fileSize)} ·{" "}
                                  {attachment.contentType} ·{" "}
                                  {attachment.uploadedByDisplayName} ·{" "}
                                  {new Date(
                                    attachment.uploadedDate,
                                  ).toLocaleString()}
                                </p>
                              </div>

                              <div className="flex items-center gap-2">
                                <button
                                  onClick={() =>
                                    void openAttachment(attachment)
                                  }
                                  disabled={
                                    attachmentActionId === attachment.id
                                  }
                                  className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                                >
                                  Open
                                </button>
                                <button
                                  onClick={() =>
                                    void downloadAttachment(attachment)
                                  }
                                  disabled={
                                    attachmentActionId === attachment.id
                                  }
                                  className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                                >
                                  Download
                                </button>
                              </div>
                            </div>
                          ))
                        ) : (
                          <p className="text-sm text-gray-500 dark:text-slate-400">
                            No attachments uploaded yet.
                          </p>
                        )
                      ) : (
                        <p className="text-sm text-gray-500 dark:text-slate-400">
                          Selected attachments will upload after the ticket is
                          created.
                        </p>
                      )}

                      {queuedAttachments.length > 0 && (
                        <div className="space-y-2 rounded-md border border-dashed border-cortex-blue/40 bg-cortex-blue/5 p-3 dark:border-cortex-blue/30 dark:bg-cortex-blue/10">
                          <p className="text-xs font-semibold uppercase tracking-wide text-cortex-blue">
                            {ticket.id
                              ? "Ready to upload when you save"
                              : "Queued for upload when you create the ticket"}
                          </p>
                          {queuedAttachments.map((file) => (
                            <div
                              key={getQueuedAttachmentKey(file)}
                              className="flex items-center justify-between gap-3 rounded-md bg-white px-3 py-2 dark:bg-slate-900/70"
                            >
                              <div className="min-w-0">
                                <p className="truncate text-sm font-medium text-gray-900 dark:text-slate-100">
                                  {file.name}
                                </p>
                                <p className="text-xs text-gray-500 dark:text-slate-400">
                                  {formatFileSize(file.size)}
                                </p>
                              </div>
                              <button
                                onClick={() => removeQueuedAttachment(file)}
                                type="button"
                                className="text-sm text-red-600 transition-colors hover:text-red-700 dark:text-red-300 dark:hover:text-red-200"
                              >
                                Remove
                              </button>
                            </div>
                          ))}
                        </div>
                  )}
                    </div>
                  </div>
              </ScrollableViewport>

              {intakeApprovalHandlers && ticket.id ? (
                <div className="border-t border-gray-200 pt-3 dark:border-slate-800">
                  <p className="text-[11px] font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
                    Review actions
                  </p>
                  <p className="mt-1 text-xs text-gray-500 dark:text-slate-500">
                    Approve to move this ticket into active work.
                  </p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    <button
                      type="button"
                      disabled={intakeActionPending || saving || archiving}
                      onClick={async () => {
                        setIntakeActionPending(true);
                        try {
                          await intakeApprovalHandlers.approve();
                        } finally {
                          setIntakeActionPending(false);
                        }
                      }}
                      className="rounded-md bg-cortex-blue px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      {intakeActionPending ? "Working…" : "Approve"}
                    </button>
                    <button
                      type="button"
                      disabled={intakeActionPending || saving || archiving}
                      onClick={() => {
                        setIntakeReasonModal("return");
                        setIntakeReasonDraft("");
                      }}
                      className="rounded-md border border-amber-300 bg-amber-50/80 px-3 py-1.5 text-xs font-semibold text-amber-950 transition-colors hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-100 dark:hover:bg-amber-900/50"
                    >
                      Return for Detail
                    </button>
                    <button
                      type="button"
                      disabled={intakeActionPending || saving || archiving}
                      onClick={() => {
                        setIntakeReasonModal("reject");
                        setIntakeReasonDraft("");
                      }}
                      className="rounded-md border border-red-300 bg-red-50/80 px-3 py-1.5 text-xs font-semibold text-red-800 transition-colors hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-red-800 dark:bg-red-950/40 dark:text-red-200 dark:hover:bg-red-950/60"
                    >
                      Reject
                    </button>
                  </div>
                </div>
              ) : null}

              {/* Actions */}
              <div className="flex shrink-0 flex-col gap-3 border-t border-gray-200 bg-white pt-4 dark:border-slate-800 dark:bg-slate-900 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex min-w-0 flex-wrap items-center gap-2 sm:gap-3">
                  {ticket.id && (
                    <button
                      onClick={() => setIsHistoryModalOpen(true)}
                      disabled={saving || archiving}
                      className="px-4 py-2 rounded-md border border-gray-300 text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    >
                      History
                    </button>
                  )}

                  {ticket.id && (
                    <div ref={actionMenuRef} className="relative">
                      <button
                        type="button"
                        onClick={() =>
                          setIsActionMenuOpen((current) => !current)
                        }
                        disabled={saving || archiving}
                        className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        Actions ▴
                      </button>

                      {isActionMenuOpen && (
                        <div className="absolute bottom-full left-0 z-20 mb-2 max-w-[min(16rem,calc(100vw-2rem))] overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900 sm:left-auto sm:right-0">
                          {quickMoveBoards.length > 0 && (
                            <div className="border-b border-gray-100 px-2 py-2 dark:border-slate-800">
                              {quickMoveBoards.map((board) => (
                                <button
                                  key={board.id}
                                  type="button"
                                  onClick={() => void handleQuickMove(board)}
                                  disabled={saving || archiving}
                                  className="w-full rounded-md px-3 py-2 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60 dark:text-slate-200 dark:hover:bg-slate-800"
                                >
                                  Move to {board.name}
                                  {board.requiresStoryPoints
                                    ? " (Story Points)"
                                    : ""}
                                </button>
                              ))}
                            </div>
                          )}

                          {canArchiveTicket && (
                            <button
                              type="button"
                              onClick={() => void handleArchive()}
                              disabled={saving || archiving}
                              className="w-full px-4 py-3 text-left text-sm text-amber-700 transition-colors hover:bg-amber-50 disabled:cursor-not-allowed disabled:opacity-60 dark:text-amber-300 dark:hover:bg-amber-950/20"
                            >
                              {archiving ? "Archiving..." : "Archive"}
                            </button>
                          )}

                          {canDeleteTicket && (
                            <button
                              type="button"
                              onClick={handleDelete}
                              disabled={saving || archiving}
                              className="w-full px-4 py-3 text-left text-sm text-red-600 transition-colors hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60 dark:text-red-300 dark:hover:bg-red-950/30"
                            >
                              Delete
                            </button>
                          )}

                          <button
                            type="button"
                            onClick={() => {
                              setIsTicketDetailsOpen(true);
                              setIsActionMenuOpen(false);
                            }}
                            className={`w-full px-4 py-3 text-left text-sm text-gray-700 transition-colors hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800 ${
                              quickMoveBoards.length > 0 ||
                              canArchiveTicket ||
                              canDeleteTicket
                                ? "border-t border-gray-100 dark:border-slate-800"
                                : ""
                            }`}
                          >
                            {isRequesterContext
                              ? "Request details"
                              : "Ticket details"}
                          </button>
                        </div>
                      )}
                    </div>
                  )}
                </div>

                <div className="flex flex-shrink-0 flex-wrap items-center justify-end gap-2 sm:gap-3">
                  <button
                    onClick={onClose}
                    disabled={archiving}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                  >
                    Cancel
                  </button>
                  {canSaveTicket && (
                    <button
                      onClick={handleSave}
                      disabled={
                        saving ||
                        archiving ||
                        (isCreateMode
                          ? hasCreateValidationErrors
                          : !title.trim()) ||
                        (selectedBoardRequiresStoryPoints && storyPoints === "")
                      }
                      className="rounded-md bg-cortex-blue px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {saving
                        ? ticket.id
                          ? "Saving..."
                          : "Creating..."
                        : ticket.id
                          ? "Save Changes"
                          : "Create Ticket"}
                    </button>
                  )}
                </div>
              </div>
            </div>

            {/* ================= REVIEWER: CORTEX TABBED PANEL (right rail) ================= */}
            {showAiTriageColumn ? (
              <div className="relative flex h-full max-h-full min-h-0 min-w-0 flex-col overflow-hidden">
                <CortexTabbedPanel
                  key={ticket.id || "new-ticket-cortex"}
                  ticket={triageDisplayTicket}
                  isModalOpen={isOpen}
                  ticketBoards={ticketBoards}
                  livePreview={routingLivePreviewInput}
                  riskLevel={latestRisk?.riskLevel ?? null}
                  onRiskReady={setLatestRisk}
                  onOpenSourceTicket={onOpenSourceTicket}
                  sourceContextSlot={reviewerSourceContextTabSlot ?? undefined}
                  governanceContextEvidenceSlot={
                    reviewerGovernanceSummarySlot ?? undefined
                  }
                  sapReferenceEvidenceSlot={
                    reviewerSapEvidenceSlot ?? undefined
                  }
                  synitiKnowledgeEvidenceSlot={
                    reviewerSynitiEvidenceSlot ?? undefined
                  }
                  sapDecisionAssistMatches={sapDecisionAssistMatches}
                  sapIntentOnly={sapIntentOnlyForAssist}
                  sapDecisionAssistTicketText={sapDecisionAssistTicketText}
                  onReassignmentApplied={(updatedTicket) => {
                    applyServerTicketToForm(updatedTicket);
                    onTriageApplySuccess?.(updatedTicket);
                  }}
                  reviewSlot={
                    <ApprovalTriageModalColumn
                      ticket={triageDisplayTicket}
                      onRegenerateAnalysis={handleRegenerateTriageAnalysis}
                      canRegenerateAnalysis={canOfferTriageRegenerate}
                      regenerateDisabledHint={triageRegenerateDisabledReason}
                      regenerateLoading={regenerateTriageLoading}
                      applyControls={triageApplyControls}
                    />
                  }
                  intakeSlot={
                    reviewerIntakeQualityKind !== null &&
                    reviewerIntakeQualityCopy ? (
                      <div className="mb-3 rounded-md border border-gray-200 bg-gray-50 p-2.5 dark:border-slate-700 dark:bg-slate-900/50">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={`inline-flex shrink-0 items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                              reviewerIntakeQualityKind === "none"
                                ? "border border-slate-200 bg-slate-100 text-slate-800 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-200"
                                : reviewerIntakeQualityKind === "ready"
                                  ? CLARITY_STATE_PILL_CLASS.ready_for_execution
                                  : reviewerIntakeQualityKind === "gaps"
                                    ? CLARITY_STATE_PILL_CLASS.would_have_required_follow_up
                                    : CLARITY_STATE_PILL_CLASS.requires_clarification
                            }`}
                          >
                            {reviewerIntakeQualityCopy.title}
                          </span>
                        </div>
                        <p className="mt-1.5 text-xs leading-relaxed text-gray-600 dark:text-slate-400">
                          {reviewerIntakeQualityCopy.body}
                        </p>
                      </div>
                    ) : null
                  }
                  evidenceSlot={
                    screenshotInsightResult ? (
                      <ScreenshotInsightEvidenceCard
                        result={screenshotInsightResult}
                        compactForReviewerRail={showAiTriageColumn}
                      />
                    ) : null
                  }
                />
              </div>
            ) : null}

            {/* ================= COMMENTS ================= */}
            {showCommentsColumn && (
              <div className="relative flex h-full max-h-full min-h-0 min-w-0 flex-col overflow-hidden rounded-md border border-gray-200 bg-gray-50/60 p-4 dark:border-slate-800 dark:bg-slate-900/30 lg:h-full">
                <div className="mb-3 flex items-center justify-between border-b border-gray-200 pb-2 dark:border-slate-800">
                  <h3 className="text-sm font-semibold text-gray-700 dark:text-slate-300">
                    Comments
                  </h3>
                  <span className="rounded-full bg-gray-200 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-slate-800 dark:text-slate-300">
                    {comments.length}
                  </span>
                </div>

                <ScrollableViewport
                  viewportRef={commentThreadScrollRef}
                  outerClassName="flex min-h-0 flex-1 flex-col overflow-hidden"
                  viewportClassName="relative max-h-full min-h-0 flex-1 basis-0 overflow-y-auto overscroll-y-contain pr-1 touch-pan-y"
                  affordanceAriaLabel="Scroll comments to bottom"
                  viewportProps={{ onScroll: handleCommentThreadScroll }}
                >
                  {loadingComments ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      Loading comments…
                    </p>
                  ) : (
                    <CommentList comments={comments} />
                  )}
                  {pendingNewCommentsCount > 0 && (
                    <div className="sticky bottom-0 z-10 flex justify-center pt-2 pointer-events-none">
                      <button
                        type="button"
                        onClick={scrollCommentThreadToBottom}
                        className="pointer-events-auto rounded-full border border-cortex-blue/40 bg-white px-3 py-1.5 text-xs font-semibold text-cortex-blue shadow-sm transition-colors hover:bg-cortex-blue/10 dark:border-cortex-blue/50 dark:bg-slate-800 dark:text-cortex-blue dark:hover:bg-slate-700/80"
                      >
                        New comments
                        {pendingNewCommentsCount > 1
                          ? ` (${pendingNewCommentsCount})`
                          : ""}
                      </button>
                    </div>
                  )}
                </ScrollableViewport>

                <div className="mt-3">
                  {typingIndicatorText && (
                    <p className="mb-2 text-xs text-gray-500 dark:text-slate-400">
                      {typingIndicatorText}
                    </p>
                  )}
                  <AddComment
                    onAdd={addComment}
                    onTyping={() => {
                      void signalTyping();
                    }}
                    disabled={!canCreateTickets(authRoles)}
                  />
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {ticket.id && isTicketDetailsOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/50 dark:bg-black/60"
            aria-hidden="true"
            onClick={() => setIsTicketDetailsOpen(false)}
          />
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="ticket-details-dialog-title"
            className="relative z-10 w-full max-w-lg overflow-hidden rounded-lg border border-gray-200 bg-white text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3 dark:border-slate-800">
              <h2
                id="ticket-details-dialog-title"
                className="text-base font-semibold text-gray-900 dark:text-slate-100"
              >
                Ticket Details
              </h2>
              <button
                type="button"
                onClick={() => setIsTicketDetailsOpen(false)}
                className="rounded-md p-1.5 text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-800 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200"
                aria-label="Close ticket details"
              >
                <span className="text-lg leading-none" aria-hidden="true">
                  ×
                </span>
              </button>
            </div>
            <ScrollableViewport
              viewportRef={ticketDetailsScrollRef}
              viewportClassName="max-h-[min(70dvh,28rem)] overflow-y-auto p-4"
              affordanceAriaLabel="Scroll ticket details summary to bottom"
            >
                {ticketDetailsBody}
            </ScrollableViewport>
          </div>
        </div>
      )}

      {intakeReasonModal ? (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 px-4">
          <div
            className="w-full max-w-lg rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-800 dark:bg-slate-900"
            role="dialog"
            aria-modal="true"
            aria-labelledby="ticket-intake-reason-dialog-title"
          >
            <h2
              id="ticket-intake-reason-dialog-title"
              className="text-lg font-semibold text-gray-900 dark:text-slate-100"
            >
              {intakeReasonModal === "return"
                ? "Return for detail"
                : "Reject ticket"}
            </h2>
            <textarea
              value={intakeReasonDraft}
              onChange={(e) => setIntakeReasonDraft(e.target.value)}
              rows={5}
              className="mt-4 w-full rounded-md border border-gray-300 bg-white p-3 text-sm text-gray-900 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
              placeholder="Reason…"
            />
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => {
                  setIntakeReasonModal(null);
                  setIntakeReasonDraft("");
                }}
                className="rounded-md border border-gray-300 px-4 py-2 text-sm text-gray-700 dark:border-slate-600 dark:text-slate-200"
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={intakeActionPending}
                onClick={async () => {
                  const trimmed = intakeReasonDraft.trim();
                  if (!trimmed) {
                    toast.error("A reason is required.");
                    return;
                  }
                  if (trimmed.length > 2000) {
                    toast.error("Reason must be 2000 characters or fewer.");
                    return;
                  }
                  if (!intakeApprovalHandlers) {
                    return;
                  }
                  setIntakeActionPending(true);
                  try {
                    if (intakeReasonModal === "return") {
                      await intakeApprovalHandlers.returnForDetail(trimmed);
                    } else {
                      await intakeApprovalHandlers.reject(trimmed);
                    }
                    setIntakeReasonModal(null);
                    setIntakeReasonDraft("");
                  } finally {
                    setIntakeActionPending(false);
                  }
                }}
                className="rounded-md bg-cortex-blue px-4 py-2 text-sm font-semibold text-white hover:bg-cortex-blue-dark disabled:opacity-50"
              >
                {intakeActionPending ? "Submitting…" : "Submit"}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {ticket.id && (
        <TicketHistoryModal
          ticketId={ticket.id}
          isOpen={isHistoryModalOpen}
          onClose={() => setIsHistoryModalOpen(false)}
        />
      )}
    </div>
  );
}
