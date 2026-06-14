import { useQuery } from "@tanstack/react-query";
import { getRecipesOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdRecipes = (householdId: number, enabled = true) =>
    useQuery({
        ...getRecipesOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
        refetchOnMount: "always",
    });
