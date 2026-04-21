import { useAuth0 } from "@auth0/auth0-react";
import { useCallback, useEffect, useRef, useState } from "react";
import { attachmentService, getUserFacingErrorMessage } from "../services/api";
import { commentService } from "../services/commentService";
import type { TicketAttachment } from "../types/attachment";
import type { ArchivedTicket } from "../types/archivedTicket";
import type { Comment } from "../types/comment";
import CommentList from "./CommentList";
import {
  formatDisplayDateTime,
  formatDisplayValue,
  formatTicketIdentifier,
} from "../utils/presentation";
import {
  readOnlyBusinessOwnerLabel,
  readOnlySynitiOwnerLabel,
} from "../utils/ownerIdentity";
import { ScrollToBottomButton } from "./ui/ScrollToBottomButton";

type ArchivedTicketModalProps = {
  ticket: ArchivedTicket | null;
  onClose: () => void;
};

const API_AUDIENCE = "https://cortex-api";

function formatFileSize(size: number) {
  if (size < 1024) {
    return `${size} B`;
  }
  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }
  return `${(size / (1024 * 1024)).toFixed(1)} MB`;
}

export default function ArchivedTicketModal({
  ticket,
  onClose,
}: ArchivedTicketModalProps) {
  const { getAccessTokenSilently } = useAuth0();
  const [comments, setComments] = useState<Comment[]>([]);
  const [attachments, setAttachments] = useState<TicketAttachment[]>([]);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingAttachments, setLoadingAttachments] = useState(false);
  const [commentsError, setCommentsError] = useState<string | null>(null);
  const [attachmentsError, setAttachmentsError] = useState<string | null>(null);
  const [attachmentActionId, setAttachmentActionId] = useState<number | null>(null);
  const archivedDetailsScrollRef = useRef<HTMLDivElement | null>(null);
  const archivedCommentsScrollRef = useRef<HTMLDivElement | null>(null);

  const loadComments = useCallback(async () => {
    if (!ticket?.id) return;
    setLoadingComments(true);
    setCommentsError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      setComments(await commentService.getByArchivedTicket(ticket.id, token));
    } catch (error) {
      setCommentsError(
        getUserFacingErrorMessage(error, "Unable to load archived comments."),
      );
    } finally {
      setLoadingComments(false);
    }
  }, [getAccessTokenSilently, ticket?.id]);

  const loadAttachments = useCallback(async () => {
    if (!ticket?.id) return;
    setLoadingAttachments(true);
    setAttachmentsError(null);
    try {
      const token = await getAccessTokenSilently({
        authorizationParams: { audience: API_AUDIENCE },
      });
      setAttachments(await attachmentService.getByArchivedTicket(ticket.id, token));
    } catch (error) {
      setAttachmentsError(
        getUserFacingErrorMessage(error, "Unable to load archived attachments."),
      );
    } finally {
      setLoadingAttachments(false);
    }
  }, [getAccessTokenSilently, ticket?.id]);

  const openAttachment = useCallback(
    async (attachment: TicketAttachment) => {
      if (!ticket?.id) return;
      const popup = window.open("", "_blank", "noopener,noreferrer");
      if (!popup) return;
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        setAttachmentActionId(attachment.id);
        const blob = await attachmentService.downloadArchived(
          ticket.id,
          attachment.id,
          token,
        );
        const url = URL.createObjectURL(blob);
        popup.location.href = url;
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } catch (error) {
        popup.close();
        setAttachmentsError(
          getUserFacingErrorMessage(error, "Unable to open attachment."),
        );
      } finally {
        setAttachmentActionId((current) =>
          current === attachment.id ? null : current,
        );
      }
    },
    [getAccessTokenSilently, ticket?.id],
  );

  const downloadAttachment = useCallback(
    async (attachment: TicketAttachment) => {
      if (!ticket?.id) return;
      try {
        const token = await getAccessTokenSilently({
          authorizationParams: { audience: API_AUDIENCE },
        });
        setAttachmentActionId(attachment.id);
        const blob = await attachmentService.downloadArchived(
          ticket.id,
          attachment.id,
          token,
        );
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = attachment.fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
      } catch (error) {
        setAttachmentsError(
          getUserFacingErrorMessage(error, "Unable to download attachment."),
        );
      } finally {
        setAttachmentActionId((current) =>
          current === attachment.id ? null : current,
        );
      }
    },
    [getAccessTokenSilently, ticket?.id],
  );

  useEffect(() => {
    if (!ticket) return;
    void loadComments();
    void loadAttachments();
  }, [loadAttachments, loadComments, ticket]);

  if (!ticket) return null;

  return (
    <div className="scroll-surface fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
        onClick={onClose}
      />

      <div className="flex min-h-full items-start justify-center p-3 sm:items-center sm:p-4">
        <div
          className="relative max-h-[calc(100dvh-1.5rem)] w-full max-w-5xl overflow-hidden rounded-lg border border-gray-200 bg-white p-4 text-gray-900 shadow-xl dark:border-slate-800 dark:bg-slate-900 dark:text-slate-100 sm:max-h-[calc(100dvh-2rem)] sm:p-6"
          onClick={(e) => e.stopPropagation()}
          role="dialog"
          aria-modal="true"
          aria-label={`Archived ticket ${formatTicketIdentifier(ticket.id)}`}
        >
          <div className="grid h-[calc(100dvh-6rem)] min-h-0 gap-6 grid-cols-1 lg:grid-cols-[minmax(0,1fr)_380px]">
            <div className="relative flex min-h-0 min-w-0 flex-col">
              <div
                ref={archivedDetailsScrollRef}
                className="scroll-surface min-h-0 flex-1 space-y-6 overflow-y-auto pr-1"
              >
                <div className="flex items-start justify-between gap-3 border-b border-gray-200 pb-5 dark:border-slate-800">
                  <div className="min-w-0 flex-1">
                    <label className="mb-2 block text-lg font-medium text-gray-700 dark:text-slate-300">
                      Enter Ticket Title
                    </label>
                    <input
                      type="text"
                      value={ticket.title}
                      readOnly
                      className="mb-1 w-full cursor-not-allowed border-b border-gray-300 bg-transparent text-xl font-bold text-gray-900 opacity-80 focus:outline-none dark:border-slate-700 dark:text-slate-100"
                    />
                    <p className="text-sm text-gray-500 dark:text-slate-400">
                      {formatTicketIdentifier(ticket.id)}
                    </p>
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                      <span className="rounded-full bg-cortex-blue-soft px-3 py-1 text-xs font-semibold text-cortex-ink dark:bg-cortex-blue/20 dark:text-slate-100">
                        {formatDisplayValue(ticket.status)}
                      </span>
                      <span className="rounded-full bg-gray-100 px-3 py-1 text-xs font-semibold text-gray-700 dark:bg-slate-800 dark:text-slate-300">
                        {formatDisplayValue(ticket.priority)}
                      </span>
                      <span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-800 dark:bg-amber-900/30 dark:text-amber-300">
                        Archived {formatDisplayDateTime(ticket.archivedDate)}
                      </span>
                    </div>
                  </div>
                  <button
                    onClick={onClose}
                    className="text-2xl font-bold text-gray-400 hover:text-gray-600 dark:text-slate-500 dark:hover:text-slate-300"
                  >
                    ×
                  </button>
                </div>

                <div className="rounded-md border border-gray-200 bg-white p-4 dark:border-slate-800 dark:bg-slate-900/40">
                  <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                    Description
                  </label>
                  <textarea
                    value={ticket.description}
                    readOnly
                    rows={4}
                    className="w-full cursor-not-allowed rounded-md border-gray-300 bg-white text-gray-900 opacity-80 shadow-sm dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
                  />
                </div>

                <div className="grid grid-cols-1 gap-5 rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-800/40 md:grid-cols-2">
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Board
                    </label>
                    <input
                      type="text"
                      value={formatDisplayValue(ticket.boardName)}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Story Points
                    </label>
                    <input
                      type="text"
                      value={ticket.storyPoints === undefined ? "—" : String(ticket.storyPoints)}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Priority
                    </label>
                    <input
                      type="text"
                      value={formatDisplayValue(ticket.priority)}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Status
                    </label>
                    <input
                      type="text"
                      value={formatDisplayValue(ticket.status)}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Syniti Owner
                    </label>
                    <input
                      type="text"
                      value={formatDisplayValue(readOnlySynitiOwnerLabel(ticket))}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300">
                      Business Owner
                    </label>
                    <input
                      type="text"
                      value={formatDisplayValue(readOnlyBusinessOwnerLabel(ticket))}
                      readOnly
                      className="w-full cursor-not-allowed rounded-md border-gray-300 bg-gray-100 text-gray-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300"
                    />
                  </div>
                </div>

                <div className="rounded-md border border-gray-200 bg-gray-50 p-4 dark:border-slate-800 dark:bg-slate-800/60">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 dark:text-slate-300">
                        Attachments
                      </label>
                      <p className="mt-1 text-sm text-gray-500 dark:text-slate-400">
                        Archived files are read-only and remain downloadable.
                      </p>
                    </div>
                    <button
                      type="button"
                      className="inline-flex cursor-default items-center rounded-md bg-cortex-blue px-3 py-2 text-sm font-medium text-white"
                    >
                      {attachments.length}
                    </button>
                  </div>

                  <div className="mt-4 space-y-3">
                    {loadingAttachments ? (
                      <p className="text-sm text-gray-500 dark:text-slate-400">
                        Loading attachments…
                      </p>
                    ) : attachmentsError ? (
                      <p className="text-sm text-red-700 dark:text-red-300">
                        {attachmentsError}
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
                              {formatFileSize(attachment.fileSize)} · {attachment.contentType} ·{" "}
                              {formatDisplayValue(attachment.uploadedByDisplayName)} ·{" "}
                              {formatDisplayDateTime(attachment.uploadedDate)}
                            </p>
                          </div>
                          <div className="flex items-center gap-2">
                            <button
                              onClick={() => void openAttachment(attachment)}
                              disabled={attachmentActionId === attachment.id}
                              className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                            >
                              Open
                            </button>
                            <button
                              onClick={() => void downloadAttachment(attachment)}
                              disabled={attachmentActionId === attachment.id}
                              className="rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-700 transition-colors hover:bg-gray-100 disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                            >
                              Download
                            </button>
                          </div>
                        </div>
                      ))
                    ) : (
                      <p className="text-sm text-gray-500 dark:text-slate-400">
                        No archived attachments available.
                      </p>
                    )}
                  </div>
                </div>
              </div>
              <ScrollToBottomButton
                containerRef={archivedDetailsScrollRef}
                aria-label="Scroll archived ticket details to bottom"
              />

              <div className="flex shrink-0 flex-col gap-3 border-t border-gray-200 bg-white pt-4 dark:border-slate-800 dark:bg-slate-900 sm:flex-row sm:items-center sm:justify-between">
                <div className="text-xs text-gray-500 dark:text-slate-400">
                  Created {formatDisplayDateTime(ticket.createdDate)} by{" "}
                  {formatDisplayValue(ticket.createdByDisplayName)} · Last updated{" "}
                  {formatDisplayDateTime(ticket.lastModifiedDate)} · Archived by{" "}
                  {formatDisplayValue(ticket.archivedByDisplayName)}
                </div>
                <button
                  onClick={onClose}
                  className="rounded-md bg-cortex-blue px-4 py-2 text-white transition-colors hover:bg-cortex-blue-dark"
                >
                  Close
                </button>
              </div>
            </div>

            <aside className="flex min-h-0 min-w-0 flex-col overflow-hidden rounded-lg border border-gray-200 bg-gray-50 dark:border-slate-800 dark:bg-slate-900/70">
              <div className="border-b border-gray-200 px-4 py-3 dark:border-slate-800">
                <div className="flex items-center justify-between gap-3">
                  <h3 className="text-sm font-semibold text-gray-700 dark:text-slate-300">
                    Comments
                  </h3>
                  <span className="rounded-full bg-gray-200 px-2.5 py-1 text-xs font-semibold text-gray-700 dark:bg-slate-800 dark:text-slate-300">
                    {comments.length}
                  </span>
                </div>
              </div>

              <div className="relative min-h-0 flex-1">
                <div
                  ref={archivedCommentsScrollRef}
                  className="scroll-surface h-full overflow-y-auto px-4 py-4"
                >
                  {loadingComments ? (
                    <p className="text-sm text-gray-500 dark:text-slate-400">Loading comments…</p>
                  ) : commentsError ? (
                    <p className="text-sm text-red-700 dark:text-red-300">{commentsError}</p>
                  ) : (
                    <CommentList comments={comments} />
                  )}
                </div>
                <ScrollToBottomButton
                  containerRef={archivedCommentsScrollRef}
                  aria-label="Scroll archived comments to bottom"
                />
              </div>
              <div className="border-t border-gray-200 bg-white px-4 py-3 dark:border-slate-800 dark:bg-slate-900">
                <p className="text-xs text-gray-500 dark:text-slate-400">
                  Archived tickets are read-only. Comments are shown for context.
                </p>
              </div>
            </aside>
          </div>
        </div>
      </div>
    </div>
  );
}
