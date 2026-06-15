import { useQuery } from "@tanstack/react-query";
import {
    getRecipeItemsQueryKey,
    getRecipeLinksQueryKey,
    getRecipeRevisionOptions,
    getRecipeSectionsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import {
    REVISION_QUERY_OPTIONS,
    useRevisionInvalidation,
} from "../../../hooks/useRevisionInvalidation";

// Polls this recipe's opaque revision token every 2s (while focused) and invalidates the
// recipe-items query only when another user's change moves the token. An in-flight mutation on
// this recipe (variables carry path.recipeId) suppresses the remote refetch for that tick.
export const useRecipeRevision = (householdId: number, recipeId: number) => {
    const enabled = householdId > 0 && recipeId > 0;

    const { data } = useQuery({
        ...getRecipeRevisionOptions({ path: { householdId, recipeId } }),
        ...REVISION_QUERY_OPTIONS,
        enabled,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeItemsQueryKey({
            path: { householdId, recipeId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path
                ?.recipeId === recipeId,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeSectionsQueryKey({
            path: { householdId, recipeId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path
                ?.recipeId === recipeId,
    });

    useRevisionInvalidation({
        rev: data?.rev,
        dataQueryKey: getRecipeLinksQueryKey({
            path: { householdId, recipeId },
        }),
        isLocalMutation: (variables) =>
            (variables as { path?: { recipeId?: number } } | undefined)?.path
                ?.recipeId === recipeId,
    });
};
