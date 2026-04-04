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
        if (saving || !title.trim()) return;

        e.preventDefault();
        handleSave();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, saving, title, onClose, handleSave]);

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
            audience: "https://cortex-api",
          },
        });

        const payload = JSON.parse(atob(token.split(".")[1]));

        const normalize = (v: unknown) => (Array.isArray(v) ? v : v ? [v] : []);

        setPermissions(normalize(payload.permissions));
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

  const hasPermission = (permission: string) => {
    return (
      permissions.includes(permission) || permissions.includes("admin:system")
    );
  };

  const createdByName =
    ticket.createdByDisplayName ||
    ticket.createdByUser?.displayName ||
    currentUser?.displayName ||
    ticket.createdBy ||
    "Created By API";

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
          className="relative bg-white rounded-lg shadow-xl max-w-5xl w-full p-6"
          tabIndex={-1}
        >
          <div className="grid grid-cols-[1fr_380px] gap-6">
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
                    className="w-full text-2xl font-bold text-gray-900 mb-1 border-b border-gray-300 focus:border-cortex-blue focus:outline-none"
                  />
                  <p className="text-sm text-gray-500">{ticket.id}</p>
                </div>
                <button
                  onClick={onClose}
                  className="text-gray-400 hover:text-gray-600 text-2xl font-bold"
                >
                  ×
                </button>
              </div>

              {/* Description */}
              <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Description
                </label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={4}
                  placeholder="Enter ticket description..."
                  className="w-full rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
                />
              </div>

              {/* Editable Fields */}
              <div className="grid grid-cols-2 gap-4 mb-6">
                {/* Priority */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Priority
                  </label>
                  <select
                    value={priority}
                    onChange={(e) => setPriority(e.target.value)}
                    className="w-full rounded-md border-gray-300 shadow-sm"
                  >
                    <option value="Critical">Critical</option>
                    <option value="High">High</option>
                    <option value="Medium">Medium</option>
                    <option value="Low">Low</option>
                  </select>
                </div>

                {/* Status */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Status
                  </label>
                  <select
                    value={status}
                    onChange={(e) => setStatus(e.target.value)}
                    className="w-full rounded-md border-gray-300 shadow-sm"
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
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Syniti Owner
                  </label>
                  <input
                    type="text"
                    value={synitiOwner}
                    onChange={(e) => setSynitiOwner(e.target.value)}
                    className="w-full rounded-md border-gray-300 shadow-sm"
                  />
                </div>

                {/* Business Owner */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Business Owner
                  </label>
                  <input
                    type="text"
                    value={businessOwner}
                    onChange={(e) => setBusinessOwner(e.target.value)}
                    className="w-full rounded-md border-gray-300 shadow-sm"
                  />
                </div>
              </div>

              {/* Metadata */}
              <div className="bg-gray-50 p-4 rounded-md mb-6">
                <div className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span className="font-medium">Created By:</span>{" "}
                    {createdByName}
                  </div>
                  <div>
                    <span className="font-medium">Created Date:</span>{" "}
                    {new Date(ticket.createdDate).toLocaleDateString()}
                  </div>
                </div>
              </div>

              {/* Actions */}
              <div className="flex justify-between items-center">
                {ticket.id && hasPermission("tickets:delete") && (
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
                    className="px-4 py-2 bg-gray-200 rounded-md"
                  >
                    Cancel
                  </button>
                  {hasPermission("tickets:update") && (
                    <button
                      onClick={handleSave}
                      disabled={saving || !title.trim()}
                      className="px-4 py-2 bg-cortex-blue text-white rounded-md"
                    >
                      {saving ? "Saving..." : "Save Changes"}
                    </button>
                  )}
                </div>
              </div>
            </div>

            {/* ================= RIGHT PANEL ================= */}
            <div className="border-l pl-4 flex flex-col min-h-[500px]">
              <h3 className="text-sm font-semibold text-gray-700 mb-2">
                Comments
              </h3>

              <div className="flex-1 overflow-y-auto pr-1">
                {loadingComments ? (
                  <p className="text-sm text-gray-500">Loading comments…</p>
                ) : (
                  <CommentList comments={comments} />
                )}
              </div>

              <div className="mt-3">
                <AddComment onAdd={addComment} />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
