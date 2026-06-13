import { useQuery } from "@tanstack/react-query";
import {
    getItemsQueryKey,
    getListRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls this list's opaque revision token every 2s (while focused) and invalidates the list-items
// query only when another user's change moves the token. Local edits win — an in-flight mutation on
// this list (its variables carry path.listId) suppresses the remote refetch for that tick.
export const useListRevision = (householdId: number, listId: number) => {
    const enabled = householdId > 0 && listId > 0;

    const { data } = useQuery({
        ...getListRevisionOptions({ path: { householdId, listId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getItemsQueryKey({ path: { householdId, listId } }),
        isLocalMutation: (variables) =>
            (variables as { path?: { listId?: number } } | undefined)?.path
                ?.listId === listId,
    });
};
