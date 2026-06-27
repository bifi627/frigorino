import { useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";

const MAX_POLL_MS = 4000;
const INTERVAL_MS = 600;

interface PollableItem {
    id: number;
    quantity?: unknown;
}

// The spreadable getXItemsOptions({ path }) result — carries the shared list query's key + fn.
// Typed minimally (the two generated shapes differ only in row type); the hook only reads
// queryKey + queryFn, and only ever spreads queryFn into useQuery (never calls it itself).
interface ItemsQueryOptions<T> {
    queryKey: readonly unknown[];
    // Optional + `any` context to match the generated queryOptions() shape (queryFn is typed
    // optional there, and carries a specific queryKey-tuple type we don't care about here).
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    queryFn?: (context: any) => T[] | Promise<T[]>;
}

// Tracks which just-created items are awaiting async quantity extraction and drives a per-row
// "extracting" indicator. Unlike a single-item getItem poll, this refetches the existing items
// LIST while ANY tracked add is in flight — so rapid successive adds each keep their own spinner
// instead of the last one overwriting a single slot. Call markPending(id) with a create response's
// real id when extractionPending is true; the id clears when its row's quantity lands (the refetch
// patches the shared cache) or after MAX_POLL_MS, whichever comes first.
export const useExtractionPoll = <T extends PollableItem>(
    itemsOptions: ItemsQueryOptions<T>,
) => {
    const [pendingIds, setPendingIds] = useState<Set<number>>(
        () => new Set<number>(),
    );
    // Per-id deadline timers — a never-arriving extraction (text whose digits aren't a quantity)
    // must stop being tracked after MAX_POLL_MS so its spinner doesn't hang forever.
    const deadlines = useRef(new Map<number, ReturnType<typeof setTimeout>>());

    const drop = useCallback((id: number) => {
        const timer = deadlines.current.get(id);
        if (timer) {
            clearTimeout(timer);
            deadlines.current.delete(id);
        }
        setPendingIds((prev) => {
            if (!prev.has(id)) return prev;
            const next = new Set(prev);
            next.delete(id);
            return next;
        });
    }, []);

    const markPending = useCallback(
        (id: number) => {
            deadlines.current.set(
                id,
                setTimeout(() => drop(id), MAX_POLL_MS),
            );
            setPendingIds((prev) => {
                if (prev.has(id)) return prev;
                const next = new Set(prev);
                next.add(id);
                return next;
            });
        },
        [drop],
    );

    const hasPending = pendingIds.size > 0;

    const query = useQuery({
        queryKey: itemsOptions.queryKey,
        queryFn: itemsOptions.queryFn,
        enabled: hasPending,
        refetchInterval: hasPending ? INTERVAL_MS : false,
        staleTime: 0,
    });

    const data = query.data;
    useEffect(() => {
        if (!data || !hasPending) return;
        // Stop tracking any row whose extracted quantity has now arrived in the refetched list.
        // This is genuine synchronization with external data (the poll result), so the setState
        // inside drop() is intentional — scoped disable, mirroring the prior single-item poll.
        /* eslint-disable react-hooks/set-state-in-effect */
        for (const item of data) {
            if (item.quantity && pendingIds.has(item.id)) drop(item.id);
        }
        /* eslint-enable react-hooks/set-state-in-effect */
    }, [data, hasPending, pendingIds, drop]);

    useEffect(() => {
        const timers = deadlines.current;
        return () => {
            for (const timer of timers.values()) clearTimeout(timer);
        };
    }, []);

    const isItemExtracting = useCallback(
        (id: number) => pendingIds.has(id),
        [pendingIds],
    );

    return { markPending, isItemExtracting };
};
