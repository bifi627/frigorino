import { useQuery } from "@tanstack/react-query";
import { getBlueprintOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useSortBlueprint = (
    householdId: number,
    blueprintId: number,
    enabled = true,
) =>
    useQuery({
        ...getBlueprintOptions({ path: { householdId, blueprintId } }),
        enabled: enabled && householdId > 0 && blueprintId > 0,
        staleTime: 1000 * 60 * 2,
    });
