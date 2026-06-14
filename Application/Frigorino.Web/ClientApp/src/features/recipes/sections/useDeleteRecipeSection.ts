import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    deleteRecipeSectionMutation,
    getRecipeItemsQueryKey,
    getRecipeSectionsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";
import type {
    RecipeItemResponse,
    RecipeSectionResponse,
} from "../../../lib/api/types.gen";
import { useRestoreRecipeSection } from "./useRestoreRecipeSection";

export const useDeleteRecipeSection = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();
    const { t } = useTranslation();
    const restoreSection = useRestoreRecipeSection();

    return useMutation({
        ...deleteRecipeSectionMutation(),
        onMutate: async (variables) => {
            const path = {
                householdId: variables.path.householdId,
                recipeId: variables.path.recipeId,
            };
            const sectionsKey = getRecipeSectionsQueryKey({ path });
            const itemsKey = getRecipeItemsQueryKey({ path });

            await queryClient.cancelQueries({ queryKey: sectionsKey });
            await queryClient.cancelQueries({ queryKey: itemsKey });

            const previousSections =
                queryClient.getQueryData<RecipeSectionResponse[]>(sectionsKey);
            const previousItems =
                queryClient.getQueryData<RecipeItemResponse[]>(itemsKey);

            queryClient.setQueryData<RecipeSectionResponse[]>(sectionsKey, (old) =>
                old?.filter((s) => s.id !== variables.path.sectionId),
            );
            queryClient.setQueryData<RecipeItemResponse[]>(itemsKey, (old) =>
                old?.filter((i) => i.sectionId !== variables.path.sectionId),
            );

            return { previousSections, previousItems };
        },
        onError: (_data, variables, context) => {
            const path = {
                householdId: variables.path.householdId,
                recipeId: variables.path.recipeId,
            };
            if (context?.previousSections) {
                queryClient.setQueryData(
                    getRecipeSectionsQueryKey({ path }),
                    context.previousSections,
                );
            }
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getRecipeItemsQueryKey({ path }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (_data, variables) => {
            toast(t("recipes.sectionDeleted"), {
                action: {
                    label: t("common.undo"),
                    onClick: () => {
                        restoreSection.mutate({ path: variables.path });
                    },
                },
                duration: 5000,
            });
        },
        onSettled: (_data, _error, variables) => {
            const path = {
                householdId: variables.path.householdId,
                recipeId: variables.path.recipeId,
            };
            debouncedInvalidate(getRecipeSectionsQueryKey({ path }));
            debouncedInvalidate(getRecipeItemsQueryKey({ path }));
        },
    });
};
