import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import type { Ticket } from "../types/ticket";
import { commentService } from "../services/commentService";
import type { Comment } from "../types/comment";
import CommentList from "./CommentList";
import AddComment from "./AddComment";
import { useAuth0 } from "@auth0/auth0-react";
import {
  buildSlaTooltip,
  formatSlaSummary,
  getSlaBadgeClass,
} from "../utils/ticketSla";

const API_AUDIENCE = "https://cortex-api";
const ADMIN_PERMISSION = "admin:system";
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

interface TicketModalProps {
  ticket: Ticket;
  isOpen: boolean;
  onClose: () => void;
  onSave: (updatedTicket: Partial<Ticket>) => Promise<void>;
  onDelete: (ticket: Ticket) => void;
  currentUser: {
    displayName: string;
  } | null;
  createdByDisplayName: string;
}

export default function TicketModal({
  ticket,
  isOpen,
  onClose,
  onSave,
  onDelete,
  currentUser,
}: TicketModalProps) {
  const [priority, setPriority] = useState(ticket.priority);
  const [status, setStatus] = useState(ticket.status);
  const [synitiOwner, setSynitiOwner] = useState(ticket.synitiOwner || "");
  const [businessOwner, setBusinessOwner] = useState(
    ticket.businessOwner || "",
  );
  const [saving, setSaving] = useState(false);
  const [description, setDescription] = useState(ticket.description || "");
  const [title, setTitle] = useState(ticket.title || "");

  const [comments, setComments] = useState<Comment[]>([]);
  const [loadingComments, setLoadingComments] = useState(false);
  const [permissions, setPermissions] = useState<string[]>([]);

  const { getAccessTokenSilently } = useAuth0();

  // Used to prevent older comment fetches from overwriting newer ones
  const commentsLoadVersion = useRef(0);
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

  // ✅ CRITICAL: useLayoutEffect prevents the “1 frame of old ticket data”
  useLayoutEffect(() => {
    if (!isOpen) return;

    setTitle(ticket.title || "");
    setDescription(ticket.description || "");
    setPriority(ticket.priority);
    setStatus(ticket.status);
    setSynitiOwner(ticket.synitiOwner || "");
    setBusinessOwner(ticket.businessOwner || "");
  }, [ticket, isOpen]);

  const handleSave = useCallback(async () => {
    setSaving(true);
    try {
      await onSave({
        title,
        description,
        priority,
        status,
        synitiOwner: synitiOwner || undefined,
        businessOwner: businessOwner || undefined,
      });
      onClose();
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
    synitiOwner,
    businessOwner,
  ]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }

      if (e.key === "Enter" && !e.shiftKey) {
        const active = document.activeElement;
        if (active?.tagName === "TEXTAREA") return;
        if (saving || !title.trim() || !canSaveTicket) return;

        e.preventDefault();
        handleSave();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [canSaveTicket, isOpen, saving, title, onClose, handleSave]);

  // ✅ CRITICAL: clear comments + show loading BEFORE paint, and guard response ordering
  useLayoutEffect(() => {
    if (!isOpen) return;

    // Always reset the right panel instantly on ticket change
    setComments([]);
    setLoadingComments(!!ticket.id);

    // bump version so older requests can't win
    commentsLoadVersion.current += 1;
  }, [isOpen, ticket.id]);

  useEffect(() => {
    if (!isOpen || !ticket.id) return;

    const myVersion = commentsLoadVersion.current;

    const load = async () => {
      try {
        const token = await getAccessTokenSilently();
        const data = await commentService.getByTicket(ticket.id, token);

        // If something newer started, ignore this result
        if (commentsLoadVersion.current !== myVersion) return;

        setComments(data);
      } finally {
        if (commentsLoadVersion.current === myVersion) {
          setLoadingComments(false);
        }
      }
    };

    load();
  }, [getAccessTokenSilently, isOpen, ticket.id]);

  useEffect(() => {
    const loadPermissions = async () => {
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: {
            audience: API_AUDIENCE,
          },
        });

        setPermissions(parsePermissionsFromToken(token));
      } catch (err) {
        console.error("Failed to load permissions", err);
      }
    };

    loadPermissions();
  }, [getAccessTokenSilently]);

  if (!isOpen) return null;

  const handleDelete = () => {
    // (Keeping your current behavior)
    onClose();
    onDelete(ticket);
  };

  const addComment = async (body: string) => {
    if (!ticket.id) return;

    const token = await getAccessTokenSilently();

    const created = await commentService.create(ticket.id, body, token);
    setComments((prev) => [...prev, created]);
  };

  const createdByName =
    ticket.createdByDisplayName ||
    ticket.createdByUser?.displayName ||
    currentUser?.displayName ||
    ticket.createdBy ||
    "Created By API";
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
                  <input
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="Enter ticket title..."
                    className="w-full bg-transparent text-2xl font-bold text-gray-900 dark:text-slate-100 mb-1 border-b border-gray-300 dark:border-slate-700 focus:border-cortex-blue focus:outline-none"
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

              {/* Editable Fields */}
              <div className="grid grid-cols-2 gap-4 mb-6">
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
                    <option value="New">New</option>
                    <option value="In Progress">In Progress</option>
                    <option value="Pending Business Review">
                      Pending Business Review
                    </option>
                    <option value="Resolved">Resolved</option>
                    <option value="Closed">Closed</option>
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
                {ticket.id && hasPermission(TICKETS_DELETE_PERMISSION) && (
                  <button
                    onClick={handleDelete}
                    disabled={saving}
                    className="px-4 py-2 bg-red-600 text-white rounded-md"
                  >
                    Delete
                  </button>
                )}

                <div className="flex space-x-3">
                  <button
                    onClick={onClose}
                    className="px-4 py-2 bg-gray-200 text-gray-800 rounded-md dark:bg-slate-800 dark:text-slate-200"
                  >
                    Cancel
                  </button>
                  {canSaveTicket && (
                    <button
                      onClick={handleSave}
                      disabled={saving || !title.trim()}
                      className="px-4 py-2 bg-cortex-blue text-white rounded-md"
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
    </div>
  );
}
