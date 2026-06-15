import { useQuery } from "@tanstack/react-query";
import { getRecipeAttachmentsOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useRecipeAttachments = (
    householdId: number,
    recipeId: number,
    enabled = true,
) =>
    useQuery({
        ...getRecipeAttachmentsOptions({ path: { householdId, recipeId } }),
        enabled: enabled && householdId > 0 && recipeId > 0,
        staleTime: 1000 * 30,
    });
