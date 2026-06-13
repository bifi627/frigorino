import { useQuery } from "@tanstack/react-query";
import { getBlueprintsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useSortBlueprints = (householdId: number, enabled = true) =>
    useQuery({
        ...getBlueprintsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
