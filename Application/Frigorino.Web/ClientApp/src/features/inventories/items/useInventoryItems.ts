import { useQuery } from "@tanstack/react-query";
import { getInventoryItemsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useInventoryItems = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) =>
    useQuery({
        ...getInventoryItemsOptions({ path: { householdId, inventoryId } }),
        enabled: enabled && householdId > 0 && inventoryId > 0,
        staleTime: 1000 * 30,
    });
