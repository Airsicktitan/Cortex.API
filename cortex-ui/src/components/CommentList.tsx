import type { Comment } from "../types/comment";

interface Props {
  comments: Comment[];
}

export default function CommentList({ comments }: Props) {
  if (comments.length === 0) {
    return <p className="text-sm text-gray-500 italic">No comments yet</p>;
  }

  return (
    <div className="space-y-4">
      {comments.map((c) => (
        <div key={c.id} className="bg-gray-50 p-3 rounded-md">
          <div className="text-sm text-gray-700 whitespace-pre-wrap">
            {c.body}
          </div>

          <div className="mt-2 text-xs text-gray-400">
            {c.createdByUser?.displayName ?? c.createdBy} ·{" "}
            {new Date(c.createdDate).toLocaleString()}
          </div>
        </div>
      ))}
    </div>
  );
}
