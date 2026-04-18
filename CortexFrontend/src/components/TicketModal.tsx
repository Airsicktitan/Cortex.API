import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type DragEvent,
} from "react";
import type { Ticket, TicketMutationInput, TicketSaveOutcome } from "../types/ticket";
import type { TicketAttachment } from "../types/attachment";
import type { RealtimeEvent } from "../types/realtime";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { TicketStatusDefinition } from "../types/ticketStatus";
import type { UserDirectoryEntry } from "../types/user";
import { commentService } from "../services/commentService";
import {
  attachmentService,
  getUserFacingErrorMessage,
  userService,
} from "../services/api";
import type { Comment } from "../types/comment";
import CommentList from "./CommentList";
import AddComment from "./AddComment";
import TicketHistoryModal from "./TicketHistoryModal";
import UserCombobox from "./UserCombobox";
import TicketRoutingInsight from "./TicketRoutingInsight";
import { useAuth0 } from "@auth0/auth0-react";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaBadgeClass,
  getSlaDisplayLabel,
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
} from "../utils/role";
import { readOnlyOwnerDetailDisplay } from "../utils/ownerIdentity";

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
  const existingIndex = current.findIndex((comment) => comment.id === incoming.id);
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
  ) => Promise<TicketSaveOutcome | void>;
  onArchive: (ticket: Ticket, changeReason?: string) => Promise<void>;
  onDelete: (ticket: Ticket) => void;
  currentUser: {
    displayName: string;
    department?: string;
    role?: string;
    roles?: string[];
  } | null;
  createdByDisplayName: string;
}

type CreateFormField = "title" | "description" | "priority" | "storyPoints";
type CreateFormErrors = Partial<Record<CreateFormField, string>>;
type TypingPresence = {
  key: string;
  displayName?: string;
  expiresAt: number;
};

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
  const [ownerDirectory, setOwnerDirectory] = useState<UserDirectoryEntry[]>([]);
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
  const titleInputRef = useRef<HTMLInputElement | null>(null);
  const actionMenuRef = useRef<HTMLDivElement | null>(null);

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
  const commentThreadNearBottomRef = useRef(true);
  const commentThreadOpenScrollPendingRef = useRef(false);
  const commentThreadSendScrollPendingRef = useRef(false);
  const prevCommentIdsSigRef = useRef<string>("");
  /** Latest ticket from props; used so we don't list `ticket` as a layout-effect dependency (avoids resetting the form on every parent/realtime object update). */
  const ticketPropRef = useRef(ticket);
  ticketPropRef.current = ticket;
  /** Reset form fields from ticket only when opening the modal or switching to a different ticket (id), not when the same ticket object is replaced while editing. */
  const lastHydratedFormKeyRef = useRef<string | null>(null);
  const [pendingNewCommentsCount, setPendingNewCommentsCount] = useState(0);
  const authRoles = useMemo(
    () => normalizeRoles(currentUser?.roles, currentUser?.role),
    [currentUser?.roles, currentUser?.role],
  );
  const isCreateMode = !ticket.id;
  const canCreateTicket = isCreateMode && canCreateTickets(authRoles);
  const canUpdateTicket = Boolean(ticket.id) && canEditTickets(authRoles);
  const canSaveTicket = canCreateTicket || canUpdateTicket;
  const canDeleteTicket = Boolean(ticket.id) && canEditTickets(authRoles);
  const canArchiveTicket = Boolean(ticket.id) && canEditTickets(authRoles);
  /** Existing ticket: User/Guest cannot edit (API enforces Business Manager+). */
  const formReadOnly = Boolean(ticket.id) && !canUpdateTicket;
  const selectedBoard =
    ticketBoards.find((board) => board.id === boardId) ?? defaultBoard;
  const selectedBoardRequiresStoryPoints =
    selectedBoard?.requiresStoryPoints ?? false;
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
    lastTypingPingAtRef.current = 0;
    pendingLocalCommentRef.current = null;
  }, [isOpen, ticket.id, ticketBoards]);

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
      const outcome = await onSave(
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
        },
        queuedAttachments,
      );
      if (outcome !== "reloaded") {
        onClose();
      }
    } catch {
      // The parent save handler already surfaces the error to the user.
    } finally {
      setSaving(false);
    }
  }, [
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

  const reloadComments = useCallback(async () => {
    if (!ticket.id) return;

    const myVersion = ++commentsLoadVersion.current;
    setLoadingComments(true);

    try {
      const token = await getApiToken();
      const data = await commentService.getByTicket(ticket.id, token);

      if (commentsLoadVersion.current !== myVersion) return;

      setComments((current) => reconcileCommentsById(current, data));
    } finally {
      if (commentsLoadVersion.current === myVersion) {
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
    if (!isOpen || !ticket.id) return;
    void reloadComments();
  }, [isOpen, reloadComments, ticket.id]);

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
      prevSig
        ? prevSig.split(",").map((id) => Number(id))
        : [],
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
      const pending = pendingLocalCommentRef.current;

      if (pending) {
        const isExpired = Date.now() > pending.expiresAt;

        if (!isExpired) {
          if (
            latestRealtimeEvent.entityId === pending.id ||
            !latestRealtimeEvent.entityId
          ) {
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

      void reloadComments();
    }

    if (latestRealtimeEvent.eventType === "comment.typing") {
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

    const intervalId = window.setInterval(() => {
      const now = Date.now();
      setTypingUsers((current) =>
        current.filter((entry) => entry.expiresAt > now),
      );
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [isOpen, ticket.id]);

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
    el.scrollTop = el.scrollHeight;
    commentThreadNearBottomRef.current = true;
    setPendingNewCommentsCount(0);
  }, []);

  const handleCommentThreadScroll = useCallback(() => {
    const el = commentThreadScrollRef.current;
    if (!el) {
      return;
    }
    const distanceFromBottom =
      el.scrollHeight - el.scrollTop - el.clientHeight;
    const near = distanceFromBottom <= COMMENT_THREAD_NEAR_BOTTOM_PX;
    commentThreadNearBottomRef.current = near;
    if (near) {
      setPendingNewCommentsCount(0);
    }
  }, []);

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
      const outcome = await onSave(
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
      if (outcome === "reloaded") {
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
        ? `User #${ticket.createdBy}`
        : "") ||
    "Unknown User";
  const hasPersistedSla = Boolean(ticket.id);
  const canManageComments = Boolean(ticket.id);
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
      ? "Leave blank to use the matching routing rule."
      : undefined;
  const businessOwnerHelperText = ownerDirectoryError
    ? ownerDirectoryError
    : !ticket.id
      ? "Leave blank to use routing first, then default this ticket to you as the requester."
      : undefined;

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
            <span
              className={`mt-1 inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${slaBadgeClass}`}
              title={slaTooltip}
            >
              {slaDisplayLabel}
            </span>
          </div>
          <div title={slaTooltip}>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              SLA Deadline
            </p>
            <p className="mt-1 text-gray-800 dark:text-slate-200">
              {formatDisplayDateTime(ticket.slaTargetDate)}
            </p>
          </div>
          <div className="sm:col-span-2" title={slaTooltip}>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              SLA Tracking
            </p>
            <p className="mt-1 text-gray-700 dark:text-slate-300">
              {formatSlaSummary(ticket)}
            </p>
          </div>
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
    <div className="scroll-surface fixed inset-0 z-50 overflow-y-auto">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="flex min-h-full items-start justify-center p-3 sm:items-center sm:p-4">
        <div
          className="relative max-h-[calc(100dvh-1.5rem)] w-full max-w-5xl overflow-hidden rounded-lg border border-gray-200 bg-white p-4 text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100 sm:max-h-[calc(100dvh-2rem)] sm:p-6"
          tabIndex={-1}
        >
          <div
            className={`grid h-[calc(100dvh-6rem)] min-h-0 gap-6 ${
              canManageComments
                ? "grid-cols-1 lg:grid-cols-[minmax(0,1fr)_380px]"
                : "grid-cols-1"
            }`}
          >
            {/* ================= LEFT PANEL ================= */}
            <div className="flex min-h-0 min-w-0 flex-col">
              <div className="scroll-surface min-h-0 flex-1 space-y-6 overflow-y-auto pr-1">
              {/* Header */}
              <div className="flex items-start justify-between gap-3 border-b border-gray-200 pb-5 dark:border-slate-800">
                <div className="min-w-0 flex-1">
                  <label className="block text-lg font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Enter Ticket Title
                    {isCreateMode && (
                      <span className="ml-1 text-red-600 dark:text-red-400">
                        *
                      </span>
                    )}
                  </label>
                  <input
                    ref={titleInputRef}
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    readOnly={formReadOnly}
                    placeholder="Enter ticket title..."
                    className="w-full bg-transparent text-xl font-bold text-gray-900 dark:text-slate-100 mb-1 border-b border-gray-300 dark:border-slate-700 focus:border-cortex-blue focus:outline-none read-only:cursor-not-allowed read-only:opacity-80"
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
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                      <span className="rounded-full bg-cortex-blue-soft px-3 py-1 text-xs font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
                        {status}
                      </span>
                      <span
                        className={`rounded-full px-3 py-1 text-xs font-semibold ${priorityBadgeClass}`}
                      >
                        {priority}
                      </span>
                      <span
                        className={`rounded-full px-3 py-1 text-xs font-semibold ${slaBadgeClass}`}
                        title={slaTooltip}
                      >
                        {slaDisplayLabel}
                      </span>
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

              {/* Description */}
              <div className="rounded-md border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900/40">
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                  Description
                  {isCreateMode && (
                    <span className="ml-1 text-red-600 dark:text-red-400">
                      *
                    </span>
                  )}
                </label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  readOnly={formReadOnly}
                  rows={4}
                  placeholder="Enter ticket description..."
                  className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500 read-only:cursor-not-allowed read-only:opacity-80"
                />
                {isCreateMode && validationErrors.description && (
                  <p className="mt-2 text-xs text-red-600 dark:text-red-400">
                    {validationErrors.description}
                  </p>
                )}
              </div>

              {!ticket.id && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Routing Department
                  </label>
                  <input
                    type="text"
                    value={department}
                    onChange={(e) => setDepartment(e.target.value)}
                    placeholder="Defaults from your profile"
                    className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
                  />
                  <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                    Used with title and department routing rules when you leave
                    the owner fields blank.
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
                    className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
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
                      onChange={(e) => setStoryPoints(Number(e.target.value))}
                      className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
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
                      className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
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
                    users={ownerDirectory}
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
                    users={ownerDirectory}
                    onChange={setBusinessOwner}
                    loading={ownerDirectoryLoading}
                    disabled={ownerPickerDisabled}
                    helperText={businessOwnerHelperText}
                  />
                </div>
              </div>

              {ticket.id ? (
                <TicketRoutingInsight ticket={ticket} isModalOpen={isOpen} />
              ) : null}

              {ticket.id && (
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
                      Images, PDFs, Office documents, text, and CSV files are
                      supported.
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
                              onClick={() => void openAttachment(attachment)}
                              disabled={attachmentActionId === attachment.id}
                              className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                            >
                              Open
                            </button>
                            <button
                              onClick={() =>
                                void downloadAttachment(attachment)
                              }
                              disabled={attachmentActionId === attachment.id}
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
              </div>

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
                            Ticket details
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

            {/* ================= RIGHT PANEL ================= */}
            {canManageComments && (
              <div className="flex min-h-0 h-full flex-col rounded-md border border-gray-200 bg-gray-50/60 p-4 dark:border-slate-800 dark:bg-slate-900/30">
                <div className="mb-3 flex items-center justify-between border-b border-gray-200 pb-2 dark:border-slate-800">
                  <h3 className="text-sm font-semibold text-gray-700 dark:text-slate-300">
                    Comments
                  </h3>
                  <span className="rounded-full bg-gray-200 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-slate-800 dark:text-slate-300">
                    {comments.length}
                  </span>
                </div>

                <div
                  ref={commentThreadScrollRef}
                  onScroll={handleCommentThreadScroll}
                  className="scroll-surface relative min-h-0 flex-1 overflow-y-auto pr-1"
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
                </div>

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
            aria-hidden
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
                <span className="text-lg leading-none" aria-hidden>
                  ×
                </span>
              </button>
            </div>
            <div className="scroll-surface max-h-[min(70dvh,28rem)] overflow-y-auto p-4">
              {ticketDetailsBody}
            </div>
          </div>
        </div>
      )}

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
