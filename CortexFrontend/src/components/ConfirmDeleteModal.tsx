interface ConfirmDeleteModalProps {
  isOpen: boolean;
  title?: string;
  message?: string;
  onCancel: () => void;
  onConfirm: () => void;
  loading?: boolean;
}

export default function ConfirmDeleteModal({
  isOpen,
  title = "Delete Ticket",
  message = "Are you sure you want to delete this ticket? This action cannot be undone.",
  onCancel,
  onConfirm,
  loading = false,
}: ConfirmDeleteModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-black bg-opacity-50 flex items-center justify-center px-4">
      <div className="bg-white dark:bg-slate-900 text-gray-900 dark:text-slate-100 rounded-lg shadow-xl border border-gray-200 dark:border-slate-800 max-w-md w-full p-6">
        <h2 className="text-xl font-bold mb-3">{title}</h2>
        <p className="mb-6 text-gray-600 dark:text-slate-400">{message}</p>

        <div className="flex justify-end space-x-3">
          <button
            onClick={onCancel}
            className="px-4 py-2 rounded-md bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            className="bg-red-600 text-white px-4 py-2 rounded"
          >
            {loading ? "Deleting…" : "Delete"}
          </button>
        </div>
      </div>
    </div>
  );
}
