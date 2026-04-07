import { useState } from "react";

interface AddCommentProps {
  onAdd: (body: string) => Promise<void>;
}

export default function AddComment({ onAdd }: AddCommentProps) {
  const [body, setBody] = useState("");
  const [posting, setPosting] = useState(false);

  const submit = async () => {
    if (!body.trim()) return;

    try {
      setPosting(true);
      await onAdd(body.trim());
      setBody("");
    } finally {
      setPosting(false);
    }
  };

  return (
    <div className="space-y-2">
      <textarea
        value={body}
        onChange={(e) => setBody(e.target.value)}
        rows={3}
        placeholder="Write a comment…"
        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm text-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            submit();
          }
        }}
      />

      <div className="flex justify-end">
        <button
          onClick={submit}
          disabled={posting || !body.trim()}
          className="px-3 py-1.5 bg-blue-600 text-white rounded-md text-sm disabled:opacity-50"
        >
          {posting ? "Posting…" : "Post"}
        </button>
      </div>
    </div>
  );
}
