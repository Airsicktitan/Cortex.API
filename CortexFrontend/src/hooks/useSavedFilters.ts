import { useEffect, useState } from "react";
import type { Dispatch, SetStateAction } from "react";
import toast from "react-hot-toast";
import {
  type FilterOption,
  type PageSizeOption,
  normalize,
} from "./useTickets";

export type SavedTicketFilter = {
  id: string;
  name: string;
  filter: FilterOption;
  filterValue: string;
  searchQuery: string;
  pageSize: PageSizeOption;
};

function isFilterOption(value: string): value is FilterOption {
  return (
    value === "all" ||
    value === "status" ||
    value === "priority" ||
    value === "sla"
  );
}

function isPageSizeOption(value: unknown): value is PageSizeOption {
  return value === "all" || value === 10 || value === 25 || value === 50;
}

function createSavedFilterId() {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function parseSavedFilters(rawValue: string | null): SavedTicketFilter[] {
  if (!rawValue) {
    return [];
  }

  try {
    const parsed = JSON.parse(rawValue) as unknown;
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.flatMap((item) => {
      if (typeof item !== "object" || item === null) {
        return [];
      }

      const candidate = item as Partial<SavedTicketFilter>;
      const filterOption = candidate.filter;
      const pageSizeValue =
        candidate.pageSize === "all" ? "all" : Number(candidate.pageSize ?? 0);

      if (
        typeof candidate.id !== "string" ||
        typeof candidate.name !== "string" ||
        typeof filterOption !== "string" ||
        !isFilterOption(filterOption) ||
        !isPageSizeOption(pageSizeValue)
      ) {
        return [];
      }

      return [
        {
          id: candidate.id,
          name: candidate.name,
          filter: filterOption,
          filterValue:
            typeof candidate.filterValue === "string"
              ? candidate.filterValue
              : "",
          searchQuery:
            typeof candidate.searchQuery === "string"
              ? candidate.searchQuery
              : "",
          pageSize: pageSizeValue,
        },
      ];
    });
  } catch {
    return [];
  }
}

type UseSavedFiltersParams = {
  storageKey: string;
  filter: FilterOption;
  filterValue: string;
  searchQuery: string;
  pageSize: PageSizeOption;
  setFilter: Dispatch<SetStateAction<FilterOption>>;
  setFilterValue: Dispatch<SetStateAction<string>>;
  setSearchQuery: Dispatch<SetStateAction<string>>;
  setPageSize: Dispatch<SetStateAction<PageSizeOption>>;
};

export function useSavedFilters({
  storageKey,
  filter,
  filterValue,
  searchQuery,
  pageSize,
  setFilter,
  setFilterValue,
  setSearchQuery,
  setPageSize,
}: UseSavedFiltersParams) {
  const [savedFilters, setSavedFilters] = useState<SavedTicketFilter[]>([]);
  const [selectedSavedFilterId, setSelectedSavedFilterId] = useState("");
  const [isSaveFilterModalOpen, setIsSaveFilterModalOpen] = useState(false);
  const [savedFilterName, setSavedFilterName] = useState("");

  useEffect(() => {
    setSavedFilters(
      parseSavedFilters(window.localStorage.getItem(storageKey)),
    );
    setSelectedSavedFilterId("");
  }, [storageKey]);

  useEffect(() => {
    window.localStorage.setItem(storageKey, JSON.stringify(savedFilters));
  }, [savedFilters, storageKey]);

  const handleFilterChange = (value: string) => {
    setSelectedSavedFilterId("");
    setFilter(isFilterOption(value) ? value : "all");
    setFilterValue("");
  };

  const handleFilterValueChange = (value: string) => {
    setSelectedSavedFilterId("");
    setFilterValue(value);
  };

  const handleSearchChange = (value: string) => {
    setSelectedSavedFilterId("");
    setSearchQuery(value);
  };

  const handlePageSizeChange = (value: string) => {
    setSelectedSavedFilterId("");
    if (value === "all") {
      setPageSize("all");
      return;
    }

    const nextPageSize = Number(value);
    if (nextPageSize === 10 || nextPageSize === 25 || nextPageSize === 50) {
      setPageSize(nextPageSize);
    }
  };

  const openSaveFilterModal = () => {
    const existingFilter = savedFilters.find(
      (savedFilter) => savedFilter.id === selectedSavedFilterId,
    );

    setSavedFilterName(existingFilter?.name ?? "My Ticket View");
    setIsSaveFilterModalOpen(true);
  };

  const closeSaveFilterModal = () => {
    setIsSaveFilterModalOpen(false);
    setSavedFilterName("");
  };

  const saveCurrentFilter = () => {
    const trimmedName = savedFilterName.trim();
    if (!trimmedName) {
      return;
    }

    const existingFilter = savedFilters.find(
      (savedFilter) => normalize(savedFilter.name) === normalize(trimmedName),
    );
    const savedFilterId = existingFilter?.id ?? createSavedFilterId();
    const nextSavedFilter: SavedTicketFilter = {
      id: savedFilterId,
      name: trimmedName,
      filter,
      filterValue,
      searchQuery,
      pageSize,
    };

    setSavedFilters((currentFilters) => [
      nextSavedFilter,
      ...currentFilters.filter(
        (savedFilter) => savedFilter.id !== savedFilterId,
      ),
    ]);
    setSelectedSavedFilterId(savedFilterId);
    closeSaveFilterModal();
    toast.success(
      existingFilter ? "Saved filter updated" : "Saved filter saved",
    );
  };

  const applySavedFilter = (savedFilterId: string) => {
    setSelectedSavedFilterId(savedFilterId);

    if (!savedFilterId) {
      return;
    }

    const savedFilter = savedFilters.find(
      (filterEntry) => filterEntry.id === savedFilterId,
    );
    if (!savedFilter) {
      return;
    }

    setFilter(savedFilter.filter);
    setFilterValue(savedFilter.filterValue);
    setSearchQuery(savedFilter.searchQuery);
    setPageSize(savedFilter.pageSize);
  };

  const clearTicketFilters = () => {
    setSelectedSavedFilterId("");
    setFilter("all");
    setFilterValue("");
    setSearchQuery("");
    setPageSize(10);
  };

  const deleteSavedFilter = () => {
    if (!selectedSavedFilterId) {
      return;
    }

    const filterToDelete = savedFilters.find(
      (savedFilter) => savedFilter.id === selectedSavedFilterId,
    );

    setSavedFilters((currentFilters) =>
      currentFilters.filter(
        (savedFilter) => savedFilter.id !== selectedSavedFilterId,
      ),
    );
    setSelectedSavedFilterId("");
    toast.success(
      filterToDelete
        ? `Removed "${filterToDelete.name}"`
        : "Saved filter removed",
    );
  };

  return {
    savedFilters,
    selectedSavedFilterId,
    setSelectedSavedFilterId,
    isSaveFilterModalOpen,
    savedFilterName,
    setSavedFilterName,
    handleFilterChange,
    handleFilterValueChange,
    handleSearchChange,
    handlePageSizeChange,
    openSaveFilterModal,
    closeSaveFilterModal,
    saveCurrentFilter,
    applySavedFilter,
    clearTicketFilters,
    deleteSavedFilter,
  };
}
