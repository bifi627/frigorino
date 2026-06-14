import { useQuery } from "@tanstack/react-query";
import { getRecipeOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useRecipe = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeOptions({ path: { householdId, recipeId } }),
        enabled: enabled && recipeId > 0 && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
