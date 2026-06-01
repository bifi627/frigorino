import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";
import { useShallow } from "zustand/react/shallow";
import type { PromoteSuggestion, QuantityDto } from "../../../lib/api";

// One pending promote candidate. Persisted to localStorage so a mid-shop refresh doesn't lose
// the batch. Device-scoped by design — no DB row (see spec).
export interface PromotableEntry {
    itemId: number;
    listId: number;
    name: string;
    quantity: QuantityDto | null;
    expiryHandling: PromoteSuggestion["expiryHandling"];
    suggestedExpiry: string | null;
}

interface PromotableState {
    entries: PromotableEntry[];
    add: (entry: PromotableEntry) => void;
    remove: (itemId: number) => void;
    clearForList: (listId: number) => void;
}

export const usePromotableStore = create<PromotableState>()(
    persist(
        (set) => ({
            entries: [],
            // Replace any existing entry for the same item (re-toggle keeps the latest suggestion).
            add: (entry) =>
                set((s) => ({
                    entries: [
                        ...s.entries.filter((e) => e.itemId !== entry.itemId),
                        entry,
                    ],
                })),
            remove: (itemId) =>
                set((s) => ({
                    entries: s.entries.filter((e) => e.itemId !== itemId),
                })),
            clearForList: (listId) =>
                set((s) => ({
                    entries: s.entries.filter((e) => e.listId !== listId),
                })),
        }),
        {
            name: "frigorino.promote.batch",
            storage: createJSONStorage(() => localStorage),
        },
    ),
);

// Stable filtered selector for a list's pending entries (useShallow avoids re-render churn when
// an unrelated list's entries change).
export const usePromotableForList = (listId: number): PromotableEntry[] =>
    usePromotableStore(
        useShallow((s) => s.entries.filter((e) => e.listId === listId)),
    );
