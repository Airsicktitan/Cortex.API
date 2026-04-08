import type { FormEvent } from "react";

interface SaveTicketFilterModalProps {
  isOpen: boolean;
  name: string;
  onNameChange: (value: string) => void;
  onClose: () => void;
  onSave: () => void;
}

export default function SaveTicketFilterModal({
  isOpen,
  name,
  onNameChange,
  onClose,
  onSave,
}: SaveTicketFilterModalProps) {
  if (!isOpen) {
    return null;
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!name.trim()) {
      return;
    }

    onSave();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div
        className="fixed inset-0 bg-black/50"
        onClick={onClose}
      />

      <form
        onSubmit={handleSubmit}
        className="relative z-10 w-full max-w-md rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-slate-800 dark:bg-slate-900"
      >
        <h2 className="text-xl font-semibold text-gray-900 dark:text-slate-100">
          Save Filter
        </h2>
        <p className="mt-2 text-sm text-gray-500 dark:text-slate-400">
          Give this ticket view a name so you can reuse it later.
        </p>

        <div className="mt-4">
          <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
            Filter Name
          </label>
          <input
            type="text"
            value={name}
            onChange={(event) => onNameChange(event.target.value)}
            placeholder="My Ticket View"
            autoFocus
            className="mt-2 w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
          />
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-800 transition-colors hover:bg-gray-300 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={!name.trim()}
            className="rounded-md bg-cortex-blue px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            Save Filter
          </button>
        </div>
      </form>
    </div>
  );
}
