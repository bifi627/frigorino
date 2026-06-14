import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeItemMutation,
    getRecipeItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { RecipeItemResponse } from "../../../lib/api/types.gen";
import { useRestoreRecipeItem } from "./useRestoreRecipeItem";

export const useDeleteRecipeItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreItem = useRestoreRecipeItem();

    return useMutation({
        ...deleteRecipeItemMutation(),
        onMutate: async (variables) => {
            const queryKey = getRecipeItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    recipeId: variables.path.recipeId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<RecipeItemResponse[]>(queryKey);

            queryClient.setQueryData<RecipeItemResponse[]>(
                queryKey,
                (old) => {
                    if (!old) return old;
                    return old.filter(
                        (item) => item.id !== variables.path.itemId,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getRecipeItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            recipeId: variables.path.recipeId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("common.itemDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreItem.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getRecipeItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        recipeId: variables.path.recipeId,
                    },
                }),
            );
        },
    });
};
