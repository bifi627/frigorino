import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import {
    getItemOptions,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";

const MAX_POLL_MS = 4000;
const INTERVAL_MS = 600;

// Polls a single just-created item until its quantity arrives (extraction completed) or the
// deadline passes, then patches the items-list cache. `enabled` should be set only when the
// entered text contained a digit (otherwise no extraction runs).
export const useExtractionPoll = (
    householdId: number,
    listId: number,
    itemId: number | null,
    enabled: boolean,
) => {
    const queryClient = useQueryClient();
    const startedAtRef = useRef<number>(0);
    // The extraction window stays "active" from the moment a pollable item arrives until
    // the quantity lands or the deadline passes — this drives the row indicator, so it must
    // not flicker with the brief in-flight (`isFetching`) moments of each poll.
    const [active, setActive] = useState(false);

    useEffect(() => {
        if (!(enabled && (itemId ?? 0) > 0)) {
            setActive(false);
            return;
        }
        startedAtRef.current = Date.now();
        setActive(true);
        const timer = setTimeout(() => setActive(false), MAX_POLL_MS);
        return () => clearTimeout(timer);
    }, [itemId, enabled]);

    const query = useQuery({
        ...getItemOptions({
            path: { householdId, listId, itemId: itemId ?? 0 },
        }),
        enabled: enabled && (itemId ?? 0) > 0,
        refetchInterval: (q) => {
            const data = q.state.data as ListItemResponse | undefined;
            if (data?.quantity) return false; // quantity arrived
            if (Date.now() - startedAtRef.current > MAX_POLL_MS) return false; // timed out
            return INTERVAL_MS;
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
