import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
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

    // Track the poll start time when a new itemId arrives
    useEffect(() => {
        if (enabled && (itemId ?? 0) > 0) {
            startedAtRef.current = Date.now();
        }
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

    const isExtracting =
        enabled &&
        (itemId ?? 0) > 0 &&
        !query.data?.quantity &&
        query.isFetching;

    return { isExtracting, extractingItemId: itemId };
};
