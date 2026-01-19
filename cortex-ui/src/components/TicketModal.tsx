import { useState } from "react";
import type { Ticket } from "../types/ticket";

interface TicketModalProps {
  ticket: Ticket;
  isOpen: boolean;
  onClose: () => void;
  onSave: (updatedTicket: Partial<Ticket>) => Promise<void>;
}

export default function TicketModal({
  ticket,
  isOpen,
  onClose,
  onSave,
}: TicketModalProps) {
  const [priority, setPriority] = useState(ticket.priority);
  const [status, setStatus] = useState(ticket.status);
  const [synitiOwner, setSynitiOwner] = useState(ticket.synitiOwner || "");
  const [businessOwner, setBusinessOwner] = useState(
    ticket.businessOwner || "",
  );
  const [saving, setSaving] = useState(false);

  if (!isOpen) return null;

  const handleSave = async () => {
    setSaving(true);
    try {
      await onSave({
        title: ticket.title,
        description: ticket.description,
        priority,
        status,
        synitiOwner: synitiOwner || undefined,
        businessOwner: businessOwner || undefined,
      });
      onClose();
    } catch (error) {
      console.error("Failed to save:", error);
      alert("Failed to save changes. Please try again.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="flex min-h-full items-center justify-center p-4">
        <div className="relative bg-white rounded-lg shadow-xl max-w-2xl w-full p-6">
          {/* Header */}
          <div className="flex items-start justify-between mb-6">
            <div className="flex-1">
              <h2 className="text-2xl font-bold text-gray-900 mb-1">
                {ticket.title}
              </h2>
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
            <p className="text-gray-600 bg-gray-50 p-3 rounded-md">
              {ticket.description}
            </p>
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
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
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
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
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
                placeholder="Enter owner name..."
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
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
                placeholder="Enter owner name..."
                className="w-full rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
              />
            </div>
          </div>

          {/* Metadata */}
          <div className="bg-gray-50 p-4 rounded-md mb-6">
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="font-medium text-gray-700">Created By:</span>
                <span className="ml-2 text-gray-600">{ticket.createdBy}</span>
              </div>
              <div>
                <span className="font-medium text-gray-700">Created Date:</span>
                <span className="ml-2 text-gray-600">
                  {new Date(ticket.createdDate).toLocaleDateString()}
                </span>
              </div>
              {ticket.lastModifiedBy && (
                <div>
                  <span className="font-medium text-gray-700">
                    Last Modified By:
                  </span>
                  <span className="ml-2 text-gray-600">
                    {ticket.lastModifiedBy}
                  </span>
                </div>
              )}
              {ticket.lastModifiedDate && (
                <div>
                  <span className="font-medium text-gray-700">
                    Last Modified:
                  </span>
                  <span className="ml-2 text-gray-600">
                    {new Date(ticket.lastModifiedDate).toLocaleDateString()}
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* Actions */}
          <div className="flex justify-end space-x-3">
            <button
              onClick={onClose}
              className="px-4 py-2 bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300 transition-colors"
              disabled={saving}
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              disabled={saving}
              className="px-4 py-2 bg-cortex-blue text-white rounded-md hover:bg-blue-700 transition-colors disabled:opacity-50"
            >
              {saving ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
