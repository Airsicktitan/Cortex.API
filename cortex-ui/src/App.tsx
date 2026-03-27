/* eslint-disable @typescript-eslint/no-explicit-any */
import { useEffect, useMemo, useState } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import type { Ticket } from "./types/ticket";
import { ticketService } from "./services/api";
import TicketCard from "./components/TicketCard";
import TicketModal from "./components/TicketModal";
import ConfirmDeleteModal from "./components/ConfirmDeleteModal";
import toast from "react-hot-toast";

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
  const {
    isAuthenticated,
    isLoading: authLoading,
    user,
    loginWithRedirect,
    logout,
  } = useAuth0();

  const [allTickets, setAllTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentUser, setCurrentUser] = useState<any | null>(null);

  const [filter, setFilter] = useState<"all" | "status" | "priority">("all");
  const [filterValue, setFilterValue] = useState("");
  const debouncedFilterValue = useDebouncedValue(filterValue, 300);

  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const [ticketToDelete, setTicketToDelete] = useState<Ticket | null>(null);
  const [deleting, setDeleting] = useState(false);
  const { getAccessTokenSilently } = useAuth0();
  const loadAllTickets = async () => {
    setLoading(true);
    setError(null);

    try {
      const data = await ticketService.getAll();
      setAllTickets(data);
    } catch {
      setError("Failed to load tickets. Make sure the API is running.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!isAuthenticated) return;

    const bootstrap = async () => {
      const token = await getAccessTokenSilently();

      // triggers UserContextService
      const currentUser = await ticketService.getCurrentUser(token);
      setCurrentUser(currentUser);

      // Then load tickets
      await loadAllTickets();
    };

    bootstrap();
  }, [isAuthenticated, getAccessTokenSilently]);

  const tickets = useMemo(() => {
    const v = normalize(debouncedFilterValue);
    if (filter === "all" || !v) return allTickets;

    if (filter === "status") {
      return allTickets.filter((t) =>
        normalize(String((t as any).status ?? "")).includes(v),
      );
    }

    return allTickets.filter((t) =>
      normalize(String((t as any).priority ?? "")).includes(v),
    );
  }, [allTickets, filter, debouncedFilterValue]);

  const handleSaveTicket = async (updatedTicket: Partial<Ticket>) => {
    if (!selectedTicket) return;
    const token = await getAccessTokenSilently();

    try {
      if (!selectedTicket.id) {
        const created = await ticketService.create(
          updatedTicket as Omit<Ticket, "id" | "createdDate" | "createdBy">,
          token,
        );
        setAllTickets((prev) => [created, ...prev]);
        toast.success("Ticket created");
      } else {
        const saved = await ticketService.update(
          selectedTicket.id,
          updatedTicket,
          token,
        );
        setAllTickets((prev) =>
          prev.map((t) => (t.id === saved.id ? saved : t)),
        );
        toast.success("Ticket updated");
      }

      setIsModalOpen(false);
      setSelectedTicket(null);
    } catch {
      toast.error("Failed to save ticket");
    }
  };

  const requestDeleteTicket = (ticket: Ticket) => {
    setTicketToDelete(ticket);
  };

  const confirmDeleteTicket = async () => {
    if (!ticketToDelete) return;

    try {
      setDeleting(true);

      await ticketService.delete(ticketToDelete.id);

      setAllTickets((prev) => prev.filter((t) => t.id !== ticketToDelete.id));

      toast.success("Ticket deleted");
    } catch {
      toast.error("Failed to delete ticket");
    } finally {
      setDeleting(false);
      setTicketToDelete(null);
    }
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setTimeout(() => {
      setSelectedTicket(null);
    }, 0);
  };

  const openTicket = (ticket: Ticket) => {
    setSelectedTicket(ticket);
    setIsModalOpen(true);
  };

  if (authLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-cortex-blue mx-auto" />
          <p className="mt-4 text-gray-600">Loading...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 flex items-center justify-center">
        <div className="text-center">
          <h1 className="text-4xl font-bold mb-4">🧠 CORTEX</h1>
          <p className="text-gray-600 mb-6">Support Ticket System</p>
          <button
            onClick={() => loginWithRedirect()}
            className="px-6 py-3 bg-cortex-blue text-white rounded-md hover:bg-blue-700 transition-colors"
          >
            Log In
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100">
      <header className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-6 py-6 flex justify-between items-center">
          <h1 className="text-3xl font-bold">🧠 CORTEX</h1>
          <h2 className="text-lg text-gray-600">Support Ticket System</h2>

          <div className="flex items-center space-x-4">
            <select
              value={filter}
              onChange={(e) => {
                setFilter(e.target.value as any);
                setFilterValue("");
              }}
              className="rounded-md border-gray-300 shadow-sm"
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
                className="rounded-md border-gray-300 shadow-sm"
              />
            )}

            <button
              onClick={loadAllTickets}
              className="px-4 py-2 bg-cortex-blue text-white rounded-md"
            >
              Refresh
            </button>

            <button
              onClick={() =>
                openTicket({
                  id: "",
                  title: "",
                  description: "",
                  priority: "Medium",
                  status: "New",
                  createdDate: new Date().toISOString(),
                } as Ticket)
              }
              className="px-4 py-2 bg-green-600 text-white rounded-md"
            >
              + New Ticket
            </button>

            <div className="flex items-center gap-3 ml-4 pl-4 border-l border-gray-300">
              <span className="text-sm text-gray-700">
                {currentUser?.displayName ?? user?.name}
              </span>
              <button
                onClick={() =>
                  logout({ logoutParams: { returnTo: window.location.origin } })
                }
                className="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 transition-colors"
              >
                Log Out
              </button>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8">
        {loading && (
          <div className="text-center py-12">
            <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-cortex-blue mx-auto" />
            <p className="mt-4 text-gray-600">Loading tickets…</p>
          </div>
        )}

        {error && (
          <div className="bg-red-50 border-l-4 border-red-500 p-4 rounded">
            <p className="text-red-700">{error}</p>
          </div>
        )}

        {!loading && !error && tickets.length === 0 && (
          <p className="text-gray-600 text-center">No tickets found</p>
        )}

        {!loading && !error && tickets.length > 0 && (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {tickets.map((ticket) => (
              <TicketCard
                key={ticket.id}
                ticket={ticket}
                onClick={() => openTicket(ticket)}
              />
            ))}
          </div>
        )}
      </main>

      {selectedTicket && isModalOpen && (
        <TicketModal
          key={selectedTicket.id ?? "new"}
          ticket={selectedTicket}
          isOpen
          onClose={closeModal}
          onSave={handleSaveTicket}
          onDelete={requestDeleteTicket}
          currentUser={currentUser}
        />
      )}

      <ConfirmDeleteModal
        isOpen={!!ticketToDelete}
        onCancel={() => setTicketToDelete(null)}
        onConfirm={confirmDeleteTicket}
        loading={deleting}
      />
    </div>
  );
}

export default App;
