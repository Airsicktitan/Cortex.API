/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { useEffect, useMemo, useState } from "react";
import type { Ticket } from "./types/ticket";
import { ticketService } from "./services/api";
import TicketCard from "./components/TicketCard";
import TicketModal from "./components/TicketModal";

function useDebouncedValue<T>(value: T, delayMs: number) {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(handle);
  }, [value, delayMs]);

  return debounced;
}

function normalize(v: string) {
  return v.trim().toLowerCase();
}

function App() {
  const [allTickets, setAllTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [filter, setFilter] = useState<"all" | "status" | "priority">("all");
  const [filterValue, setFilterValue] = useState("");
  const debouncedFilterValue = useDebouncedValue(filterValue, 300);

  // Modal state
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const loadAllTickets = async () => {
    setLoading(true);
    setError(null);

    try {
      const data = await ticketService.getAll();
      setAllTickets(data);
    } catch (err) {
      setError(
        "Failed to load tickets. Make sure the API is running on http://localhost:5214",
      );
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAllTickets();
  }, []);

  const tickets = useMemo(() => {
    const v = normalize(filterValue);
    if (filter === "all" || !v) return allTickets;

    if (filter === "status") {
      return allTickets.filter(
        (t) => normalize(String((t as any).status ?? "")).includes(v), // ← Changed === to .includes()
      );
    }

    // priority
    return allTickets.filter(
      (t) => normalize(String((t as any).priority ?? "")).includes(v), // ← Changed === to .includes()
    );
  }, [allTickets, filter, filterValue]);

  const handleTicketClick = (ticket: Ticket) => {
    setSelectedTicket(ticket);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setSelectedTicket(null);
  };

  const handleSaveTicket = async (updatedTicket: Partial<Ticket>) => {
    if (!selectedTicket) return;

    try {
      const saved = await ticketService.update(
        selectedTicket.id,
        updatedTicket,
      );

      // Update the ticket in the local state
      setAllTickets((prev) => prev.map((t) => (t.id === saved.id ? saved : t)));
    } catch (error) {
      console.error("Failed to update ticket:", error);
      throw error;
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100">
      <header className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <div className="text-4xl">🧠</div>
              <div>
                <h1 className="text-3xl font-bold text-gray-900">CORTEX</h1>
                <p className="text-sm text-gray-500">
                  Central Operations & Routing Technology EXpert
                </p>
              </div>
            </div>

            <div className="flex items-center space-x-4">
              <select
                value={filter}
                onChange={(e: { target: { value: any; }; }) => {
                  setFilter(e.target.value as any);
                  setFilterValue("");
                }}
                className="rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
              >
                <option value="all">All Tickets</option>
                <option value="status">By Status</option>
                <option value="priority">By Priority</option>
              </select>

              {filter !== "all" && (
                <input
                  type="text"
                  placeholder={`Enter ${filter}...`}
                  value={filterValue}
                  onChange={(e) => setFilterValue(e.target.value)}
                  className="rounded-md border-gray-300 shadow-sm focus:border-cortex-blue focus:ring focus:ring-cortex-blue focus:ring-opacity-50"
                />
              )}

              <button
                onClick={loadAllTickets}
                className="px-4 py-2 bg-cortex-blue text-white rounded-md hover:bg-blue-700 transition-colors"
              >
                Refresh
              </button>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-4 py-8 sm:px-6 lg:px-8">
        {loading && (
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-cortex-blue"></div>
            <p className="mt-4 text-gray-600">Loading tickets...</p>
          </div>
        )}

        {error && (
          <div className="bg-red-50 border-l-4 border-red-500 p-4 rounded">
            <div className="flex">
              <span className="text-red-500 text-xl">⚠️</span>
              <p className="ml-3 text-sm text-red-700">{error}</p>
            </div>
          </div>
        )}

        {!loading && !error && tickets.length === 0 && (
          <div className="text-center py-12">
            <div className="text-6xl mb-4">📭</div>
            <p className="text-gray-600">No tickets found</p>
          </div>
        )}

        {!loading && !error && tickets.length > 0 && (
          <>
            <div className="mb-6">
              <h2 className="text-2xl font-semibold text-gray-900">
                {filter === "all" && `All Tickets (${tickets.length})`}
                {filter === "status" &&
                  debouncedFilterValue &&
                  `Status containing "${debouncedFilterValue}": ${tickets.length} found`}
                {filter === "priority" &&
                  debouncedFilterValue &&
                  `Priority containing "${debouncedFilterValue}": ${tickets.length} found`}
              </h2>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {tickets.map((ticket) => (
                <TicketCard
                  key={ticket.id}
                  ticket={ticket}
                  onClick={() => handleTicketClick(ticket)}
                />
              ))}
            </div>
          </>
        )}
      </main>

      <footer className="mt-12 py-6 text-center text-gray-500 text-sm">
        Built by Adam Hooper | Syniti Engineering Transition Project
      </footer>

      {/* Modal */}
      {selectedTicket && (
        <TicketModal
          ticket={selectedTicket}
          isOpen={isModalOpen}
          onClose={handleCloseModal}
          onSave={handleSaveTicket}
        />
      )}
    </div>
  );
}

export default App;
