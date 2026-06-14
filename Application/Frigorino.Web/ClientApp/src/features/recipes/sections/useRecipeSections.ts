import { useQuery } from "@tanstack/react-query";
import { getRecipeSectionsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeSections = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeSectionsOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
