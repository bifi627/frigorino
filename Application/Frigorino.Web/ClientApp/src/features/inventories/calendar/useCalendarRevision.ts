import { useQuery } from "@tanstack/react-query";
import {
    getExpiryCalendarQueryKey,
    getExpiryCalendarRevisionOptions,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls the household-wide expiry-calendar revision token every 2s (while focused) and invalidates the
// calendar query only when another user's change moves the token. The calendar is household-wide, so
// suppress only on in-flight INVENTORY-item mutations (those carry path.inventoryId); list mutations
// don't affect the calendar.
export const useCalendarRevision = (householdId: number) => {
    const enabled = householdId > 0;

    const { data } = useQuery({
        ...getExpiryCalendarRevisionOptions({ path: { householdId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getExpiryCalendarQueryKey({ path: { householdId } }),
        isLocalMutation: (variables) =>
            (variables as { path?: { inventoryId?: number } } | undefined)?.path
                ?.inventoryId != null,
        // An in-flight inventory-item mutation re-invalidates the inventory-items query, NOT this
        // household-wide calendar query — so advancing the baseline on suppression would drop a
        // coincident remote calendar change. Opt out: re-detect it on the next tick after settle.
        advanceBaselineWhenSuppressed: false,
    });
};
