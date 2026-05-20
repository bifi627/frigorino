import { useQuery } from "@tanstack/react-query";
import { getInventoryOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useInventory = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) =>
    useQuery({
        ...getInventoryOptions({ path: { householdId, inventoryId } }),
        enabled: enabled && inventoryId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
