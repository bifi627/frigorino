import { useQuery } from "@tanstack/react-query";
import { getPendingPromotionsOptions } from "../../../lib/api/@tanstack/react-query.gen";

// Server-shared pending-promotion batch for a list. Replaces the device-local promotableStore.
// `enabled` lets the sheet fetch only when opened.
export const usePendingPromotions = (
    householdId: number,
    listId: number,
    enabled = true,
) =>
    useQuery({
        ...getPendingPromotionsOptions({ path: { householdId, listId } }),
        enabled: enabled && householdId > 0 && listId > 0,
        staleTime: 1000 * 30,
    });
