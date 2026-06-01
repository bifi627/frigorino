import { useQuery } from "@tanstack/react-query";
import { getInventorySettingsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useInventorySettings = (
    householdId: number,
    inventoryId: number,
    enabled = true,
) =>
    useQuery({
        ...getInventorySettingsOptions({ path: { householdId, inventoryId } }),
        enabled: enabled && householdId > 0 && inventoryId > 0,
        staleTime: 1000 * 60 * 5,
    });
