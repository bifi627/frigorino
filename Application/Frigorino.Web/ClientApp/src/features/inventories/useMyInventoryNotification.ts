import { useQuery } from "@tanstack/react-query";
import { getMyInventoryNotificationOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useMyInventoryNotification = (
    householdId: number,
    inventoryId: number,
) =>
    useQuery({
        ...getMyInventoryNotificationOptions({
            path: { householdId, inventoryId },
        }),
        enabled: householdId > 0 && inventoryId > 0,
        staleTime: 5 * 60 * 1000,
    });
