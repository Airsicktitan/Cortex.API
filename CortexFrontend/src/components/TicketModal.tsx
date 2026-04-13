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
import type { Ticket, TicketMutationInput } from "../types/ticket";
import type { TicketAttachment } from "../types/attachment";
import type { RealtimeEvent } from "../types/realtime";
import type { TicketBoardDefinition } from "../types/ticketBoard";
import type { TicketStatusDefinition } from "../types/ticketStatus";
import { commentService } from "../services/commentService";
import { attachmentService } from "../services/api";
import type { Comment } from "../types/comment";
import CommentList from "./CommentList";
import AddComment from "./AddComment";
import TicketHistoryModal from "./TicketHistoryModal";
import { useAuth0 } from "@auth0/auth0-react";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaBadgeClass,
} from "../utils/ticketSla";
import toast from "react-hot-toast";

const API_AUDIENCE = "https://cortex-api";
const ADMIN_PERMISSION = "admin:system";
const DEVELOPER_PERMISSION = "developer";
const TICKETS_CREATE_PERMISSION = "tickets:create";
const TICKETS_UPDATE_PERMISSION = "tickets:update";
const TICKETS_DELETE_PERMISSION = "tickets:delete";

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const payload = token.split(".")[1];
  if (!payload) return null;

  try {
    const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
    return JSON.parse(atob(padded)) as Record<string, unknown>;
  } catch (error) {
    console.error("Failed to decode token payload", error);
    return null;
  }
}

function parsePermissionsFromToken(token: string): string[] {
  const payload = decodeJwtPayload(token);
  const value = payload?.permissions;

  if (Array.isArray(value)) {
    return value.filter(
      (permission): permission is string => typeof permission === "string",
    );
  }

  if (typeof value === "string" && value.trim()) {
    return [value];
  }

  return [];
}

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
  ) => Promise<void>;
  onArchive: (ticket: Ticket, changeReason?: string) => Promise<void>;
  onDelete: (ticket: Ticket) => void;
  currentUser: {
    displayName: string;
    department?: string;
    role?: string;
  } | null;
  createdByDisplayName: string;
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
}: TicketModalProps) {
  const defaultBoard =
    ticketBoards.find((board) => board.id === ticket.boardId) ??
    ticketBoards.find((board) => board.name === "Ticket") ??
    ticketBoards[0];
  const [priority, setPriority] = useState(ticket.priority);
  const [status, setStatus] = useState(ticket.status);
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
  const [permissions, setPermissions] = useState<string[]>([]);
  const [isActionMenuOpen, setIsActionMenuOpen] = useState(false);
  const titleInputRef = useRef<HTMLInputElement | null>(null);
  const actionMenuRef = useRef<HTMLDivElement | null>(null);

  const { getAccessTokenSilently } = useAuth0();

  // Used to prevent older comment fetches from overwriting newer ones
  const commentsLoadVersion = useRef(0);
  const attachmentsLoadVersion = useRef(0);
  const hasPermission = (permission: string) => {
    return (
      permissions.includes(permission) || permissions.includes(ADMIN_PERMISSION)
    );
  };
  const canCreateTicket =
    !ticket.id && hasPermission(TICKETS_CREATE_PERMISSION);
  const canUpdateTicket =
    Boolean(ticket.id) && hasPermission(TICKETS_UPDATE_PERMISSION);
  const canSaveTicket = canCreateTicket || canUpdateTicket;
  const canDeleteTicket =
    Boolean(ticket.id) && hasPermission(TICKETS_DELETE_PERMISSION);
  const canArchiveTicket =
    Boolean(ticket.id) &&
    canUpdateTicket &&
    (permissions.includes(ADMIN_PERMISSION) ||
      permissions.includes(DEVELOPER_PERMISSION) ||
      currentUser?.role === "Admin" ||
      currentUser?.role === "Developer");
  const selectedBoard =
    ticketBoards.find((board) => board.id === boardId) ?? defaultBoard;
  const selectedBoardRequiresStoryPoints =
    selectedBoard?.requiresStoryPoints ?? false;
  const quickMoveBoards = useMemo(
    () =>
      ticketBoards.filter(
        (board) => board.isEnabled && Boolean(ticket.id) && board.id !== boardId,
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
  useLayoutEffect(() => {
    if (!isOpen) return;

    setTitle(ticket.title || "");
    setDescription(ticket.description || "");
    setPriority(ticket.priority);
    setStatus(ticket.status);
    setDepartment(ticket.department || currentUser?.department || "");
    setBoardId(defaultBoard?.id ?? 0);
    setStoryPoints(ticket.storyPoints ?? (defaultBoard?.requiresStoryPoints ? 1 : ""));
    setSynitiOwner(ticket.synitiOwner || "");
    setBusinessOwner(ticket.businessOwner || "");
    setChangeReason("");
    setQueuedAttachments([]);
    setIsAttachmentDropActive(false);
    setIsHistoryModalOpen(false);
    setIsActionMenuOpen(false);
  }, [currentUser?.department, defaultBoard, ticket, isOpen]);

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
    setSaving(true);
    try {
      await onSave(
        {
          title,
          description,
          priority,
          status,
          department: !ticket.id ? department.trim() || undefined : undefined,
          boardId,
          storyPoints:
            selectedBoardRequiresStoryPoints && storyPoints !== ""
              ? Number(storyPoints)
              : undefined,
          synitiOwner: synitiOwner || undefined,
          businessOwner: businessOwner || undefined,
          changeReason: ticket.id ? changeReason.trim() || undefined : undefined,
        },
        queuedAttachments,
      );
      onClose();
    } catch {
      // The parent save handler already surfaces the error to the user.
    } finally {
      setSaving(false);
    }
  }, [
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
    queuedAttachments,
  ]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (isHistoryModalOpen) {
        return;
      }

      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }

      if (e.key === "Enter" && !e.shiftKey) {
        const active = document.activeElement;
        if (active?.tagName === "TEXTAREA") return;
        if (
          saving ||
          !title.trim() ||
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
    handleSave,
    isHistoryModalOpen,
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

      setComments(data);
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
        toast.error("Failed to load attachments");
      }
    } finally {
      if (attachmentsLoadVersion.current === myVersion) {
        setLoadingAttachments(false);
      }
    }
  }, [getApiToken, ticket.id]);

  useEffect(() => {
    if (!isOpen || !ticket.id) return;
    void reloadComments();
  }, [isOpen, reloadComments, ticket.id]);

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
      void reloadComments();
    }

    if (latestRealtimeEvent.eventType === "attachment.created") {
      void reloadAttachments();
    }
  }, [
    isOpen,
    latestRealtimeEvent,
    reloadAttachments,
    reloadComments,
    ticket.id,
  ]);

  useEffect(() => {
    const loadPermissions = async () => {
      try {
        const token = await getApiToken();
        setPermissions(parsePermissionsFromToken(token));
      } catch (err) {
        console.error("Failed to load permissions", err);
      }
    };

    void loadPermissions();
  }, [getApiToken]);

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

  if (!isOpen) return null;

  const handleBoardSelectionChange = (nextBoardId: number) => {
    const nextBoard = ticketBoards.find((board) => board.id === nextBoardId);
    setBoardId(nextBoardId);

    if (nextBoard?.requiresStoryPoints) {
      setStoryPoints((currentValue) => (currentValue === "" ? 1 : currentValue));
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
      await onSave(
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
        },
        queuedAttachments,
      );
      onClose();
      toast.success(`Moved ticket to ${targetBoard.name}`);
    } catch (error) {
      console.error("Failed to move ticket to board", error);
      toast.error(`Failed to move ticket to ${targetBoard.name}`);
    } finally {
      setSaving(false);
    }
  };

  const addComment = async (body: string) => {
    if (!ticket.id) return;

    try {
      const token = await getApiToken();
      await commentService.create(ticket.id, body, token);
      await reloadComments();
    } catch (error) {
      console.error("Failed to add comment", error);
      toast.error(
        error instanceof Error && error.message.trim()
          ? error.message
          : "Failed to add comment",
      );
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
      toast.error("Failed to open attachment");
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
      toast.error("Failed to download attachment");
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
  const slaTooltip = buildSlaTooltip(ticket);
  const slaBadgeClass = getSlaBadgeClass(ticket.slaStatus);

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="flex min-h-full items-center justify-center p-4">
        <div
          className="relative bg-white dark:bg-slate-900 text-gray-900 dark:text-slate-100 rounded-lg shadow-xl border border-gray-200 dark:border-slate-800 max-w-5xl w-full p-6"
          tabIndex={-1}
        >
          <div
            className={`grid gap-6 ${
              canManageComments ? "grid-cols-[1fr_380px]" : "grid-cols-1"
            }`}
          >
            {/* ================= LEFT PANEL ================= */}
            <div className="min-w-0">
              {/* Header */}
              <div className="flex items-start justify-between mb-6">
                <div className="flex-1">
                  <label className="block text-lg font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Enter Ticket Title
                  </label>
                  <input
                    ref={titleInputRef}
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="Enter ticket title..."
                    className="w-full bg-transparent text-xl font-bold text-gray-900 dark:text-slate-100 mb-1 border-b border-gray-300 dark:border-slate-700 focus:border-cortex-blue focus:outline-none"
                  />
                  <p className="text-sm text-gray-500 dark:text-slate-400">
                    {ticket.id}
                  </p>
                </div>
                <button
                  onClick={onClose}
                  className="text-gray-400 hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300 text-2xl font-bold"
                >
                  ×
                </button>
              </div>

              {/* Description */}
              <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                  Description
                </label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={4}
                  placeholder="Enter ticket description..."
                  className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
                />
              </div>

              {!ticket.id && (
                <div className="mb-6">
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
              <div className="grid grid-cols-2 gap-4 mb-6">
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Board
                  </label>
                  <select
                    value={boardId}
                    onChange={(e) => handleBoardSelectionChange(Number(e.target.value))}
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
                </div>

                {/* Status */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Status
                  </label>
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
                </div>

                {/* Syniti Owner */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Syniti Owner
                  </label>
                  <input
                    type="text"
                    value={synitiOwner}
                    onChange={(e) => setSynitiOwner(e.target.value)}
                    className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                  />
                  {!ticket.id && (
                    <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                      Leave blank to use the matching routing rule.
                    </p>
                  )}
                </div>

                {/* Business Owner */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-slate-300 mb-2">
                    Business Owner
                  </label>
                  <input
                    type="text"
                    value={businessOwner}
                    onChange={(e) => setBusinessOwner(e.target.value)}
                    className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                  />
                  {!ticket.id && (
                    <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
                      Leave blank to use routing first, then default this ticket
                      to you as the requester.
                    </p>
                  )}
                </div>
              </div>

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

              <div className="mb-6 rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-800/60">
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

              {/* Metadata */}
              <div className="bg-gray-50 dark:bg-slate-800/70 p-4 rounded-md mb-6">
                <div className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span className="font-medium">Created By:</span>{" "}
                    {createdByName}
                  </div>
                  <div>
                    <span className="font-medium">Created Date:</span>{" "}
                    {new Date(ticket.createdDate).toLocaleDateString()}
                  </div>
                  <div>
                    <span className="font-medium">Board:</span>{" "}
                    {selectedBoard?.name ?? ticket.boardName ?? "Ticket"}
                  </div>
                  {selectedBoardRequiresStoryPoints && (
                    <div>
                      <span className="font-medium">Story Points:</span>{" "}
                      {storyPoints === "" ? "—" : storyPoints}
                    </div>
                  )}
                  {hasPersistedSla ? (
                    <>
                      <div>
                        <span className="font-medium">SLA Status:</span>{" "}
                        <span
                          className={`inline-flex px-2 py-1 rounded-full text-xs font-medium ${slaBadgeClass}`}
                          title={slaTooltip}
                        >
                          {ticket.slaStatus}
                        </span>
                      </div>
                      <div title={slaTooltip}>
                        <span className="font-medium">SLA Deadline:</span>{" "}
                        {new Date(ticket.slaTargetDate).toLocaleString()}
                      </div>
                      <div title={slaTooltip}>
                        <span className="font-medium">SLA Tracking:</span>{" "}
                        {formatSlaSummary(ticket)}
                      </div>
                      {ticket.slaCompletedDate && (
                        <div title={slaTooltip}>
                          <span className="font-medium">SLA Completed:</span>{" "}
                          {new Date(ticket.slaCompletedDate).toLocaleString()}
                        </div>
                      )}
                    </>
                  ) : (
                    <div className="col-span-2 text-gray-600 dark:text-slate-400">
                      SLA timing will be calculated after the ticket is created
                      using the selected priority settings.
                    </div>
                  )}
                  {!canManageComments && (
                    <div className="col-span-2 text-gray-600 dark:text-slate-400">
                      Comments will be available after the ticket is created.
                    </div>
                  )}
                </div>
              </div>

              {/* Actions */}
              <div className="flex justify-between items-center">
                <div className="flex items-center gap-3">
                  {ticket.id && (
                    <button
                      onClick={() => setIsHistoryModalOpen(true)}
                      disabled={saving || archiving}
                      className="px-4 py-2 rounded-md border border-gray-300 text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    >
                      History
                    </button>
                  )}

                  {ticket.id &&
                    (quickMoveBoards.length > 0 ||
                      canArchiveTicket ||
                      canDeleteTicket) && (
                      <div ref={actionMenuRef} className="relative">
                        <button
                          type="button"
                          onClick={() =>
                            setIsActionMenuOpen((current) => !current)
                          }
                          disabled={saving || archiving}
                          className="rounded-md border border-gray-300 px-4 py-2 text-gray-700 transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                        >
                          Actions ▾
                        </button>

                        {isActionMenuOpen && (
                          <div className="absolute left-0 top-full z-20 mt-2 w-64 overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
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
                          </div>
                        )}
                      </div>
                    )}
                </div>

                <div className="flex space-x-3">
                  <button
                    onClick={onClose}
                    disabled={archiving}
                    className="rounded-md bg-gray-200 px-4 py-2 text-gray-800 transition-colors hover:bg-gray-300 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
                  >
                    Cancel
                  </button>
                  {canSaveTicket && (
                    <button
                      onClick={handleSave}
                      disabled={
                        saving ||
                        archiving ||
                        !title.trim() ||
                        (selectedBoardRequiresStoryPoints && storyPoints === "")
                      }
                      className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-60"
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
              <div className="border-l border-gray-200 dark:border-slate-800 pl-4 flex flex-col min-h-[500px]">
                <h3 className="text-sm font-semibold text-gray-700 dark:text-slate-300 mb-2">
                  Comments
                </h3>

                <div className="flex-1 overflow-y-auto pr-1">
                  {loadingComments ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      Loading comments…
                    </p>
                  ) : (
                    <CommentList comments={comments} />
                  )}
                </div>

                <div className="mt-3">
                  <AddComment onAdd={addComment} />
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

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
