import { useQuery } from "@tanstack/react-query";
import { getRecipeItemsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeItems = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeItemsOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
