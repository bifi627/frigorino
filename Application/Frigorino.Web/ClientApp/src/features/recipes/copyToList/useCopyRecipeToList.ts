import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    copyRecipeToListMutation,
    getItemsQueryKey,
    getListQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

// Copies selected recipe ingredients into a list. Caller passes
// { path: { householdId, recipeId }, body: { targetListId, items } }.
// Invalidates the target list's items and the list summary (item count).
export const useCopyRecipeToList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...copyRecipeToListMutation(),
        onSuccess: (_data, variables) => {
            const { householdId } = variables.path;
            const listId = variables.body.targetListId;
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({ path: { householdId, listId } }),
            });
            queryClient.invalidateQueries({
                queryKey: getListQueryKey({ path: { householdId, listId } }),
            });
        },
    });
};
