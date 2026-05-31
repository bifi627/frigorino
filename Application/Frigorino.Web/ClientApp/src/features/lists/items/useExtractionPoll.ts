import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import {
    getItemOptions,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

const MAX_POLL_MS = 4000;
const INTERVAL_MS = 600;

// Polls a single just-created item until its quantity arrives (extraction completed) or the
// deadline passes, then patches the items-list cache. `enabled` should mirror the create
// response's `extractionPending` — true only when the server enqueued an async extraction.
//
// Timing has ONE authority: the `active` window. A single deadline timer flips it off after
// MAX_POLL_MS; the query polls only while `active` (so the same flip stops the poll), and the
// row indicator reads `active` too. There is no second deadline and no wall-clock comparison
// inside the query, so the UI window and the poll lifetime cannot drift apart.
export const useExtractionPoll = (
    householdId: number,
    listId: number,
    itemId: number | null,
    enabled: boolean,
) => {
    const queryClient = useQueryClient();
    // The extraction window: true from the moment a pollable item arrives until the quantity
    // lands or the deadline passes. It drives both the row indicator and the query's lifetime,
    // so it must not flicker with the brief in-flight (`isFetching`) moments of each poll.
    const [active, setActive] = useState(false);
    const pollable = enabled && (itemId ?? 0) > 0;

    useEffect(() => {
        if (!pollable) {
            setActive(false);
            return;
        }
        setActive(true);
        const timer = setTimeout(() => setActive(false), MAX_POLL_MS);
        return () => clearTimeout(timer);
    }, [itemId, pollable]);

    const query = useQuery({
        ...getItemOptions({
            path: { householdId, listId, itemId: itemId ?? 0 },
        }),
        enabled: active,
        refetchInterval: (q) => {
            const data = q.state.data as ListItemResponse | undefined;
            return data?.quantity ? false : INTERVAL_MS;
        },
        staleTime: 0,
        gcTime: 0,
    });

    useEffect(() => {
        const item = query.data;
        if (!item?.quantity) return;
        setActive(false); // quantity landed — close the window immediately
        queryClient.setQueryData<ListItemResponse[]>(
            getItemsQueryKey({ path: { householdId, listId } }),
            (old) =>
                old?.map((i) =>
                    i.id === item.id
                        ? { ...i, text: item.text, quantity: item.quantity }
                        : i,
                ) ?? old,
        );
    }, [query.data, householdId, listId, queryClient]);

    const isExtracting = active && !query.data?.quantity;

    return { isExtracting, extractingItemId: itemId };
};
