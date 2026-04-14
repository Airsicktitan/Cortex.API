import type { Comment } from "../types/comment";

interface Props {
  comments: Comment[];
}

export default function CommentList({ comments }: Props) {
  if (comments.length === 0) {
    return (
      <p className="text-sm text-gray-500 dark:text-slate-400 italic">
        No comments yet
      </p>
    );
  }

  return (
    <div className="space-y-3">
      {comments.map((c) => (
        <div
          key={c.id}
          className="rounded-md border border-gray-200 bg-white p-3 shadow-sm dark:border-slate-700 dark:bg-slate-900/60"
        >
          <div className="mb-2 flex flex-col gap-1 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between sm:gap-2">
            <p className="min-w-0 truncate text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-slate-400">
              {c.createdByDisplayName ?? c.createdBy}
            </p>
            <p className="shrink-0 text-xs text-gray-400 dark:text-slate-500">
              {new Date(c.createdDate).toLocaleString()}
            </p>
          </div>

          <div className="whitespace-pre-wrap break-words text-sm leading-6 text-gray-700 dark:text-slate-200">
            {c.body}
          </div>
        </div>
      ))}
    </div>
  );
}
