import { useQuery } from "@tanstack/react-query";
import { getRecipeLinksOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeLinks = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeLinksOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
