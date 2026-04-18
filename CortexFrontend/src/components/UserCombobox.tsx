import { useEffect, useId, useMemo, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import type { UserDirectoryEntry } from "../types/user";
import {
  computeDuplicateDisplayNames,
  getOwnerOptionSubtitleLines,
  normalizeOwnerToken,
  ownerDisplayLabel,
  ownerStorageValue,
  storedOwnerMatchesUser,
  USER_ID_TOKEN_PREFIX,
} from "../utils/ownerIdentity";

interface UserComboboxProps {
  label: string;
  value: string;
  users: UserDirectoryEntry[];
  onChange: (value: string) => void;
  placeholder?: string;
  helperText?: string;
  disabled?: boolean;
  loading?: boolean;
  noResultsText?: string;
}

function getUserSearchText(user: UserDirectoryEntry) {
  const storage = ownerStorageValue(user);
  return [
    user.displayName,
    user.email,
    user.department,
    user.role,
    storage,
    `${USER_ID_TOKEN_PREFIX}${user.id}`,
  ]
    .map((value) => normalizeOwnerToken(value))
    .join(" ");
}

export default function UserCombobox({
  label,
  value,
  users,
  onChange,
  placeholder = "Search users...",
  helperText,
  disabled = false,
  loading = false,
  noResultsText = "No users found.",
}: UserComboboxProps) {
  const id = useId();
  const rootRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  const closedLabel = useMemo(
    () => ownerDisplayLabel(value, users),
    [value, users],
  );

  const duplicateDisplayNames = useMemo(
    () => computeDuplicateDisplayNames(users),
    [users],
  );

  const filteredUsers = useMemo(() => {
    const normalizedQuery = normalizeOwnerToken(query);
    if (!normalizedQuery) {
      return users;
    }

    return users.filter((user) =>
      getUserSearchText(user).includes(normalizedQuery),
    );
  }, [query, users]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const handlePointerDown = (event: MouseEvent) => {
      if (rootRef.current?.contains(event.target as Node)) {
        return;
      }

      setIsOpen(false);
      setQuery(closedLabel);
    };

    window.addEventListener("mousedown", handlePointerDown);
    return () => window.removeEventListener("mousedown", handlePointerDown);
  }, [isOpen, closedLabel]);

  const openMenu = () => {
    if (disabled) {
      return;
    }

    setIsOpen(true);
    setQuery(closedLabel);
  };

  const closeMenu = () => {
    setIsOpen(false);
    setQuery(closedLabel);
  };

  const selectUser = (user: UserDirectoryEntry) => {
    onChange(ownerStorageValue(user));
    setQuery(user.displayName);
    setIsOpen(false);
  };

  const clearSelection = () => {
    onChange("");
    setQuery("");
    inputRef.current?.focus();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (disabled) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      if (!isOpen) {
        openMenu();
        return;
      }

      setHighlightedIndex((currentIndex) =>
        filteredUsers.length === 0
          ? 0
          : Math.min(currentIndex + 1, filteredUsers.length - 1),
      );
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      if (!isOpen) {
        openMenu();
        return;
      }

      setHighlightedIndex((currentIndex) => Math.max(currentIndex - 1, 0));
      return;
    }

    if (event.key === "Enter" && isOpen) {
      const highlightedUser =
        filteredUsers[
          filteredUsers.length <= 0
            ? -1
            : Math.min(highlightedIndex, filteredUsers.length - 1)
        ];
      if (highlightedUser) {
        event.preventDefault();
        selectUser(highlightedUser);
      }
      return;
    }

    if (event.key === "Escape") {
      if (isOpen) {
        event.preventDefault();
      }
      closeMenu();
    }
  };

  const handleBlurCapture = () => {
    window.requestAnimationFrame(() => {
      if (!rootRef.current?.contains(document.activeElement)) {
        closeMenu();
      }
    });
  };

  const activeIndex =
    filteredUsers.length <= 0
      ? -1
      : Math.min(highlightedIndex, filteredUsers.length - 1);
  const activeOptionId =
    isOpen && activeIndex >= 0
      ? `${id}-option-${filteredUsers[activeIndex].id}`
      : undefined;

  return (
    <div ref={rootRef} onBlurCapture={handleBlurCapture}>
      <label
        id={`${id}-label`}
        htmlFor={`${id}-input`}
        className="mb-2 block text-sm font-medium text-gray-700 dark:text-slate-300"
      >
        {label}
      </label>

      <div className="relative">
        <input
          ref={inputRef}
          id={`${id}-input`}
          type="text"
          value={isOpen ? query : closedLabel}
          onFocus={openMenu}
          onChange={(event) => {
            setQuery(event.target.value);
            setIsOpen(true);
            setHighlightedIndex(0);
          }}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          disabled={disabled}
          role="combobox"
          autoComplete="off"
          aria-autocomplete="list"
          aria-expanded={isOpen}
          aria-controls={`${id}-listbox`}
          aria-activedescendant={activeOptionId}
          aria-labelledby={`${id}-label`}
          className="w-full rounded-md border-gray-300 bg-white pr-20 text-gray-900 shadow-sm disabled:cursor-not-allowed disabled:bg-gray-100 disabled:text-gray-500 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:disabled:bg-slate-900 dark:disabled:text-slate-400"
        />

        <div className="pointer-events-none absolute inset-y-0 right-0 flex items-center gap-1 pr-2">
          {value && !disabled && (
            <button
              type="button"
              onClick={clearSelection}
              className="pointer-events-auto rounded-md px-2 py-1 text-sm text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-800 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200"
              aria-label={`Clear ${label}`}
            >
              ×
            </button>
          )}
          <button
            type="button"
            onClick={() => {
              if (disabled) {
                return;
              }

              if (isOpen) {
                closeMenu();
                return;
              }

              inputRef.current?.focus();
              openMenu();
            }}
            className="pointer-events-auto rounded-md px-2 py-1 text-sm text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-800 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200"
            aria-label={isOpen ? `Close ${label} options` : `Open ${label} options`}
            disabled={disabled}
          >
            ▾
          </button>
        </div>

        {isOpen && (
          <div className="absolute z-30 mt-1 w-full overflow-hidden rounded-lg border border-gray-200 bg-white shadow-lg dark:border-slate-800 dark:bg-slate-900">
            <ul
              id={`${id}-listbox`}
              role="listbox"
              aria-labelledby={`${id}-label`}
              className="max-h-60 overflow-y-auto py-1"
            >
              {loading ? (
                <li className="px-3 py-2 text-sm text-gray-500 dark:text-slate-400">
                  Loading users...
                </li>
              ) : filteredUsers.length > 0 ? (
                filteredUsers.map((user, index) => {
                  const isSelected = storedOwnerMatchesUser(value, user);
                  const isHighlighted = index === highlightedIndex;
                  const { line2, line3 } = getOwnerOptionSubtitleLines(
                    user,
                    users,
                    duplicateDisplayNames,
                  );

                  return (
                    <li key={user.id} role="presentation">
                      <button
                        id={`${id}-option-${user.id}`}
                        type="button"
                        role="option"
                        aria-selected={isSelected}
                        onMouseDown={(event) => event.preventDefault()}
                        onMouseEnter={() => setHighlightedIndex(index)}
                        onClick={() => selectUser(user)}
                        className={`flex w-full items-start justify-between gap-3 px-3 py-2 text-left transition-colors ${
                          isHighlighted
                            ? "bg-cortex-blue/10 text-gray-900 dark:bg-cortex-blue/20 dark:text-slate-100"
                            : "text-gray-700 hover:bg-gray-50 dark:text-slate-200 dark:hover:bg-slate-800"
                        }`}
                      >
                        <span className="min-w-0">
                          <span className="block truncate text-sm font-medium">
                            {user.displayName}
                          </span>
                          {line2 ? (
                            <span className="block truncate text-xs text-gray-500 dark:text-slate-400">
                              {line2}
                            </span>
                          ) : null}
                          {line3 ? (
                            <span className="block truncate text-xs text-gray-500 dark:text-slate-400">
                              {line3}
                            </span>
                          ) : null}
                        </span>
                        {isSelected && (
                          <span className="shrink-0 text-xs font-semibold text-cortex-blue">
                            Selected
                          </span>
                        )}
                      </button>
                    </li>
                  );
                })
              ) : (
                <li className="px-3 py-2 text-sm text-gray-500 dark:text-slate-400">
                  {noResultsText}
                </li>
              )}
            </ul>
          </div>
        )}
      </div>

      {helperText && (
        <p className="mt-2 text-xs text-gray-500 dark:text-slate-400">
          {helperText}
        </p>
      )}
    </div>
  );
}
