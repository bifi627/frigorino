import { useQuery } from "@tanstack/react-query";
import { getInventoriesOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdInventories = (
    householdId: number,
    enabled = true,
) =>
    useQuery({
        ...getInventoriesOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
