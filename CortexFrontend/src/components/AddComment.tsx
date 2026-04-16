import { useState } from "react";

interface AddCommentProps {
  onAdd: (body: string) => Promise<void>;
  onTyping?: () => void;
  disabled?: boolean;
}

export default function AddComment({
  onAdd,
  onTyping,
  disabled = false,
}: AddCommentProps) {
  const [body, setBody] = useState("");
  const [posting, setPosting] = useState(false);

  const submit = async () => {
    if (disabled || posting || !body.trim()) return;

    try {
      setPosting(true);
      await onAdd(body.trim());
      setBody("");
    } catch {
      // The parent surfaces the error and we intentionally keep the draft text.
    } finally {
      setPosting(false);
    }
  };

  return (
    <div
      data-comment-composer="true"
      className="space-y-2 rounded-md border border-gray-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900/70"
    >
      <div className="flex items-center justify-between">
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
          Add Comment
        </p>
        {posting && (
          <span className="text-xs text-cortex-blue dark:text-cortex-cyan">
            Posting comment...
          </span>
        )}
      </div>
      <textarea
        value={body}
        onChange={(e) => {
          const nextBody = e.target.value;
          setBody(nextBody);
          if (nextBody.trim().length > 0 && !disabled && !posting) {
            onTyping?.();
          }
        }}
        rows={3}
        placeholder="Write a comment…"
        disabled={posting || disabled}
        className="w-full rounded-md border-gray-300 bg-white text-sm text-gray-900 shadow-sm disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            e.stopPropagation();
            submit();
          }
        }}
      />

      <div className="flex justify-end">
        <button
          onClick={submit}
          disabled={disabled || posting || !body.trim()}
          className="rounded-md bg-cortex-blue px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-cortex-blue-dark disabled:cursor-not-allowed disabled:opacity-50"
        >
          {posting ? "Posting..." : "Post Comment"}
        </button>
      </div>
    </div>
  );
}
