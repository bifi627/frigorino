import { useQuery } from "@tanstack/react-query";
import {
    getInventoryItemsQueryKey,
    getInventoryRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls this inventory's opaque revision token every 2s (while focused) and invalidates the
// inventory-items query only when another user's change moves the token. An in-flight mutation on this
// inventory (variables carry path.inventoryId) suppresses the remote refetch for that tick.
export const useInventoryRevision = (
    householdId: number,
    inventoryId: number,
) => {
    const enabled = householdId > 0 && inventoryId > 0;

    const { data } = useQuery({
        ...getInventoryRevisionOptions({ path: { householdId, inventoryId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getInventoryItemsQueryKey({
            path: { householdId, inventoryId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { inventoryId?: number } } | undefined)?.path
                ?.inventoryId === inventoryId,
    });
};
